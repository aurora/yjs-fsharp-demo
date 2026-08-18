
import { Union, Record } from "./fable_modules/fable-library-js.5.13.0/Types.js";
import { int32_type, bool_type, list_type, class_type, tuple_type, option_type, string_type, union_type, record_type, float64_type } from "./fable_modules/fable-library-js.5.13.0/Reflection.js";
import { ofArray } from "./fable_modules/fable-library-js.5.13.0/List.js";

export class Vec2 extends Record {
    constructor(X, Y) {
        super();
        this.X = X;
        this.Y = Y;
    }
}

export function Vec2_$reflection() {
    return record_type("Client.Types.Vec2", [], Vec2, () => [["X", float64_type], ["Y", float64_type]]);
}

export class NoteColor extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Yellow", "Pink", "Blue", "Green"];
    }
    static Yellow = new NoteColor(0, []);
    static Pink = new NoteColor(1, []);
    static Blue = new NoteColor(2, []);
    static Green = new NoteColor(3, []);
}

export function NoteColor_$reflection() {
    return union_type("Client.Types.NoteColor", [], NoteColor, () => [[], [], [], []]);
}

export const NoteColorModule_all = ofArray([NoteColor.Yellow, NoteColor.Pink, NoteColor.Blue, NoteColor.Green]);

export function NoteColorModule_toCss(_arg) {
    switch (_arg.tag) {
        case 1:
            return "#ff9fc7";
        case 2:
            return "#8ecbff";
        case 3:
            return "#a8e890";
        default:
            return "#fdec6b";
    }
}

export function NoteColorModule_toShadow(_arg) {
    switch (_arg.tag) {
        case 1:
            return "#d66f9c";
        case 2:
            return "#5a9bd6";
        case 3:
            return "#6fb857";
        default:
            return "#d8c73a";
    }
}

export function NoteColorModule_toStorage(_arg) {
    switch (_arg.tag) {
        case 1:
            return "pink";
        case 2:
            return "blue";
        case 3:
            return "green";
        default:
            return "yellow";
    }
}

export function NoteColorModule_ofStorage(_arg) {
    switch (_arg) {
        case "pink":
            return NoteColor.Pink;
        case "blue":
            return NoteColor.Blue;
        case "green":
            return NoteColor.Green;
        default:
            return NoteColor.Yellow;
    }
}

/**
 * Read-only projection of one sticky note, rebuilt from the shared Y.Doc every time
 * it changes (locally or remotely). The Y.Doc is the single source of truth; this
 * record is just a cheap-to-render cache of it.
 */
export class NoteSnapshot extends Record {
    constructor(Id, X, Y, W, H, Color, Text$, Title, Description) {
        super();
        this.Id = Id;
        this.X = X;
        this.Y = Y;
        this.W = W;
        this.H = H;
        this.Color = Color;
        this.Text = Text$;
        this.Title = Title;
        this.Description = Description;
    }
}

export function NoteSnapshot_$reflection() {
    return record_type("Client.Types.NoteSnapshot", [], NoteSnapshot, () => [["Id", string_type], ["X", float64_type], ["Y", float64_type], ["W", float64_type], ["H", float64_type], ["Color", NoteColor_$reflection()], ["Text", string_type], ["Title", string_type], ["Description", string_type]]);
}

export class PresenceInfo extends Record {
    constructor(ClientId, Name, Initial, Color, Cursor, Editing, LastSeen) {
        super();
        this.ClientId = ClientId;
        this.Name = Name;
        this.Initial = Initial;
        this.Color = Color;
        this.Cursor = Cursor;
        this.Editing = Editing;
        this.LastSeen = LastSeen;
    }
}

export function PresenceInfo_$reflection() {
    return record_type("Client.Types.PresenceInfo", [], PresenceInfo, () => [["ClientId", float64_type], ["Name", string_type], ["Initial", string_type], ["Color", string_type], ["Cursor", option_type(Vec2_$reflection())], ["Editing", option_type(tuple_type(string_type, string_type))], ["LastSeen", class_type("System.DateTime")]]);
}

export class Camera extends Record {
    constructor(X, Y, Zoom) {
        super();
        this.X = X;
        this.Y = Y;
        this.Zoom = Zoom;
    }
}

export function Camera_$reflection() {
    return record_type("Client.Types.Camera", [], Camera, () => [["X", float64_type], ["Y", float64_type], ["Zoom", float64_type]]);
}

export class DebugDirection extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["In", "Out", "Info"];
    }
    static In = new DebugDirection(0, []);
    static Out = new DebugDirection(1, []);
    static Info = new DebugDirection(2, []);
}

export function DebugDirection_$reflection() {
    return union_type("Client.Types.DebugDirection", [], DebugDirection, () => [[], [], []]);
}

export class DebugEntry extends Record {
    constructor(Time, Direction, Kind, Detail) {
        super();
        this.Time = Time;
        this.Direction = Direction;
        this.Kind = Kind;
        this.Detail = Detail;
    }
}

