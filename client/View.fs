/// The DOM "shell" around the canvas: presence bar, toolbar, debug panel, and the
/// sticky-note edit overlay. DOM elements are treated as dynamic JS objects (same
/// rationale as Canvas.fs) so this file isn't at the mercy of exact binding shapes.
///
/// Follows the classic non-React Elmish pattern: `view` is called after every model
/// update, but the actual DOM tree + event listeners are only built once (on the
/// first call) - see `mounted`/`cachedRender` below.
module Client.View

open Fable.Core.JsInterop
open Client.Types
open Client.Camera

let private byId (id: string) : obj = (Browser.Dom.document :> obj)?getElementById (id)

let private escapeHtml (s: string) =
    s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")

/// Diff-patches a live <textarea> to `newText` while preserving the caret position
/// as best as possible, instead of a blind `value <- newText` (which would always
/// snap the caret back to position 0 - unacceptable while someone is mid-sentence).
/// Same prefix/suffix trick as Doc.editNoteText, applied in the other direction.
let private patchTextareaIfChanged (ta: obj) (newText: string) =
    let oldText: string = ta?value

    if oldText <> newText then
        let selStart: int = ta?selectionStart
        let selEnd: int = ta?selectionEnd
        let oldLen = oldText.Length
        let newLen = newText.Length
        let maxCommon = min oldLen newLen

        let mutable prefix = 0

        while prefix < maxCommon && oldText.[prefix] = newText.[prefix] do
            prefix <- prefix + 1

        let mutable suffix = 0

        while suffix < (maxCommon - prefix)
              && oldText.[oldLen - 1 - suffix] = newText.[newLen - 1 - suffix] do
            suffix <- suffix + 1

        let insertedLen = newLen - prefix - suffix
        let removedLen = oldLen - prefix - suffix
        let delta = insertedLen - removedLen

        let adjust (pos: int) =
            if pos <= prefix then pos
            elif pos >= oldLen - suffix then pos + delta
            else prefix + insertedLen // caret was inside the edited span -> snap after it

        ta?value <- newText
        ta?setSelectionRange (adjust selStart, adjust selEnd)

