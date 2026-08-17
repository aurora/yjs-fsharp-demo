/// Everything that touches the <canvas> 2D drawing surface: painting the board, and
/// hit-testing screen/world points against sticky notes.
///
/// The 2D context is treated as a dynamic JS object (via Fable.Core.JsInterop's `?`
/// operator) rather than through a strictly-typed binding - every call below is a
/// direct 1:1 mapping onto the real HTML Canvas API, which keeps this file simple
/// and immune to binding-shape mismatches.
module Client.Canvas

open Fable.Core.JsInterop
open Client.Types
open Client.Camera

type Ctx = obj

let private closeEnough (deleteHotspotR: float) (p: Vec2) (center: Vec2) =
    let dx = p.X - center.X
    let dy = p.Y - center.Y
    sqrt (dx * dx + dy * dy) <= deleteHotspotR

/// Greedy word-wrap (also honours explicit newlines) so sticky-note text fits its box.
let private wrapText (ctx: Ctx) (text: string) (maxWidth: float) : string list =
    text.Split('\n')
    |> Array.toList
    |> List.collect (fun paragraph ->
        if paragraph = "" then
            [ "" ]
        else
            let words = paragraph.Split(' ') |> Array.toList

            let lines, current =
                ((([]: string list), ""), words)
                ||> List.fold (fun (lines, current) word ->
                    let candidate = if current = "" then word else current + " " + word
                    let width: float = ctx?measureText(candidate)?width

                    if width > maxWidth && current <> "" then
                        (current :: lines, word)
                    else
                        (lines, candidate))

            List.rev (current :: lines))

let private roundRect (ctx: Ctx) (x: float) (y: float) (w: float) (h: float) (r: float) =
    ctx?beginPath ()
    ctx?moveTo(x + r, y)
    ctx?arcTo(x + w, y, x + w, y + h, r)
    ctx?arcTo(x + w, y + h, x, y + h, r)
    ctx?arcTo(x, y + h, x, y, r)
    ctx?arcTo(x, y, x + w, y, r)
    ctx?closePath ()

let private drawGrid (ctx: Ctx) (cam: Camera) (vw: float) (vh: float) =
    let spacing = 50.0
    ctx?save ()
    ctx?strokeStyle <- "#e7e5e0"
    ctx?lineWidth <- 1.0
    let topLeft = screenToWorld cam { X = 0.0; Y = 0.0 }
    let bottomRight = screenToWorld cam { X = vw; Y = vh }

    let mutable x = floor (topLeft.X / spacing) * spacing

    while x < bottomRight.X do
        let sx = (worldToScreen cam { X = x; Y = 0.0 }).X
        ctx?beginPath ()
        ctx?moveTo(sx, 0.0)
        ctx?lineTo(sx, vh)
        ctx?stroke ()
        x <- x + spacing

    let mutable y = floor (topLeft.Y / spacing) * spacing

    while y < bottomRight.Y do
        let sy = (worldToScreen cam { X = 0.0; Y = y }).Y
        ctx?beginPath ()
        ctx?moveTo(0.0, sy)
        ctx?lineTo(vw, sy)
        ctx?stroke ()
        y <- y + spacing

    ctx?restore ()

let private drawNote (ctx: Ctx) (cam: Camera) (note: NoteSnapshot) (isEditing: bool) (isHovered: bool) =
    let topLeft = worldToScreen cam { X = note.X; Y = note.Y }
    let w = note.W * cam.Zoom
    let h = note.H * cam.Zoom

    ctx?save ()
    ctx?shadowColor <- "rgba(0,0,0,0.25)"
    ctx?shadowBlur <- (if isHovered then 14.0 else 8.0)
    ctx?shadowOffsetY <- 3.0
    ctx?fillStyle <- NoteColor.toCss note.Color
    roundRect ctx topLeft.X topLeft.Y w h (6.0 * cam.Zoom)
    ctx?fill ()
    ctx?restore ()

    ctx?save ()
    ctx?lineWidth <- (if isEditing then 3.0 else 1.0)
    ctx?strokeStyle <- (if isEditing then "#2563eb" else NoteColor.toShadow note.Color)
    roundRect ctx topLeft.X topLeft.Y w h (6.0 * cam.Zoom)
    ctx?stroke ()
    ctx?restore ()

    // Text preview is skipped while the note is being edited - the live HTML
    // <textarea> overlay sits exactly on top of it in that state (see View.fs).
    if not isEditing && cam.Zoom > 0.35 then
        ctx?save ()
        ctx?fillStyle <- "#1f2937"
        let fontSize = 14.0 * cam.Zoom
        ctx?font <- $"{fontSize}px 'Segoe UI', sans-serif"
        ctx?textBaseline <- "top"
        let pad = 10.0 * cam.Zoom
        let lines = wrapText ctx note.Text (w - 2.0 * pad)
        let lineHeight = fontSize * 1.3
        let maxLines = max 1 (int ((h - 2.0 * pad) / lineHeight))

        lines
        |> List.truncate maxLines
        |> List.iteri (fun i line -> ctx?fillText (line, topLeft.X + pad, topLeft.Y + pad + float i * lineHeight))

        ctx?restore ()

    // Small delete "x" in the corner, only when hovered - keeps the board uncluttered.
    if isHovered && not isEditing then
        let cx = topLeft.X + w - 12.0 * cam.Zoom
        let cy = topLeft.Y + 12.0 * cam.Zoom
        ctx?save ()
        ctx?fillStyle <- "rgba(0,0,0,0.35)"
        ctx?beginPath ()
        ctx?arc(cx, cy, 9.0 * cam.Zoom, 0.0, 6.2832)
        ctx?fill ()
        ctx?strokeStyle <- "white"
        ctx?lineWidth <- 1.6
        let r = 3.5 * cam.Zoom
        ctx?beginPath ()
        ctx?moveTo(cx - r, cy - r)
        ctx?lineTo(cx + r, cy + r)
        ctx?moveTo(cx + r, cy - r)
        ctx?lineTo(cx - r, cy + r)
        ctx?stroke ()
        ctx?restore ()

