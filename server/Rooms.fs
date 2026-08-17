/// The whole "CRDT server" in one file.
///
/// The trick that keeps this tiny: a Yjs update (the byte[] a Y.Doc emits whenever
/// something changes) is CRDT-safe to store and re-apply in ANY order, ANY number of
/// times, on top of ANY prior state. So the server does not need to speak Yjs at all -
/// it just needs to:
///   1. remember every update byte[] it has ever seen for a room ("the log"), and
///   2. relay every update live to the other connected clients, and
///   3. replay the whole log to anyone who joins late, so their local Y.Doc converges
///      to the same state as everyone else's.
///
/// Presence/awareness (cursor position, name, color, who is following whom) is a
/// separate, much simpler channel: plain JSON text frames, relayed live and never
/// logged (only the latest one per client is kept, so late joiners see who's online).
module Server.Rooms

open System
open System.Collections.Concurrent
open System.Net.WebSockets
open System.Text
open System.Threading
open System.Threading.Tasks

// ---------------------------------------------------------------------------
// Console debug logging - this is the "how does data flow" story for the demo.
// Every message that crosses the server is printed with a direction, a room,
// a connection id and a size/preview, so you can literally watch two browser
// tabs talking to each other through this process.
// ---------------------------------------------------------------------------

let private logLock = obj ()

let log (color: ConsoleColor) (msg: string) =
    lock logLock (fun () ->
        let prev = Console.ForegroundColor
        Console.ForegroundColor <- color
        printfn "%s  %s" (DateTime.Now.ToString("HH:mm:ss.fff")) msg
        Console.ForegroundColor <- prev)

let private shortId (id: Guid) = id.ToString("N").Substring(0, 6)

// ---------------------------------------------------------------------------
// Room state
// ---------------------------------------------------------------------------

type Connection =
    { Id: Guid
      Socket: WebSocket
      /// .NET's WebSocket forbids two overlapping SendAsync calls on the same
      /// instance. With several rooms' worth of concurrent senders all potentially
      /// broadcasting to the same recipient at once (10 people all moving their
      /// mouse at the same time, say), that overlap WILL happen without this - so
      /// every send to this connection, no matter which receive loop triggered it,
      /// queues up behind this one semaphore instead of racing.
      SendLock: SemaphoreSlim }

type RoomState =
    { Connections: ConcurrentDictionary<Guid, Connection>
      /// Every Yjs update byte[] ever received, in arrival order. Replayed to new joiners.
      UpdateLog: ResizeArray<byte[]>
      UpdateLogLock: obj
      /// Latest known awareness JSON per connection (cursor/name/color/follow).
      Presence: ConcurrentDictionary<Guid, string> }

    static member Create() =
        { Connections = ConcurrentDictionary()
          UpdateLog = ResizeArray()
          UpdateLogLock = obj ()
          Presence = ConcurrentDictionary() }

let private rooms = ConcurrentDictionary<string, RoomState>()

let getOrCreateRoom (roomId: string) : RoomState =
    rooms.GetOrAdd(roomId, fun _ ->
        log ConsoleColor.Magenta $"[{roomId}] room created"
        RoomState.Create())

// ---------------------------------------------------------------------------
// Low-level send/receive helpers
// ---------------------------------------------------------------------------

let private sendBytes (conn: Connection) (bytes: byte[]) (messageType: WebSocketMessageType) : Task =
    task {
        if conn.Socket.State = WebSocketState.Open then
            do! conn.SendLock.WaitAsync()

            try
                if conn.Socket.State = WebSocketState.Open then
                    do! conn.Socket.SendAsync(ArraySegment(bytes), messageType, true, CancellationToken.None)
            finally
                conn.SendLock.Release() |> ignore
    }
    :> Task

/// Reads one full logical WebSocket message, transparently reassembling frames
/// that were split across multiple ReceiveAsync calls (large sync payloads etc).
let private receiveFullMessage (socket: WebSocket) : Task<WebSocketReceiveResult * byte[]> =
    task {
        use ms = new IO.MemoryStream()
        let buffer = Array.zeroCreate<byte> (16 * 1024)
        let mutable result = Unchecked.defaultof<WebSocketReceiveResult>
        let mutable more = true

        while more do
            let! r = socket.ReceiveAsync(ArraySegment(buffer), CancellationToken.None)
            result <- r

            if r.MessageType <> WebSocketMessageType.Close then
                ms.Write(buffer, 0, r.Count)

            more <- not r.EndOfMessage && r.MessageType <> WebSocketMessageType.Close

        return result, ms.ToArray()
    }