let private mountShell (dispatch: Msg -> unit) : (Model -> unit) =
    // One-time wiring of the shared Y.Doc <-> WebSocket connection, and the
    // presence heartbeat. `dispatch` only exists once Elmish starts running the
    // program, so this - not Program.fs - is where the "app has started" hook lives.
    Client.Doc.debugSink <- fun dir kind detail -> dispatch (LogDebug(dir, kind, detail))
    Client.Doc.connect dispatch
    (Browser.Dom.window :> obj)?setInterval ((fun () -> dispatch HeartbeatTick), 120) |> ignore

    let nameInput = byId "my-name"
    let wrap = byId "canvas-wrap"
    let canvas = byId "board"
    let editOverlay = byId "edit-overlay"
    let presenceBar = byId "presence-bar"
    let debugPanel = byId "debug-panel"
    let debugLog = byId "debug-log"
    let debugToggle = byId "debug-toggle"
    let connStatus = byId "conn-status"
    let connText: obj = connStatus?querySelector (".conn-text")
    let undoBtn = byId "undo-btn"
    let redoBtn = byId "redo-btn"
    let notePanel = byId "note-panel"
    let notePanelClose = byId "note-panel-close"
    let noteTitleInput = byId "note-title-input"
    let noteDescTextarea = byId "note-desc-textarea"
    let titleBadge = byId "note-title-badge"
    let descBadge = byId "note-desc-badge"

    // -- one-time sizing / resize wiring -----------------------------------------

    let resizeCanvas () =
        let w: float = wrap?clientWidth
        let h: float = wrap?clientHeight
        canvas?width <- w
        canvas?height <- h
        dispatch (WindowResized(w, h))

    resizeCanvas ()
    (Browser.Dom.window :> obj)?addEventListener ("resize", fun (_: obj) -> resizeCanvas ())

    // -- one-time input wiring ----------------------------------------------------

    let localPoint (e: obj) : Vec2 =
        let rect: obj = canvas?getBoundingClientRect ()
        let clientX: float = e?clientX
        let clientY: float = e?clientY
        let left: float = rect?left
        let top: float = rect?top
        { X = clientX - left; Y = clientY - top }

    canvas?addEventListener (
        "mousedown",
        fun (e: obj) ->
            let button: float = e?button
            dispatch (MouseDown(localPoint e, int button))
    )

    canvas?addEventListener ("dblclick", fun (e: obj) -> dispatch (DoubleClick(localPoint e)))

    canvas?addEventListener (
        "wheel",
        (fun (e: obj) ->
            e?preventDefault ()
            let deltaY: float = e?deltaY
            dispatch (Wheel(localPoint e, deltaY))),
        createObj [ "passive" ==> false ]
    )

    // mousemove/mouseup on the window (not just the canvas) so a drag started on
    // the canvas keeps tracking even if the pointer briefly leaves it.
    (Browser.Dom.window :> obj)?addEventListener ("mousemove", fun (e: obj) -> dispatch (MouseMove(localPoint e)))
    (Browser.Dom.window :> obj)?addEventListener ("mouseup", fun (_: obj) -> dispatch MouseUp)

    let mutable currentEditId: string option = None

    // The text we ourselves last told the model to become, while we wait for it to
    // round-trip back through Yjs -> observeDeep -> DocChanged. Until that echo
    // arrives, the model's note text is *expected* to lag behind what's already
    // sitting in the textarea (which the browser updated natively on keystroke) -
    // patching the textarea against that stale model in the meantime would fight
    // the user's own typing and leave the caret one keystroke behind. See
    // syncEditOverlay below.
    let mutable pendingSelfText: string option = None

    editOverlay?addEventListener (
        "input",
        fun (_: obj) ->
            match currentEditId with
            | Some id ->
                let value: string = editOverlay?value
                pendingSelfText <- Some value
                dispatch (EditNoteText(id, value))
            | None -> ()
    )

    editOverlay?addEventListener ("blur", fun (_: obj) -> dispatch StopEditNote)

    editOverlay?addEventListener (
        "keydown",
        fun (e: obj) ->
            let key: string = e?key
            if key = "Escape" then editOverlay?blur ()
    )

    nameInput?addEventListener (
        "change",
        fun (_: obj) ->
            let value: string = nameInput?value
            dispatch (RenameSelf value)
    )

    let swatches: obj = (Browser.Dom.document :> obj)?querySelectorAll (".swatch")

    swatches?forEach (fun (el: obj) ->
        let colorAttr: string = el?getAttribute ("data-color")
        let color = NoteColor.ofStorage colorAttr
        el?onclick <- fun (_: obj) -> dispatch (AddNote color))

    debugToggle?addEventListener ("click", fun (_: obj) -> dispatch ToggleDebugPanel)

    undoBtn?addEventListener ("click", fun (_: obj) -> dispatch Undo)
    redoBtn?addEventListener ("click", fun (_: obj) -> dispatch Redo)

    // Ctrl/Cmd+Z (+Shift) and Ctrl/Cmd+Y for board-wide undo/redo - but NOT while a text field
    // is focused. <textarea>/<input> have their own native undo (character-level, based on
    // selectionStart/selectionEnd) that should keep handling Ctrl+Z while actively typing;
    // hijacking it there would fight the browser's own, perfectly good undo for free-text
    // edits. Outside of a text field, Ctrl+Z always means "undo the last board-wide change".
    (Browser.Dom.window :> obj)?addEventListener (
        "keydown",
        fun (e: obj) ->
            let ctrlOrMeta: bool = (e?ctrlKey: bool) || (e?metaKey: bool)

            if ctrlOrMeta then
                let activeEl: obj = (Browser.Dom.document :> obj)?activeElement

                let activeTag: string =
                    if isNull activeEl then "" else activeEl?tagName

                let isTextField = activeTag = "TEXTAREA" || activeTag = "INPUT"

                if not isTextField then
                    let key: string = (e?key: string).ToLower()
                    let shift: bool = e?shiftKey

                    if key = "z" then
                        e?preventDefault ()
                        dispatch (if shift then Redo else Undo)
                    elif key = "y" then
                        e?preventDefault ()
                        dispatch Redo
    )

    // -- note detail panel (title + description) ----------------------------------
    //
    // A small, deliberately minimal example of the "multiple UI surfaces, one document"
    // pattern from docs/02-architektur-muster.md #6: Title is a plain LWW string field (no
    // Y.Text needed - short, low collision risk), Description is real Y.Text co-editing, same
    // diff-and-patch machinery as the sticky note's own body. Neither field shows other users'
    // in-text caret position - see docs/03-fallstricke.md #6 for why that would need a real
    // editor engine (ProseMirror/CodeMirror-class) instead of a plain <input>/<textarea>; what
    // we *can* show cheaply is an "X is typing here" badge via the awareness channel, which is
    // exactly what the note-title-badge/note-desc-badge elements below are for.

    let mutable currentPanelNoteId: string option = None
    let mutable pendingSelfTitle: string option = None
    let mutable pendingSelfDescription: string option = None

    notePanelClose?addEventListener ("click", fun (_: obj) -> dispatch (SelectNote None))

    noteTitleInput?addEventListener (
        "input",
        fun (_: obj) ->
            match currentPanelNoteId with
            | Some id ->
                let value: string = noteTitleInput?value
                pendingSelfTitle <- Some value
                dispatch (SetNoteTitle(id, value))
            | None -> ()
    )

    noteTitleInput?addEventListener (
        "focus",
        fun (_: obj) ->
            match currentPanelNoteId with
            | Some id -> dispatch (SetEditingField(Some(id, "title")))
            | None -> ()
    )

    noteTitleInput?addEventListener ("blur", fun (_: obj) -> dispatch (SetEditingField None))

    noteDescTextarea?addEventListener (
        "input",
        fun (_: obj) ->
            match currentPanelNoteId with
            | Some id ->
                let value: string = noteDescTextarea?value
                pendingSelfDescription <- Some value
                dispatch (EditNoteDescription(id, value))
            | None -> ()
    )

    noteDescTextarea?addEventListener (
        "focus",
        fun (_: obj) ->
            match currentPanelNoteId with
            | Some id -> dispatch (SetEditingField(Some(id, "description")))
            | None -> ()
    )

    noteDescTextarea?addEventListener ("blur", fun (_: obj) -> dispatch (SetEditingField None))

    // -- per-render helpers ---------------------------------------------------------

    // Only the newest entry, so we can tell whether one was actually added (dispatch
    // frequency at 10 users can be high - rebuilding this whole list on every single
    // dispatch, even ones that added nothing, was the other half of the churn bug).
    let mutable lastRenderedHead: DebugEntry option = None

    // Rebuilding the presence bar's DOM is only safe/cheap to do when who's-online
    // actually changed. Model updates fire on *every* mousemove anywhere on the page
    // (not just over the canvas), so without this guard the avatar under your cursor
    // gets destroyed and recreated many times a second: :hover flickers on/off (looks
    // like the icon "jumping"), and a click's mousedown/mouseup can land on two
    // different DOM nodes and never register as a click at all.
    let mutable lastPresenceSignature = ""

    let renderPresence (model: Model) =
        let signature =
            model.MyName
            + "|"
            + model.MyColor
            + "|"
            + (model.Presence
               |> Map.toList
               |> List.map (fun (id, info) -> $"{id}:{info.Name}:{info.Color}:{model.Following = Some id}")
               |> String.concat ",")

        if signature <> lastPresenceSignature then
            lastPresenceSignature <- signature
            presenceBar?innerHTML <- ""

            let selfDiv: obj = (Browser.Dom.document :> obj)?createElement ("div")
            selfDiv?className <- "avatar self"
            selfDiv?style?background <- model.MyColor
            selfDiv?title <- $"{model.MyName} (du)"

            selfDiv?textContent <-
                (if model.MyName.Length > 0 then
                     string (System.Char.ToUpper model.MyName.[0])
                 else
                     "?")

            presenceBar?appendChild (selfDiv) |> ignore

            for kv in model.Presence do
                let info = kv.Value
                let isFollowing = model.Following = Some info.ClientId
                let div: obj = (Browser.Dom.document :> obj)?createElement ("div")
                div?className <- "avatar" + (if isFollowing then " following" else "")
                div?style?background <- info.Color

                div?title <-
                    info.Name
                    + (if isFollowing then
                           " (Folgen aktiv - Klick zum Beenden)"
                       else
                           " (Klick zum Folgen)")

                div?textContent <- info.Initial
                div?onclick <- fun (_: obj) -> dispatch (ToggleFollow info.ClientId)
                presenceBar?appendChild (div) |> ignore

    let renderDebug (model: Model) =
        match List.tryHead model.DebugLog with
        | Some entry when Some entry <> lastRenderedHead ->
            lastRenderedHead <- Some entry

            let dirClass =
                match entry.Direction with
                | In -> "dir-in"
                | Out -> "dir-out"
                | Info -> "dir-info"

            let arrow =
                match entry.Direction with
                | In -> "&larr;"
                | Out -> "&rarr;"
                | Info -> "&middot;"

            let row: obj = (Browser.Dom.document :> obj)?createElement ("div")
            row?className <- $"debug-entry {dirClass}"

            row?innerHTML <-
                $"""<span class="t">{entry.Time.ToString("HH:mm:ss.fff")}</span><span class="dir">{arrow}</span><span class="kind">{entry.Kind}</span><span class="detail">{escapeHtml entry.Detail}</span>"""

            // Prepend the one new row instead of rebuilding the whole panel, then trim
            // any overflow off the bottom to match the model's own cap on log length.
            debugLog?prepend (row)

            let targetCount = List.length model.DebugLog
            let mutable childCount: float = debugLog?childElementCount

            while childCount > float targetCount do
                debugLog?removeChild (debugLog?lastElementChild) |> ignore
                childCount <- debugLog?childElementCount
        | _ -> ()

    let syncEditOverlay (model: Model) =
        match model.EditingNoteId with
        | None ->
            editOverlay?style?display <- "none"
            currentEditId <- None
            pendingSelfText <- None
        | Some id ->
            match Map.tryFind id model.Notes with
            | None -> ()
            | Some note ->
                let topLeft = worldToScreen model.Camera { X = note.X; Y = note.Y }
                let w = note.W * model.Camera.Zoom
                let h = note.H * model.Camera.Zoom

                editOverlay?style?display <- "block"
                editOverlay?style?left <- string topLeft.X + "px"
                editOverlay?style?top <- string topLeft.Y + "px"
                editOverlay?style?width <- string w + "px"
                editOverlay?style?height <- string h + "px"
                editOverlay?style?background <- NoteColor.toCss note.Color
                editOverlay?style?fontSize <- string (14.0 * model.Camera.Zoom) + "px"

                if currentEditId <> Some id then
                    currentEditId <- Some id
                    pendingSelfText <- None
                    editOverlay?value <- note.Text
                    editOverlay?focus ()
                    let len = note.Text.Length
                    editOverlay?setSelectionRange (len, len)
                else
                    match pendingSelfText with
                    | Some pending when pending <> note.Text ->
                        // We're still waiting for our own last edit to round-trip back
                        // through Yjs (model.Notes lags a keystroke or two behind what's
                        // already in the textarea). Patching against this stale text would
                        // fight the user's own typing and knock the caret out of place -
                        // so leave the textarea alone until our edit catches up.
                        ()
                    | Some _ ->
                        // Our own edit has caught up - textarea and model agree, nothing to patch.
                        pendingSelfText <- None
                    | None ->
                        // No self-edit in flight, so any difference is a genuine remote
                        // change - merge it in while preserving the caret as best we can.
                        patchTextareaIfChanged editOverlay note.Text

    /// Who (if anyone) currently has `field` of `noteId` focused, per the awareness channel.
    let editorNameFor (model: Model) (noteId: string) (field: string) : string option =
        model.Presence
        |> Map.toList
        |> List.tryPick (fun (_, info) ->
            match info.Editing with
            | Some(nid, f) when nid = noteId && f = field -> Some info.Name
            | _ -> None)

    let setBadge (badgeEl: obj) (nameOpt: string option) =
        match nameOpt with
        | Some name ->
            badgeEl?style?display <- "inline-block"
            badgeEl?textContent <- $"{name} tippt…"
        | None -> badgeEl?style?display <- "none"

    let syncNotePanel (model: Model) =
        match model.SelectedNoteId with
        | None ->
            notePanel?style?display <- "none"
            currentPanelNoteId <- None
            pendingSelfTitle <- None
            pendingSelfDescription <- None
        | Some id ->
            match Map.tryFind id model.Notes with
            | None ->
                notePanel?style?display <- "none"
                currentPanelNoteId <- None
            | Some note ->
                notePanel?style?display <- "block"

                if currentPanelNoteId <> Some id then
                    currentPanelNoteId <- Some id
                    pendingSelfTitle <- None
                    pendingSelfDescription <- None
                    noteTitleInput?value <- note.Title
                    noteDescTextarea?value <- note.Description
                else
                    // Same self-edit-in-flight protection as the main body overlay above -
                    // see docs/03-fallstricke.md #1 for why this matters.
                    (match pendingSelfTitle with
                     | Some pending when pending <> note.Title -> ()
                     | Some _ -> pendingSelfTitle <- None
                     | None -> patchTextareaIfChanged noteTitleInput note.Title)

                    (match pendingSelfDescription with
                     | Some pending when pending <> note.Description -> ()
                     | Some _ -> pendingSelfDescription <- None
                     | None -> patchTextareaIfChanged noteDescTextarea note.Description)

                setBadge titleBadge (editorNameFor model id "title")
                setBadge descBadge (editorNameFor model id "description")

    // -- canvas painting, coalesced to the display's refresh rate -----------------
    //
    // Model updates can arrive far faster than the screen can show them (every
    // mousemove, every remote cursor ping from every other user...). Painting
    // synchronously on each one is wasted work - the browser can't display more
    // than one frame per refresh anyway - so instead we just remember the latest
    // model and paint it (at most) once per requestAnimationFrame. This never makes
    // anything feel slower than native (60/120/144Hz is still what you get), it just
    // stops burning CPU repainting frames nobody ever sees.

    let mutable pendingPaintModel: Model option = None
    let mutable paintScheduled = false

    let paintCanvas () =
        paintScheduled <- false

        match pendingPaintModel with
        | None -> ()
        | Some model ->
            pendingPaintModel <- None

            let hoveredNoteId =
                match model.LocalCursorWorld with
                | Some p ->
                    match Canvas.hitTest model p with
                    | Canvas.HitNote id
                    | Canvas.HitDeleteButton id -> Some id
                    | Canvas.HitNothing -> None
                | None -> None

            canvas?style?cursor <-
                (match model.Drag with
                 | DraggingNote _
                 | PanningCamera _ -> "grabbing"
                 | NotDragging ->
                     match hoveredNoteId with
                     | Some _ -> "grab"
                     | None -> "default")

            Canvas.render (canvas :?> Browser.Types.HTMLCanvasElement) model hoveredNoteId

    let requestPaint (model: Model) =
        pendingPaintModel <- Some model

        if not paintScheduled then
            paintScheduled <- true
            (Browser.Dom.window :> obj)?requestAnimationFrame (fun (_: obj) -> paintCanvas ())

    // -- the actual per-model render --------------------------------------------

    let renderModel (model: Model) =
        connStatus?className <- "conn-status" + (if model.Connected then " online" else "")
        connText?textContent <- (if model.Connected then "verbunden" else "verbinde…")

        let activeEl: obj = (Browser.Dom.document :> obj)?activeElement

        if not (obj.ReferenceEquals(activeEl, nameInput)) then
            nameInput?value <- model.MyName

        // Cheap enough to just query fresh every render rather than mirroring into Model -
        // canUndo/canRedo are always in sync with the live document this way.
        undoBtn?disabled <- not (Client.Doc.canUndo ())
        redoBtn?disabled <- not (Client.Doc.canRedo ())

        if model.DebugOpen then
            debugPanel?classList?add ("open")
            debugToggle?classList?add ("active")
        else
            debugPanel?classList?remove ("open")
            debugToggle?classList?remove ("active")

        renderPresence model
        renderDebug model
        syncEditOverlay model
        syncNotePanel model
        requestPaint model

    renderModel

let mutable private mounted = false
let mutable private cachedRender: Model -> unit = ignore

/// The Elmish `view` function. Elmish hands us the *same* dispatch closure on every
/// call, so it's safe to build the DOM + wire events only the first time and just
/// re-render on every call after that.
let view (model: Model) (dispatch: Msg -> unit) : unit =
    if not mounted then
        mounted <- true
        cachedRender <- mountShell dispatch

    cachedRender model
