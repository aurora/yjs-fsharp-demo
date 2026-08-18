/// Encoding/decoding for the small JSON "who's online, where's your cursor" protocol
/// that rides alongside the binary Yjs updates on the same WebSocket (see
/// server/Rooms.fs and Doc.fs). Kept as plain, readable JSON on purpose - unlike the
/// Yjs traffic, this is meant to be easy to eyeball in the debug panel/console.
module Client.Awareness

open Fable.Core
open Fable.Core.JsInterop
open Client.Types

type Incoming =
    | Presence of ClientId * name: string * color: string * cursor: Vec2 option * editing: EditingRef option
    | Bye of ClientId

let encodePresence
    (id: ClientId)
    (name: string)
    (color: string)
    (cursor: Vec2 option)
    (editing: EditingRef option)
    : string =
    let cursorObj: obj =
        match cursor with
        | Some c -> createObj [ "x" ==> c.X; "y" ==> c.Y ]
        | None -> null

    let editingObj: obj =
        match editing with
        | Some(noteId, field) -> createObj [ "noteId" ==> noteId; "field" ==> field ]
        | None -> null

    createObj
        [ "type" ==> "presence"
          "id" ==> id
          "name" ==> name
          "color" ==> color
          "cursor" ==> cursorObj
          "editing" ==> editingObj ]
    |> JS.JSON.stringify

let tryDecode (json: string) : Incoming option =
    try
        let o = JS.JSON.parse json
        let kind: string = o?``type``

        match kind with
        | "presence" ->
            let id: ClientId = o?id
            let name: string = o?name
            let color: string = o?color
            let cursorRaw: obj = o?cursor

            let cursor =
                if isNull cursorRaw then
                    None
                else
                    Some { X = cursorRaw?x; Y = cursorRaw?y }

            let editingRaw: obj = o?editing

            let editing =
                if isNull editingRaw then
                    None
                else
                    Some(editingRaw?noteId, editingRaw?field)

            Some(Presence(id, name, color, cursor, editing))
        | "bye" ->
            let id: ClientId = o?id
            Some(Bye id)
        | _ -> None
    with _ ->
        None
