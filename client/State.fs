/// The MVU core: initial Model, and the single `update` function every Msg flows
/// through. Side effects onto the shared Y.Doc (add/move/delete/edit a note) are
/// issued directly from here at the point the user's intent is decided - the Y.Doc
/// itself stays the single source of truth, and its own change observer (Doc.connect)
/// is what feeds the resulting `DocChanged` message back into this same loop.
module Client.State

open System
open Fable.Core
open Elmish
open Client.Types
open Client.Camera

type StartupConfig =
    { Name: string
      Color: string
      ClientId: ClientId }

let init (cfg: StartupConfig) () : Model * Cmd<Msg> =
    let model =
        { Me = Some cfg.ClientId
          MyName = cfg.Name
          MyColor = cfg.Color
          Notes = Map.empty
          NoteOrder = []
          Presence = Map.empty
          Camera = { X = 0.0; Y = 0.0; Zoom = 1.0 }
          ViewportW = Browser.Dom.window.innerWidth
          ViewportH = Browser.Dom.window.innerHeight
          LocalCursorWorld = None
          LastSentCursor = None
          LastHeartbeatAt = DateTime.MinValue
          LastDragCommitAt = DateTime.MinValue
          Drag = NotDragging
          EditingNoteId = None
          Following = None
          Connected = false
          DebugLog = []
          DebugOpen = true }

    model, Cmd.none

