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

/// Scoped to the whole board (all notes, including their nested x/y/color/text fields, plus
/// their order) - Ctrl+Z undoes the single most recent LOCAL change anywhere on the board, no
/// matter what other users did in the meantime. That last guarantee is not extra work: it's
/// the same origin mechanism as onLocalUpdate/applyRemoteUpdate below - by default an
/// UndoManager only tracks transactions with no explicit origin, which is exactly the
/// complement of the `remoteOrigin`-tagged ones, so a remote peer's changes are structurally
/// invisible to it.
let private undoManager = newUndoManager [| box notesMap; box noteOrder |]

/// My own identity for this session = Yjs's own randomly assigned doc.clientID.
/// Reusing it means we don't need a second id scheme for presence/awareness.
let myClientId: ClientId = (doc :> obj)?clientID

let private connRef: Ws.Connection option ref = ref None

/// Wired up once by Program.fs so every send/receive also reaches the on-screen
/// debug panel, not just the browser console - this is the "how does data flow"
/// story for the demo.
let mutable debugSink: DebugDirection -> string -> string -> unit = fun _ _ _ -> ()

/// Mirroring every wire message to `console.log` as well is OFF by default. It's not just
/// noise: with DevTools actually open, browsers retain every logged line indefinitely, and
/// that retention is a well-known real performance drag - open it across two or three browser
/// windows at once (each independently sending/receiving awareness pings and Yjs updates) and
/// it can visibly bog down the whole machine, not just the tab. The on-screen debug panel
/// (View.fs, capped and rendered incrementally) stays the primary "watch the traffic" view;
/// this is an opt-in extra for when DevTools is actually open and wanted - see
/// `window.__collab.setVerbose` in Program.fs.
let mutable consoleLoggingEnabled = false

let private log (dir: DebugDirection) (kind: string) (detail: string) =
    if consoleLoggingEnabled then
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

            // Defensive: title/description were added to the note "shape" after this prototype
            // already had notes in some running sessions - fall back to empty rather than crash.
            let title =
                if mapHas "title" noteMap then
                    mapGetString "title" noteMap
                else
                    ""

            let description =
                if mapHas "description" noteMap then
                    mapGetObj "description" noteMap :?> YText |> textToString
                else
                    ""

            { Id = id
              X = mapGetFloat "x" noteMap
              Y = mapGetFloat "y" noteMap
              W = mapGetFloat "w" noteMap
              H = mapGetFloat "h" noteMap
              Color = mapGetString "color" noteMap |> NoteColor.ofStorage
              Text = textToString text
              Title = title
              Description = description })

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
        mapSet "title" (box "") noteMap
        mapSet "description" (box (newText "")) noteMap
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

/// A short, low-collision-risk field (a title) doesn't need Y.Text - a plain string value on
/// the Y.Map is fine, with ordinary causally-correct last-writer-wins semantics per field. See
/// docs/02-architektur-muster.md #6: not everything needs character-level merge.
let setNoteTitle (id: string) (title: string) : unit =
    match tryGetNoteMap id with
    | Some noteMap ->
        transact doc (fun () -> mapSet "title" (box title) noteMap)
        log Out "note-title" $"{id.Substring(0, 6)} -> \"{title}\""
    | None -> ()

/// Turns a textarea's `input` event into a minimal Y.Text edit instead of a
/// full clear-and-rewrite, by diffing off the common prefix/suffix of old vs new
/// text. This is what lets two people type in different parts of the same field
/// at the same time without stomping on each other - Y.Text's CRDT merges the two
/// small, disjoint edits cleanly instead of one full-text write clobbering the other.
/// `fieldKey` picks which Y.Text on the note this applies to - the sticky's own body
/// ("text") and its longer-form "description" both go through this same helper.
let private editNoteTextField (fieldKey: string) (id: string) (oldText: string) (newText': string) : unit =
    match tryGetNoteMap id with
    | Some noteMap ->
        let textNode = mapGetObj fieldKey noteMap :?> YText
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

        log Out $"note-{fieldKey}" $"{id.Substring(0, 6)} -{removedLen}/+{insertedText.Length} chars @ {prefix}"
    | None -> ()

let editNoteText (id: string) (oldText: string) (newText': string) : unit =
    editNoteTextField "text" id oldText newText'

let editNoteDescription (id: string) (oldText: string) (newText': string) : unit =
    editNoteTextField "description" id oldText newText'

// -- undo/redo --------------------------------------------------------------------

let undo () : unit =
    if Yjs.canUndo undoManager then
        log Out "undo" "reverting last local change"
        Yjs.undo undoManager

let redo () : unit =
    if Yjs.canRedo undoManager then
        log Out "redo" "reapplying last undone change"
        Yjs.redo undoManager

let canUndo () : bool = Yjs.canUndo undoManager
let canRedo () : bool = Yjs.canRedo undoManager

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
