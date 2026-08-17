/// All the shared vocabulary of the app in one place: the Model, the Msg union
/// MVU dispatches, and the small value types both are built from.
module Client.Types

open System

/// We deliberately reuse Yjs's own `doc.clientID` (a random int Yjs assigns every
/// Y.Doc) as our application-level user id instead of inventing a second id scheme.
type ClientId = float

type Vec2 = { X: float; Y: float }

type NoteColor =
    | Yellow
    | Pink
    | Blue
    | Green

module NoteColor =
    let all = [ Yellow; Pink; Blue; Green ]

    let toCss =
        function
        | Yellow -> "#fdec6b"
        | Pink -> "#ff9fc7"
        | Blue -> "#8ecbff"
        | Green -> "#a8e890"

    let toShadow =
        function
        | Yellow -> "#d8c73a"
        | Pink -> "#d66f9c"
        | Blue -> "#5a9bd6"
        | Green -> "#6fb857"

    let toStorage =
        function
        | Yellow -> "yellow"
        | Pink -> "pink"
        | Blue -> "blue"
        | Green -> "green"

    let ofStorage =
        function
        | "pink" -> Pink
        | "blue" -> Blue
        | "green" -> Green
        | _ -> Yellow

/// Read-only projection of one sticky note, rebuilt from the shared Y.Doc every time
/// it changes (locally or remotely). The Y.Doc is the single source of truth; this
/// record is just a cheap-to-render cache of it.
type NoteSnapshot =
    { Id: string
      X: float
      Y: float
      W: float
      H: float
      Color: NoteColor
      Text: string }

type PresenceInfo =
    { ClientId: ClientId
      Name: string
      Initial: string
      Color: string
      /// World-space cursor position (canvas coordinates), NOT screen pixels - so
      /// "follow" converges on the same spot regardless of the other user's own
      /// zoom/pan/window size.
      Cursor: Vec2 option
      LastSeen: DateTime }

type Camera = { X: float; Y: float; Zoom: float }

type DebugDirection =
    | In
    | Out
    | Info

type DebugEntry =
    { Time: DateTime
      Direction: DebugDirection
      Kind: string
      Detail: string }

type DragState =
    | NotDragging
    | DraggingNote of id: string * grabDx: float * grabDy: float
    | PanningCamera of startScreen: Vec2 * startCamera: Vec2

type Model =
    { Me: ClientId option
      MyName: string
      MyColor: string
      Notes: Map<string, NoteSnapshot>
      NoteOrder: string list
      Presence: Map<ClientId, PresenceInfo>
      Camera: Camera
      ViewportW: float
      ViewportH: float
      LocalCursorWorld: Vec2 option
      LastSentCursor: Vec2 option
      LastHeartbeatAt: DateTime
      LastDragCommitAt: DateTime
      Drag: DragState
      EditingNoteId: string option
      Following: ClientId option
      Connected: bool
      DebugLog: DebugEntry list
      DebugOpen: bool }

type Msg =
    | SocketOpened
    | SocketClosed
    | AwarenessReceived of string
    | DocChanged of NoteSnapshot list * string list
    | MouseDown of Vec2 * button: int
    | MouseMove of Vec2
    | MouseUp
    | DoubleClick of Vec2
    | Wheel of Vec2 * deltaY: float
    | WindowResized of float * float
    | AddNote of NoteColor
    | DeleteNote of string
    | StartEditNote of string
    | EditNoteText of string * string
    | StopEditNote
    | ToggleFollow of ClientId
    | ToggleDebugPanel
    | HeartbeatTick
    | LogDebug of DebugDirection * kind: string * detail: string
    | RenameSelf of string