export function DebugEntry_$reflection() {
    return record_type("Client.Types.DebugEntry", [], DebugEntry, () => [["Time", class_type("System.DateTime")], ["Direction", DebugDirection_$reflection()], ["Kind", string_type], ["Detail", string_type]]);
}

export class DragState extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["NotDragging", "DraggingNote", "PanningCamera"];
    }
    static NotDragging = new DragState(0, []);
}

export function DragState_$reflection() {
    return union_type("Client.Types.DragState", [], DragState, () => [[], [["id", string_type], ["grabDx", float64_type], ["grabDy", float64_type]], [["startScreen", Vec2_$reflection()], ["startCamera", Vec2_$reflection()]]]);
}

export class Model extends Record {
    constructor(Me, MyName, MyColor, Notes, NoteOrder, Presence, Camera, ViewportW, ViewportH, LocalCursorWorld, LastSentCursor, LastHeartbeatAt, LastDragCommitAt, Drag, EditingNoteId, SelectedNoteId, MyEditingField, Following, Connected, DebugLog, DebugOpen) {
        super();
        this.Me = Me;
        this.MyName = MyName;
        this.MyColor = MyColor;
        this.Notes = Notes;
        this.NoteOrder = NoteOrder;
        this.Presence = Presence;
        this.Camera = Camera;
        this.ViewportW = ViewportW;
        this.ViewportH = ViewportH;
        this.LocalCursorWorld = LocalCursorWorld;
        this.LastSentCursor = LastSentCursor;
        this.LastHeartbeatAt = LastHeartbeatAt;
        this.LastDragCommitAt = LastDragCommitAt;
        this.Drag = Drag;
        this.EditingNoteId = EditingNoteId;
        this.SelectedNoteId = SelectedNoteId;
        this.MyEditingField = MyEditingField;
        this.Following = Following;
        this.Connected = Connected;
        this.DebugLog = DebugLog;
        this.DebugOpen = DebugOpen;
    }
}

export function Model_$reflection() {
    return record_type("Client.Types.Model", [], Model, () => [["Me", option_type(float64_type)], ["MyName", string_type], ["MyColor", string_type], ["Notes", class_type("Microsoft.FSharp.Collections.FSharpMap`2", [string_type, NoteSnapshot_$reflection()])], ["NoteOrder", list_type(string_type)], ["Presence", class_type("Microsoft.FSharp.Collections.FSharpMap`2", [float64_type, PresenceInfo_$reflection()])], ["Camera", Camera_$reflection()], ["ViewportW", float64_type], ["ViewportH", float64_type], ["LocalCursorWorld", option_type(Vec2_$reflection())], ["LastSentCursor", option_type(Vec2_$reflection())], ["LastHeartbeatAt", class_type("System.DateTime")], ["LastDragCommitAt", class_type("System.DateTime")], ["Drag", DragState_$reflection()], ["EditingNoteId", option_type(string_type)], ["SelectedNoteId", option_type(string_type)], ["MyEditingField", option_type(tuple_type(string_type, string_type))], ["Following", option_type(float64_type)], ["Connected", bool_type], ["DebugLog", list_type(DebugEntry_$reflection())], ["DebugOpen", bool_type]]);
}

export class Msg extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["SocketOpened", "SocketClosed", "AwarenessReceived", "DocChanged", "MouseDown", "MouseMove", "MouseUp", "DoubleClick", "Wheel", "WindowResized", "AddNote", "DeleteNote", "StartEditNote", "EditNoteText", "StopEditNote", "ToggleFollow", "ToggleDebugPanel", "HeartbeatTick", "LogDebug", "RenameSelf", "Undo", "Redo", "SelectNote", "SetNoteTitle", "EditNoteDescription", "SetEditingField"];
    }
    static SocketOpened = new Msg(0, []);
    static SocketClosed = new Msg(1, []);
    static MouseUp = new Msg(6, []);
    static StopEditNote = new Msg(14, []);
    static ToggleDebugPanel = new Msg(16, []);
    static HeartbeatTick = new Msg(17, []);
    static Undo = new Msg(20, []);
    static Redo = new Msg(21, []);
}

export function Msg_$reflection() {
    return union_type("Client.Types.Msg", [], Msg, () => [[], [], [["Item", string_type]], [["Item1", list_type(NoteSnapshot_$reflection())], ["Item2", list_type(string_type)]], [["Item1", Vec2_$reflection()], ["button", int32_type]], [["Item", Vec2_$reflection()]], [], [["Item", Vec2_$reflection()]], [["Item1", Vec2_$reflection()], ["deltaY", float64_type]], [["Item1", float64_type], ["Item2", float64_type]], [["Item", NoteColor_$reflection()]], [["Item", string_type]], [["Item", string_type]], [["Item1", string_type], ["Item2", string_type]], [], [["Item", float64_type]], [], [], [["Item1", DebugDirection_$reflection()], ["kind", string_type], ["detail", string_type]], [["Item", string_type]], [], [], [["Item", option_type(string_type)]], [["Item1", string_type], ["Item2", string_type]], [["Item1", string_type], ["Item2", string_type]], [["Item", option_type(tuple_type(string_type, string_type))]]]);
}