let private maxDebugEntries = 150

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg with
    | SocketOpened -> { model with Connected = true }, Cmd.none
    | SocketClosed -> { model with Connected = false }, Cmd.none

    | DocChanged(notes, order) ->
        let notesMap = notes |> List.map (fun n -> n.Id, n) |> Map.ofList
        let editing =
            match model.EditingNoteId with
            | Some id when not (Map.containsKey id notesMap) -> None // note was deleted remotely
            | other -> other
        { model with
            Notes = notesMap
            NoteOrder = order
            EditingNoteId = editing },
        Cmd.none

    | AwarenessReceived json ->
        match Awareness.tryDecode json with
        | Some(Awareness.Presence(id, name, color, cursor)) when Some id <> model.Me ->
            let initial =
                if name.Length > 0 then
                    string (Char.ToUpper name.[0])
                else
                    "?"

            let info: PresenceInfo =
                { ClientId = id
                  Name = name
                  Initial = initial
                  Color = color
                  Cursor = cursor
                  LastSeen = DateTime.Now }

            let newCamera =
                match model.Following, cursor with
                | Some followId, Some c when followId = id -> centerOn model.ViewportW model.ViewportH c model.Camera
                | _ -> model.Camera

            { model with
                Presence = Map.add id info model.Presence
                Camera = newCamera },
            Cmd.none
        | Some(Awareness.Bye id) ->
            let following = if model.Following = Some id then None else model.Following

            { model with
                Presence = Map.remove id model.Presence
                Following = following },
            Cmd.none
        | _ -> model, Cmd.none

    | MouseDown(screenPoint, button) ->
        let worldPoint = screenToWorld model.Camera screenPoint

        if button = 0 then
            match Canvas.hitTest model worldPoint with
            | Canvas.HitDeleteButton id ->
                Doc.deleteNote id
                model, Cmd.none
            | Canvas.HitNote id ->
                match Map.tryFind id model.Notes with
                | Some note ->
                    let drag = DraggingNote(id, worldPoint.X - note.X, worldPoint.Y - note.Y)
                    { model with Drag = drag }, Cmd.none
                | None -> model, Cmd.none
            | Canvas.HitNothing ->
                let drag = PanningCamera(screenPoint, { X = model.Camera.X; Y = model.Camera.Y })
                { model with Drag = drag; Following = None }, Cmd.none
        else
            model, Cmd.none

    | MouseMove screenPoint ->
        let worldPoint = screenToWorld model.Camera screenPoint

        match model.Drag with
        | DraggingNote(id, dx, dy) ->
            let newX, newY = worldPoint.X - dx, worldPoint.Y - dy

            // Update the local view on every single mousemove, so dragging feels
            // exactly as responsive as it would without any collaboration at all -
            // this is purely a local model update, no network/Yjs involved.
            let optimisticNotes =
                match Map.tryFind id model.Notes with
                | Some note -> Map.add id { note with X = newX; Y = newY } model.Notes
                | None -> model.Notes

            // ...but only commit to Yjs (and therefore onto the wire and into the
            // server's update log) at a throttled rate. ~25 commits/sec is still
            // perfectly smooth motion for everyone *watching* the drag, while
            // cutting a 100+Hz mousemove stream down to a fraction of the Yjs
            // updates it would otherwise generate.
            let now = DateTime.Now

            if (now - model.LastDragCommitAt).TotalMilliseconds > 40.0 then
                Doc.moveNote id newX newY

                { model with
                    Notes = optimisticNotes
                    LocalCursorWorld = Some worldPoint
                    LastDragCommitAt = now },
                Cmd.none
            else
                { model with
                    Notes = optimisticNotes
                    LocalCursorWorld = Some worldPoint },
                Cmd.none
        | PanningCamera(startScreen, startCam) ->
            let cam =
                { model.Camera with
                    X = startCam.X - (screenPoint.X - startScreen.X) / model.Camera.Zoom
                    Y = startCam.Y - (screenPoint.Y - startScreen.Y) / model.Camera.Zoom }

            { model with Camera = cam; LocalCursorWorld = Some worldPoint }, Cmd.none
        | NotDragging -> { model with LocalCursorWorld = Some worldPoint }, Cmd.none

    | MouseUp ->
        // Always commit the final position, even if the last mousemove landed
        // inside the throttle window and never made it to Yjs - nobody else should
        // ever see a dropped note lag behind where you actually released it.
        (match model.Drag with
         | DraggingNote(id, _, _) ->
             match Map.tryFind id model.Notes with
             | Some note -> Doc.moveNote id note.X note.Y
             | None -> ()
         | _ -> ())

        { model with Drag = NotDragging }, Cmd.none

    | DoubleClick screenPoint ->
        let worldPoint = screenToWorld model.Camera screenPoint

        match Canvas.hitTest model worldPoint with
        | Canvas.HitNote id
        | Canvas.HitDeleteButton id -> { model with EditingNoteId = Some id }, Cmd.none
        | Canvas.HitNothing -> model, Cmd.none

    | Wheel(screenPoint, deltaY) ->
        let factor = exp (-deltaY * 0.001)
        let cam = zoomAt screenPoint factor model.Camera
        { model with Camera = cam; Following = None }, Cmd.none

    | WindowResized(w, h) -> { model with ViewportW = w; ViewportH = h }, Cmd.none

    | AddNote color ->
        let center =
            screenToWorld model.Camera { X = model.ViewportW / 2.0; Y = model.ViewportH / 2.0 }

        let jitter = (JS.Math.random () - 0.5) * 60.0
        Doc.addNote color (center.X - 95.0 + jitter) (center.Y - 75.0 + jitter) |> ignore
        model, Cmd.none

    | DeleteNote id ->
        Doc.deleteNote id
        let editing = if model.EditingNoteId = Some id then None else model.EditingNoteId
        { model with EditingNoteId = editing }, Cmd.none

    | StartEditNote id -> { model with EditingNoteId = Some id }, Cmd.none

    | EditNoteText(id, newText) ->
        match Map.tryFind id model.Notes with
        | Some note -> Doc.editNoteText id note.Text newText
        | None -> ()

        model, Cmd.none

    | StopEditNote -> { model with EditingNoteId = None }, Cmd.none

    | ToggleFollow clientId ->
        let following = if model.Following = Some clientId then None else Some clientId
        { model with Following = following }, Cmd.none

    | ToggleDebugPanel -> { model with DebugOpen = not model.DebugOpen }, Cmd.none

    | HeartbeatTick ->
        // Only put a frame on the wire (and in the debug log) when the cursor actually
        // moved, or as an infrequent keepalive otherwise - a plain fixed-rate heartbeat
        // would flood the demo's own debug log with identical "didn't move" pings and
        // drown out the traffic that's actually interesting to watch.
        match model.Me with
        | Some me when model.Connected ->
            let now = DateTime.Now

            let moved =
                match model.LastSentCursor, model.LocalCursorWorld with
                | Some last, Some cur -> abs (last.X - cur.X) + abs (last.Y - cur.Y) > 1.0
                | None, Some _ -> true
                | _, None -> false

            let staleKeepAlive = (now - model.LastHeartbeatAt).TotalMilliseconds > 1000.0

            if moved || staleKeepAlive then
                let json = Awareness.encodePresence me model.MyName model.MyColor model.LocalCursorWorld
                Doc.sendAwareness json

                { model with
                    LastSentCursor = model.LocalCursorWorld
                    LastHeartbeatAt = now },
                Cmd.none
            else
                model, Cmd.none
        | _ -> model, Cmd.none

    | RenameSelf name ->
        let trimmed = name.Trim()
        { model with MyName = (if trimmed = "" then model.MyName else trimmed) }, Cmd.none

    | LogDebug(dir, kind, detail) ->
        let entry: DebugEntry =
            { Time = DateTime.Now
              Direction = dir
              Kind = kind
              Detail = detail }

        { model with DebugLog = entry :: model.DebugLog |> List.truncate maxDebugEntries }, Cmd.none
