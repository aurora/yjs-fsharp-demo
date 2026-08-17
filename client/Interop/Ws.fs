/// Minimal WebSocket wrapper. Binary frames carry Yjs update bytes, text frames
/// carry JSON awareness messages - see server/Rooms.fs and Doc.fs. Kept dynamic
/// (same rationale as Canvas.fs) instead of fighting typed DOM bindings.
module Client.Interop.Ws

open Fable.Core
open Fable.Core.JsInterop

/// Reference to the browser's global `WebSocket` constructor. The explicit name is
/// required - without it Fable uses this binding's own F# name as the global lookup,
/// which would (wrongly) look for a global literally called `WebSocketCtor`.
[<Global("WebSocket")>]
let private WebSocketCtor: obj = jsNative

type Handlers =
    { OnOpen: unit -> unit
      OnClose: unit -> unit
      OnBinary: byte[] -> unit
      OnText: string -> unit }

type Connection = { Socket: obj }

let connect (url: string) (handlers: Handlers) : Connection =
    let socket: obj = createNew WebSocketCtor url
    socket?binaryType <- "arraybuffer"
    socket?onopen <- fun (_: obj) -> handlers.OnOpen()
    socket?onclose <- fun (_: obj) -> handlers.OnClose()

    socket?onmessage <-
        fun (ev: obj) ->
            let data: obj = ev?data

            match data with
            | :? string as s -> handlers.OnText s
            | _ ->
                // binaryType = "arraybuffer", so anything non-string is an ArrayBuffer.
                let bytes: byte[] = JS.Constructors.Uint8Array.Create(data) |> unbox
                handlers.OnBinary bytes

    { Socket = socket }

let isOpen (conn: Connection) : bool =
    let state: float = conn.Socket?readyState
    state = 1.0 // WebSocket.OPEN

let sendBinary (conn: Connection) (bytes: byte[]) : unit =
    if isOpen conn then
        conn.Socket?send (bytes)

let sendText (conn: Connection) (text: string) : unit =
    if isOpen conn then
        conn.Socket?send (text)
