/// Entry point. Picks a display name/color for this session, then hands everything
/// else to Elmish. All external wiring (the shared Y.Doc, the WebSocket, the presence
/// heartbeat) happens lazily on Elmish's first `view` call - see View.mountShell.
module Client.Program

open Fable.Core
open Fable.Core.JsInterop
open Elmish
open Client.Types
open Client.State

let private palette =
    [| "#e63946"; "#2a9d8f"; "#e9c46a"; "#457b9d"; "#f4a261"; "#6d597a"; "#3a86ff"; "#ff6392" |]

let private randomColor () =
    palette.[int (JS.Math.random () * float palette.Length)]

// window.prompt() isn't available/reliable in every host (embedded webviews, some
// automated browsers) so the display name is generated up front and editable live
// via the #my-name input in the toolbar (see View.fs) instead of a blocking dialog.
let private randomName () =
    let adjectives = [| "Fox"; "Owl"; "Lynx"; "Wren"; "Hare"; "Puma"; "Kite"; "Newt" |]
    adjectives.[int (JS.Math.random () * float adjectives.Length)] + string (int (JS.Math.random () * 90.0) + 10)

[<EntryPoint>]
let main _ =
    let cfg: StartupConfig =
        { Name = randomName ()
          Color = randomColor ()
          ClientId = Client.Doc.myClientId }

    // A small, deliberate escape hatch for the demo: open devtools and call
    // `__collab.snapshot()` to inspect the live shared document at any time,
    // independent of whatever the debug panel currently has scrolled into view.
    (Browser.Dom.window :> obj)?__collab <-
        createObj
            [ "clientId" ==> Client.Doc.myClientId
              "snapshot" ==> fun () -> Client.Doc.snapshot () |> fst |> List.toArray |> box ]

    Program.mkProgram (State.init cfg) State.update View.view |> Program.run

    0