/// Fans a message out to every other connection in the room *concurrently* - each
/// individual send is still serialized per-destination via that connection's own
/// SendLock (see sendBytes), so this is safe; it just stops N-1 sequential awaits
/// from stacking their latency up one after another as the room grows.
let private broadcast (room: RoomState) (exceptId: Guid) (bytes: byte[]) (messageType: WebSocketMessageType) : Task =
    room.Connections.Values
    |> Seq.filter (fun c -> c.Id <> exceptId)
    |> Seq.map (fun conn ->
        task {
            try
                do! sendBytes conn bytes messageType
            with ex ->
                log ConsoleColor.Red $"  ! relay to {shortId conn.Id} failed: {ex.Message}"
        }
        :> Task)
    |> Task.WhenAll

// ---------------------------------------------------------------------------
// Per-connection lifecycle
// ---------------------------------------------------------------------------

let handleConnection (roomId: string) (socket: WebSocket) : Task =
    task {
        let room = getOrCreateRoom roomId
        let connId = Guid.NewGuid()
        let conn = { Id = connId; Socket = socket; SendLock = new SemaphoreSlim(1, 1) }
        room.Connections.[connId] <- conn
        log ConsoleColor.Green $"[{roomId}] + {shortId connId} connected  ({room.Connections.Count} online)"

        // Catch the newcomer up.
        let bufferedUpdates = lock room.UpdateLogLock (fun () -> room.UpdateLog.ToArray())

        if bufferedUpdates.Length > 0 then
            log ConsoleColor.DarkGreen $"[{roomId}]   -> replaying {bufferedUpdates.Length} buffered Y-UPDATE(s) to {shortId connId}"

        for update in bufferedUpdates do
            do! sendBytes conn update WebSocketMessageType.Binary

        for kv in room.Presence do
            if kv.Key <> connId then
                do! sendBytes conn (Encoding.UTF8.GetBytes(kv.Value: string)) WebSocketMessageType.Text

        // Receive loop.
        let mutable closed = false

        while not closed do
            try
                let! (result, bytes) = receiveFullMessage socket

                match result.MessageType with
                | WebSocketMessageType.Close -> closed <- true
                | WebSocketMessageType.Binary ->
                    lock room.UpdateLogLock (fun () -> room.UpdateLog.Add(bytes))
                    log ConsoleColor.Yellow $"[{roomId}] <- Y-UPDATE   {shortId connId}  {bytes.Length,6} bytes   (log now holds {room.UpdateLog.Count})"
                    do! broadcast room connId bytes WebSocketMessageType.Binary
                | WebSocketMessageType.Text ->
                    let text = Encoding.UTF8.GetString(bytes)
                    room.Presence.[connId] <- text
                    let preview = if text.Length > 100 then text.Substring(0, 100) + "…" else text
                    log ConsoleColor.Cyan $"[{roomId}] <- AWARENESS {shortId connId}  {preview}"
                    do! broadcast room connId bytes WebSocketMessageType.Text
                | _ -> ()
            with ex ->
                log ConsoleColor.Red $"[{roomId}]  ! receive error for {shortId connId}: {ex.Message}"
                closed <- true

        room.Connections.TryRemove(connId) |> ignore
        room.Presence.TryRemove(connId) |> ignore
        log ConsoleColor.Red $"[{roomId}] - {shortId connId} disconnected  ({room.Connections.Count} online)"

        let byeMsg = $"{{\"type\":\"bye\",\"id\":\"{connId}\"}}"
        do! broadcast room connId (Encoding.UTF8.GetBytes(byeMsg)) WebSocketMessageType.Text
        // Not disposing conn.SendLock here: a broadcast from another connection's
        // receive loop may still be mid-flight against it (it was only just removed
        // from room.Connections above) - disposing underneath that would turn a
        // harmless, already-logged send failure into an ObjectDisposedException
        // race instead. It's a per-connection SemaphoreSlim; letting the GC reclaim
        // it once every in-flight send has drained is the simpler, safer call here.

        if socket.State <> WebSocketState.Closed && socket.State <> WebSocketState.Aborted then
            try
                do! socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None)
            with _ -> ()
    }
    :> Task
