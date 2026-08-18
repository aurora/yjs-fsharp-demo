/// Thin, deliberately un-fancy bindings onto the vendored `yjs.mjs`.
///
/// This module is the ONLY place in the whole app that touches Yjs's actual JS API.
/// Everything above it (Doc.fs and up) only ever sees F# types. Keeping the surface
/// this small is on purpose: it's meant to be the file you point at during the demo
/// and say "this is the entire integration surface with the CRDT library".
///
/// Note on byte[]: Fable compiles .NET `byte[]` to a JS `Uint8Array` at runtime, which
/// is exactly what Yjs's update API expects/produces - so no manual conversion is needed
/// anywhere in this file.
module Client.Interop.Yjs

open Fable.Core
open Fable.Core.JsInterop

// Fable resolves relative import paths against this *source* file's own location
// (client/Interop/Yjs.fs) and re-relativizes them for wherever it writes the
// compiled JS - NOT against the output path itself. yjs.mjs lives in
// server/wwwroot/lib/yjs.mjs, hence "../../server/wwwroot/lib/yjs.mjs" from here.
[<ImportAll("../../server/wwwroot/lib/yjs.mjs")>]
let private Y: obj = jsNative

type YDoc =
    interface
    end

type YMap =
    interface
    end

type YText =
    interface
    end

type YArray =
    interface
    end

// -- Document ---------------------------------------------------------------

let createDoc () : YDoc = createNew Y?Doc () :?> YDoc

let getMap (name: string) (doc: YDoc) : YMap = (doc :> obj)?getMap (name)
let getArray (name: string) (doc: YDoc) : YArray = (doc :> obj)?getArray (name)

/// Marker used as the Yjs transaction "origin" for updates applied because they
/// arrived from the server, i.e. NOT a local edit. Filtering on this is what stops
/// us from immediately re-broadcasting other people's changes back to the server.
let remoteOrigin: obj = box "remote-sync"

let transact (doc: YDoc) (f: unit -> unit) : unit =
    (doc :> obj)?transact (System.Func<unit>(f)) |> ignore

/// Registers the outgoing-sync handler: fires once per local change (i.e. change
/// whose transaction origin is NOT `remoteOrigin`) with the raw update bytes Yjs
/// wants broadcast to every other client.
let onLocalUpdate (doc: YDoc) (handler: byte[] -> unit) : unit =
    (doc :> obj)?on (
        "update",
        (fun (update: byte[]) (origin: obj) ->
            if origin <> remoteOrigin then
                handler update)
    )
    |> ignore

let applyRemoteUpdate (doc: YDoc) (update: byte[]) : unit =
    Y?applyUpdate (doc, update, remoteOrigin) |> ignore

// -- Y.Map --------------------------------------------------------------------

let newMap () : YMap = createNew Y?Map () :?> YMap

let mapGetFloat (key: string) (map: YMap) : float = (map :> obj)?get (key)
let mapGetString (key: string) (map: YMap) : string = (map :> obj)?get (key)
let mapGetObj (key: string) (map: YMap) : obj = (map :> obj)?get (key)
let mapSet (key: string) (value: obj) (map: YMap) : unit = (map :> obj)?set (key, value) |> ignore
let mapDelete (key: string) (map: YMap) : unit = (map :> obj)?delete (key) |> ignore
/// Defensive existence check - lets us read fields that were added to the note "shape" after
/// some notes already existed (e.g. while iterating on this prototype in a running session).
let mapHas (key: string) (map: YMap) : bool = (map :> obj)?has (key)

let mapKeys (map: YMap) : string[] =
    JS.Constructors.Array?from ((map :> obj)?keys ())

/// Fires on any change anywhere below `map` - a field on a nested note map, or a
/// keystroke inside one of its Y.Text values. This is what lets a single observer
/// keep the whole board's read-model in sync no matter what changed.
let observeDeep (callback: unit -> unit) (map: YMap) : unit =
    (map :> obj)?observeDeep (fun (_events: obj) (_tx: obj) -> callback ()) |> ignore

// -- Y.Text ---------------------------------------------------------------------

let newText (initial: string) : YText =
    let t = createNew Y?Text () :?> YText
    (t :> obj)?insert (0, initial) |> ignore
    t

let textToString (t: YText) : string = (t :> obj)?toString ()
let textLength (t: YText) : int = (t :> obj)?length
let textInsert (index: int) (content: string) (t: YText) : unit = (t :> obj)?insert (index, content) |> ignore
let textDelete (index: int) (length: int) (t: YText) : unit = (t :> obj)?delete (index, length) |> ignore

// -- Y.Array (used for note z-order / creation order) --------------------------

let arrayPush (item: obj) (arr: YArray) : unit = (arr :> obj)?push ([| item |]) |> ignore

let arrayToList (arr: YArray) : string list =
    (arr :> obj)?toArray () |> unbox<string[]> |> List.ofArray

let arrayRemoveValue (value: string) (arr: YArray) : unit =
    let items = (arr :> obj)?toArray () |> unbox<string[]>
    let idx = Array.tryFindIndex ((=) value) items

    match idx with
    | Some i -> (arr :> obj)?delete (i, 1) |> ignore
    | None -> ()

// -- Y.UndoManager --------------------------------------------------------------

type YUndoManager =
    interface
    end

/// `scope` is one or more shared types whose changes - including nested content below them,
/// e.g. fields inside a Y.Map living inside this one - should be tracked. By default only
/// local changes are tracked (transactions with no explicit origin), which is exactly the
/// complement of `remoteOrigin` above: undo() can therefore never touch another user's change.
let newUndoManager (scope: obj[]) : YUndoManager =
    createNew Y?UndoManager scope :?> YUndoManager

let undo (um: YUndoManager) : unit = (um :> obj)?undo () |> ignore
let redo (um: YUndoManager) : unit = (um :> obj)?redo () |> ignore
let canUndo (um: YUndoManager) : bool = (um :> obj)?canUndo ()
let canRedo (um: YUndoManager) : bool = (um :> obj)?canRedo ()
