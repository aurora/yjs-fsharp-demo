/// The app-specific layer on top of Interop/Yjs.fs: this is where "sticky note"
/// becomes a concrete shape inside the shared Y.Doc, and where the WebSocket is
/// wired to Yjs's own update stream. Nothing outside this file (and Interop/Yjs.fs)
/// ever calls into the Yjs API directly.
///
/// Document shape:
///   doc
///    ├─ "notes"     : Y.Map<noteId, Y.Map>      -- one entry per sticky note
///    │                  each note Y.Map: { x, y, w, h, color, text: Y.Text }
///    └─ "noteOrder" : Y.Array<noteId>            -- creation/z-order
///
/// Splitting each note's fields (x/y/w/h/color) from its body (Y.Text) means two
/// people can drag a note and type into it at the same time without either edit
/// ever being lost - Yjs merges them field-by-field / character-by-character.
module Client.Doc

open Fable.Core.JsInterop
open Client.Types
open Client.Interop
open Client.Interop.Yjs

// -- the shared CRDT document: one instance, created once, lives for the page's lifetime --
let private doc = createDoc ()
let private notesMap = getMap "notes" doc
let private noteOrder = getArray "noteOrder" doc

/// My own identity for this session = Yjs's own randomly assigned doc.clientID.
/// Reusing it means we don't need a second id scheme for presence/awareness.
let myClientId: ClientId = (doc :> obj)?clientID

let private connRef: Ws.Connection option ref = ref None

/// Wired up once by Program.fs so every send/receive also reaches the on-screen
/// debug panel, not just the browser console - this is the "how does data flow"
/// story for the demo.
let mutable debugSink: DebugDirection -> string -> string -> unit = fun _ _ _ -> ()

let private log (dir: DebugDirection) (kind: string) (detail: string) =
    let arrow =
        match dir with
        | In -> "IN <-"
        | Out -> "OUT->"
        | Info -> "....."

    Browser.Dom.console.log ($"[collab] {arrow} {kind,-10} {detail}")
    debugSink dir kind detail

// -- snapshot: rebuild the read-model from the live Y.Doc -----------------------

/// Called after every local or remote change. This is the only place that reads
/// note data back out of Yjs - everything else in the app works off the resulting
/// plain F# records.
let snapshot () : NoteSnapshot list * string list =
    let order = arrayToList noteOrder

    let notes =
        mapKeys notesMap
        |> Array.toList
        |> List.map (fun id ->
            let noteMap = mapGetObj id notesMap :?> YMap
            let text = mapGetObj "text" noteMap :?> YText

            { Id = id
              X = mapGetFloat "x" noteMap
              Y = mapGetFloat "y" noteMap
              W = mapGetFloat "w" noteMap
              H = mapGetFloat "h" noteMap
              Color = mapGetString "color" noteMap |> NoteColor.ofStorage
              Text = textToString text })

    notes, order

let private tryGetNoteMap (id: string) : YMap option =
    if Array.contains id (mapKeys notesMap) then
        Some(mapGetObj id notesMap :?> YMap)
    else
        None

// -- mutations (each is a single Yjs transaction -> a single update on the wire) --

let addNote (color: NoteColor) (x: float) (y: float) : string =
    let id = System.Guid.NewGuid().ToString("N")

    transact doc (fun () ->
        let noteMap = newMap ()
        mapSet "x" (box x) noteMap
        mapSet "y" (box y) noteMap
        mapSet "w" (box 190.0) noteMap
        mapSet "h" (box 150.0) noteMap
        mapSet "color" (box (NoteColor.toStorage color)) noteMap
        mapSet "text" (box (newText "")) noteMap
        mapSet id (box noteMap) notesMap
        arrayPush (box id) noteOrder)

    log Out "note-add" $"{id.Substring(0, 6)} color={NoteColor.toStorage color}"
    id

let moveNote (id: string) (x: float) (y: float) : unit =
    match tryGetNoteMap id with
    | Some noteMap ->
        transact doc (fun () ->
            mapSet "x" (box x) noteMap
            mapSet "y" (box y) noteMap)
    | None -> ()

let deleteNote (id: string) : unit =
    transact doc (fun () ->
        mapDelete id notesMap
        arrayRemoveValue id noteOrder)

    log Out "note-delete" (id.Substring(0, 6))

/// Turns a textarea's `input` event into a minimal Y.Text edit instead of a
/// full clear-and-rewrite, by diffing off the common prefix/suffix of old vs new
/// text. This is what lets two people type in different parts of the same note
/// at the same time without stomping on each other - Y.Text's CRDT merges the two
/// small, disjoint edits cleanly instead of one full-text write clobbering the other.
let editNoteText (id: string) (oldText: string) (newText': string) : unit =
    match tryGetNoteMap id with
    | Some noteMap ->
        let textNode = mapGetObj "text" noteMap :?> YText
        let oldLen = oldText.Length
        let newLen = newText'.Length
        let maxCommon = min oldLen newLen

        let mutable prefix = 0

        while prefix < maxCommon && oldText.[prefix] = newText'.[prefix] do
            prefix <- prefix + 1

        let mutable suffix = 0

        while suffix < (maxCommon - prefix)
              && oldText.[oldLen - 1 - suffix] = newText'.[newLen - 1 - suffix] do
            suffix <- suffix + 1

        let removedLen = oldLen - prefix - suffix
        let insertedText = newText'.Substring(prefix, newLen - prefix - suffix)

        transact doc (fun () ->
            if removedLen > 0 then
                textDelete prefix removedLen textNode

            if insertedText.Length > 0 then
                textInsert prefix insertedText textNode)

        log Out "note-edit" $"{id.Substring(0, 6)} -{removedLen}/+{insertedText.Length} chars @ {prefix}"
    | None -> ()

// -- wiring: Yjs <-> WebSocket, Yjs -> read-model dispatch -----------------------

let connect (dispatch: Msg -> unit) : unit =
    // Any change to the notes tree (ours or a remote peer's) -> refresh the read-model.
    observeDeep
        (fun () ->
            let notes, order = snapshot ()
            dispatch (DocChanged(notes, order)))
        notesMap

    // Yjs -> server: forward every locally-caused change as a binary update. Changes
    // caused by applyRemoteUpdate (below) carry `remoteOrigin` and are filtered out
    // inside onLocalUpdate, so we never echo a peer's own change back to them.
    onLocalUpdate doc (fun bytes ->
        match connRef.Value with
        | Some conn ->
            log Out "y-update" $"{bytes.Length} bytes"
            Ws.sendBinary conn bytes
        | None -> ())

    let loc = Browser.Dom.window.location
    let proto = if loc.protocol = "https:" then "wss" else "ws"
    let url = $"{proto}://{loc.host}/ws?room=default"

    let handlers: Ws.Handlers =
        { OnOpen =
            fun () ->
                log Info "socket" "connected"
                dispatch SocketOpened
          OnClose =
            fun () ->
                log Info "socket" "disconnected"
                dispatch SocketClosed
          OnBinary =
            fun bytes ->
                log In "y-update" $"{bytes.Length} bytes"
                // Triggers the observeDeep above, which dispatches DocChanged - no
                // need to dispatch anything from here directly.
                applyRemoteUpdate doc bytes
          OnText =
            fun text ->
                log In "awareness" text
                dispatch (AwarenessReceived text) }

    connRef.Value <- Some(Ws.connect url handlers)

let sendAwareness (json: string) : unit =
    match connRef.Value with
    | Some conn ->
        log Out "awareness" json
        Ws.sendText conn json
    | None -> ()