let private drawCursor (ctx: Ctx) (cam: Camera) (p: Client.Types.Vec2) (color: string) (label: string) (highlighted: bool) =
    let s = worldToScreen cam p
    ctx?save ()
    ctx?translate(s.X, s.Y)

    ctx?beginPath ()
    ctx?moveTo(0.0, 0.0)
    ctx?lineTo(0.0, 16.0)
    ctx?lineTo(4.5, 12.5)
    ctx?lineTo(8.0, 19.0)
    ctx?lineTo(10.5, 17.7)
    ctx?lineTo(7.2, 11.3)
    ctx?lineTo(12.5, 11.0)
    ctx?closePath ()
    ctx?fillStyle <- color
    ctx?strokeStyle <- "white"
    ctx?lineWidth <- 1.5
    ctx?fill ()
    ctx?stroke ()

    if highlighted then
        ctx?beginPath ()
        ctx?arc(0.0, 0.0, 20.0, 0.0, 6.2832)
        ctx?strokeStyle <- color
        ctx?lineWidth <- 2.0
        ctx?setLineDash ([| 4; 3 |])
        ctx?stroke ()
        ctx?setLineDash ([||])

    ctx?font <- "12px 'Segoe UI', sans-serif"
    let textWidth: float = ctx?measureText(label)?width
    ctx?fillStyle <- color
    roundRect ctx 14.0 14.0 (textWidth + 12.0) 20.0 4.0
    ctx?fill ()
    ctx?fillStyle <- "white"
    ctx?textBaseline <- "middle"
    ctx?fillText (label, 20.0, 24.0)

    ctx?restore ()

/// Full redraw. Called imperatively from View.fs after every model update - the
/// canvas is not part of Elmish's diffing, we own it directly.
let render
    (canvas: Browser.Types.HTMLCanvasElement)
    (model: Model)
    (hoveredNoteId: string option)
    =
    let ctx: Ctx = (canvas :> obj)?getContext ("2d")
    let vw = model.ViewportW
    let vh = model.ViewportH

    ctx?fillStyle <- "#faf9f6"
    ctx?fillRect(0.0, 0.0, vw, vh)

    drawGrid ctx model.Camera vw vh

    for id in model.NoteOrder do
        match Map.tryFind id model.Notes with
        | Some note ->
            let isEditing = model.EditingNoteId = Some id
            let isHovered = hoveredNoteId = Some id
            drawNote ctx model.Camera note isEditing isHovered
        | None -> ()

    for kv in model.Presence do
        match kv.Value.Cursor with
        | Some p ->
            let highlighted = model.Following = Some kv.Key
            drawCursor ctx model.Camera p kv.Value.Color kv.Value.Name highlighted
        | None -> ()

// -- hit testing (pure geometry, no ctx needed) ----------------------------------

type Hit =
    | HitNote of id: string
    | HitDeleteButton of id: string
    | HitNothing

/// Topmost note (i.e. last in NoteOrder) whose rect contains the world point.
let hitTest (model: Model) (worldPoint: Vec2) : Hit =
    let deleteHotspotWorld = 9.0 / model.Camera.Zoom

    model.NoteOrder
    |> List.rev
    |> List.tryPick (fun id ->
        match Map.tryFind id model.Notes with
        | Some note ->
            let deleteCenter = { X = note.X + note.W - 12.0; Y = note.Y + 12.0 }

            if closeEnough deleteHotspotWorld worldPoint deleteCenter then
                Some(HitDeleteButton id)
            elif
                worldPoint.X >= note.X
                && worldPoint.X <= note.X + note.W
                && worldPoint.Y >= note.Y
                && worldPoint.Y <= note.Y + note.H
            then
                Some(HitNote id)
            else
                None
        | None -> None)
    |> Option.defaultValue HitNothing
