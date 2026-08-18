
import { Record } from "./fable_modules/fable-library-js.5.13.0/Types.js";
import { record_type, float64_type, string_type } from "./fable_modules/fable-library-js.5.13.0/Reflection.js";
import { tryFind, remove, add, containsKey, ofList, empty } from "./fable_modules/fable-library-js.5.13.0/Map.js";
import { equals, comparePrimitives } from "./fable_modules/fable-library-js.5.13.0/Util.js";
import { cons, truncate, map, empty as empty_1 } from "./fable_modules/fable-library-js.5.13.0/List.js";
import { DebugEntry, NoteSnapshot, Vec2, PresenceInfo, Model, DragState, Camera } from "./Types.js";
import { op_Subtraction, now as now_2, minValue } from "./fable_modules/fable-library-js.5.13.0/Date.js";
import { Cmd_none } from "./fable_modules/Fable.Elmish.5.0.2/cmd.fs.js";
import { editNoteDescription, setNoteTitle, redo, undo, editNoteText, addNote, moveNote, deleteNote, sendAwareness } from "./Doc.js";
import { tryDecode, encodePresence } from "./Awareness.js";
import { zoomAt, screenToWorld, centerOn } from "./Camera.js";
import { hitTest } from "./Canvas.js";

export class StartupConfig extends Record {
    constructor(Name, Color, ClientId) {
        super();
        this.Name = Name;
        this.Color = Color;
        this.ClientId = ClientId;
    }
}

export function StartupConfig_$reflection() {
    return record_type("Client.State.StartupConfig", [], StartupConfig, () => [["Name", string_type], ["Color", string_type], ["ClientId", float64_type]]);
}

export function init(cfg, unitVar) {
    return [new Model(cfg.ClientId, cfg.Name, cfg.Color, empty({
        Compare: (x, y) => (comparePrimitives(x, y) | 0),
    }), empty_1(), empty({
        Compare: (x_1, y_1) => (comparePrimitives(x_1, y_1) | 0),
    }), new Camera(0, 0, 1), window.innerWidth, window.innerHeight, undefined, undefined, minValue(), minValue(), DragState.NotDragging, undefined, undefined, undefined, undefined, false, empty_1(), true), Cmd_none()];
}

const maxDebugEntries = 150;

function sendAwarenessNow(model) {
    const matchValue = model.Me;
    if (matchValue == null) {
    }
    else {
        sendAwareness(encodePresence(matchValue, model.MyName, model.MyColor, model.LocalCursorWorld, model.MyEditingField));
    }
}

export function update(msg, model) {
    let matchValue, id_1;
    switch (msg.tag) {
        case 1:
            return [new Model(model.Me, model.MyName, model.MyColor, model.Notes, model.NoteOrder, model.Presence, model.Camera, model.ViewportW, model.ViewportH, model.LocalCursorWorld, model.LastSentCursor, model.LastHeartbeatAt, model.LastDragCommitAt, model.Drag, model.EditingNoteId, model.SelectedNoteId, model.MyEditingField, model.Following, false, model.DebugLog, model.DebugOpen), Cmd_none()];
        case 3: {
            const notesMap = ofList(map((n) => [n.Id, n], msg.fields[0]), {
                Compare: (x, y) => (comparePrimitives(x, y) | 0),
            });
            return [new Model(model.Me, model.MyName, model.MyColor, notesMap, msg.fields[1], model.Presence, model.Camera, model.ViewportW, model.ViewportH, model.LocalCursorWorld, model.LastSentCursor, model.LastHeartbeatAt, model.LastDragCommitAt, model.Drag, (matchValue = model.EditingNoteId, (matchValue != null) ? (!containsKey(matchValue, notesMap) ? ((id_1 = matchValue, undefined)) : matchValue) : matchValue), model.SelectedNoteId, model.MyEditingField, model.Following, model.Connected, model.DebugLog, model.DebugOpen), Cmd_none()];
        }
        case 2: {
            const matchValue_1 = tryDecode(msg.fields[0]);
            let matchResult, color_1, cursor_1, editing_2, id_3, name_1, id_4;
            if (matchValue_1 != null) {
                if (matchValue_1.tag === 1) {
                    matchResult = 1;
                    id_4 = matchValue_1.fields[0];
                }
                else if ((matchValue_1.fields[1], matchValue_1.fields[4], matchValue_1.fields[3], matchValue_1.fields[2], !equals(matchValue_1.fields[0], model.Me))) {
                    matchResult = 0;
                    color_1 = matchValue_1.fields[2];
                    cursor_1 = matchValue_1.fields[3];
                    editing_2 = matchValue_1.fields[4];
                    id_3 = matchValue_1.fields[0];
                    name_1 = matchValue_1.fields[1];
                }
                else {
                    matchResult = 2;
                }
            }
            else {
                matchResult = 2;
            }
            switch (matchResult) {
                case 0: {
                    const info = new PresenceInfo(id_3, name_1, (name_1.length > 0) ? name_1[0].toLocaleUpperCase() : "?", color_1, cursor_1, editing_2, now_2());
                    let newCamera;
                    const matchValue_2 = model.Following;
                    let matchResult_1, c_1, followId_1;
                    if (matchValue_2 != null) {
                        if (cursor_1 != null) {
                            if (matchValue_2 === id_3) {
                                matchResult_1 = 0;
                                c_1 = cursor_1;
                                followId_1 = matchValue_2;
                            }
                            else {
                                matchResult_1 = 1;
                            }
                        }
                        else {
                            matchResult_1 = 1;
                        }
                    }
                    else {
                        matchResult_1 = 1;
                    }
                    switch (matchResult_1) {
                        case 0: {
                            newCamera = centerOn(model.ViewportW, model.ViewportH, c_1, model.Camera);
                            break;
                        }
                        default:
                            newCamera = model.Camera;
                    }
                    return [new Model(model.Me, model.MyName, model.MyColor, model.Notes, model.NoteOrder, add(id_3, info, model.Presence), newCamera, model.ViewportW, model.ViewportH, model.LocalCursorWorld, model.LastSentCursor, model.LastHeartbeatAt, model.LastDragCommitAt, model.Drag, model.EditingNoteId, model.SelectedNoteId, model.MyEditingField, model.Following, model.Connected, model.DebugLog, model.DebugOpen), Cmd_none()];
                }
                case 1: {
                    const following = equals(model.Following, id_4) ? undefined : model.Following;
                    return [new Model(model.Me, model.MyName, model.MyColor, model.Notes, model.NoteOrder, remove(id_4, model.Presence), model.Camera, model.ViewportW, model.ViewportH, model.LocalCursorWorld, model.LastSentCursor, model.LastHeartbeatAt, model.LastDragCommitAt, model.Drag, model.EditingNoteId, model.SelectedNoteId, model.MyEditingField, following, model.Connected, model.DebugLog, model.DebugOpen), Cmd_none()];
                }
                default:
                    return [model, Cmd_none()];
            }
        }
        case 4: {
            const screenPoint = msg.fields[0];
            const worldPoint = screenToWorld(model.Camera, screenPoint);
            if (msg.fields[1] === 0) {
                const matchValue_4 = hitTest(model, worldPoint);
                switch (matchValue_4.tag) {
                    case 0: {
                        const id_6 = matchValue_4.fields[0];
                        const matchValue_5 = tryFind(id_6, model.Notes);
                        if (matchValue_5 == null) {
                            return [model, Cmd_none()];
                        }
                        else {
                            const note = matchValue_5;
                            return [new Model(model.Me, model.MyName, model.MyColor, model.Notes, model.NoteOrder, model.Presence, model.Camera, model.ViewportW, model.ViewportH, model.LocalCursorWorld, model.LastSentCursor, model.LastHeartbeatAt, model.LastDragCommitAt, new DragState(/* DraggingNote */ 1, [id_6, worldPoint.X - note.X, worldPoint.Y - note.Y]), model.EditingNoteId, id_6, model.MyEditingField, model.Following, model.Connected, model.DebugLog, model.DebugOpen), Cmd_none()];
                        }
                    }
                    case 2:
                        return [new Model(model.Me, model.MyName, model.MyColor, model.Notes, model.NoteOrder, model.Presence, model.Camera, model.ViewportW, model.ViewportH, model.LocalCursorWorld, model.LastSentCursor, model.LastHeartbeatAt, model.LastDragCommitAt, new DragState(/* PanningCamera */ 2, [screenPoint, new Vec2(model.Camera.X, model.Camera.Y)]), model.EditingNoteId, undefined, model.MyEditingField, undefined, model.Connected, model.DebugLog, model.DebugOpen), Cmd_none()];
                    default: {
                        deleteNote(matchValue_4.fields[0]);
                        return [model, Cmd_none()];
                    }
                }
            }
            else {
                return [model, Cmd_none()];
            }
        }
        case 5: {
            const screenPoint_1 = msg.fields[0];
            const worldPoint_1 = screenToWorld(model.Camera, screenPoint_1);
            const matchValue_6 = model.Drag;
            switch (matchValue_6.tag) {
                case 2: {
                    const startScreen = matchValue_6.fields[0];
                    const startCam = matchValue_6.fields[1];
                    return [new Model(model.Me, model.MyName, model.MyColor, model.Notes, model.NoteOrder, model.Presence, new Camera(startCam.X - ((screenPoint_1.X - startScreen.X) / model.Camera.Zoom), startCam.Y - ((screenPoint_1.Y - startScreen.Y) / model.Camera.Zoom), model.Camera.Zoom), model.ViewportW, model.ViewportH, worldPoint_1, model.LastSentCursor, model.LastHeartbeatAt, model.LastDragCommitAt, model.Drag, model.EditingNoteId, model.SelectedNoteId, model.MyEditingField, model.Following, model.Connected, model.DebugLog, model.DebugOpen), Cmd_none()];
                }
                case 0:
                    return [new Model(model.Me, model.MyName, model.MyColor, model.Notes, model.NoteOrder, model.Presence, model.Camera, model.ViewportW, model.ViewportH, worldPoint_1, model.LastSentCursor, model.LastHeartbeatAt, model.LastDragCommitAt, model.Drag, model.EditingNoteId, model.SelectedNoteId, model.MyEditingField, model.Following, model.Connected, model.DebugLog, model.DebugOpen), Cmd_none()];
                default: {
                    const id_7 = matchValue_6.fields[0];
                    const newY = worldPoint_1.Y - matchValue_6.fields[2];
                    const newX = worldPoint_1.X - matchValue_6.fields[1];
                    let optimisticNotes;
                    const matchValue_9 = tryFind(id_7, model.Notes);
                    if (matchValue_9 == null) {
                        optimisticNotes = model.Notes;
                    }
                    else {
                        const note_1 = matchValue_9;
                        optimisticNotes = add(id_7, new NoteSnapshot(note_1.Id, newX, newY, note_1.W, note_1.H, note_1.Color, note_1.Text, note_1.Title, note_1.Description), model.Notes);
                    }
                    const now = now_2();
                    if (op_Subtraction(now, model.LastDragCommitAt) > 40) {
                        moveNote(id_7, newX, newY);
                        return [new Model(model.Me, model.MyName, model.MyColor, optimisticNotes, model.NoteOrder, model.Presence, model.Camera, model.ViewportW, model.ViewportH, worldPoint_1, model.LastSentCursor, model.LastHeartbeatAt, now, model.Drag, model.EditingNoteId, model.SelectedNoteId, model.MyEditingField, model.Following, model.Connected, model.DebugLog, model.DebugOpen), Cmd_none()];
                    }
                    else {
                        return [new Model(model.Me, model.MyName, model.MyColor, optimisticNotes, model.NoteOrder, model.Presence, model.Camera, model.ViewportW, model.ViewportH, worldPoint_1, model.LastSentCursor, model.LastHeartbeatAt, model.LastDragCommitAt, model.Drag, model.EditingNoteId, model.SelectedNoteId, model.MyEditingField, model.Following, model.Connected, model.DebugLog, model.DebugOpen), Cmd_none()];
                    }
                }
            }
        }
        case 6: {
            const matchValue_10 = model.Drag;
            if (matchValue_10.tag === 1) {
                const id_8 = matchValue_10.fields[0];
                const matchValue_11 = tryFind(id_8, model.Notes);
                if (matchValue_11 == null) {
                }
                else {
                    const note_2 = matchValue_11;
                    moveNote(id_8, note_2.X, note_2.Y);
                }
            }
            return [new Model(model.Me, model.MyName, model.MyColor, model.Notes, model.NoteOrder, model.Presence, model.Camera, model.ViewportW, model.ViewportH, model.LocalCursorWorld, model.LastSentCursor, model.LastHeartbeatAt, model.LastDragCommitAt, DragState.NotDragging, model.EditingNoteId, model.SelectedNoteId, model.MyEditingField, model.Following, model.Connected, model.DebugLog, model.DebugOpen), Cmd_none()];
        }
        case 7: {
            const matchValue_12 = hitTest(model, screenToWorld(model.Camera, msg.fields[0]));
            let matchResult_2, id_9;
            switch (matchValue_12.tag) {
                case 1: {
                    matchResult_2 = 0;
                    id_9 = matchValue_12.fields[0];
                    break;
                }
                case 2: {
                    matchResult_2 = 1;
                    break;
                }
                default: {
                    matchResult_2 = 0;
                    id_9 = matchValue_12.fields[0];
                }
            }
            switch (matchResult_2) {
                case 0: {
                    const newModel = new Model(model.Me, model.MyName, model.MyColor, model.Notes, model.NoteOrder, model.Presence, model.Camera, model.ViewportW, model.ViewportH, model.LocalCursorWorld, model.LastSentCursor, model.LastHeartbeatAt, model.LastDragCommitAt, model.Drag, id_9, model.SelectedNoteId, [id_9, "text"], model.Following, model.Connected, model.DebugLog, model.DebugOpen);
                    sendAwarenessNow(newModel);
                    return [newModel, Cmd_none()];
                }
                default:
                    return [model, Cmd_none()];
            }
        }
        case 8:
            return [new Model(model.Me, model.MyName, model.MyColor, model.Notes, model.NoteOrder, model.Presence, zoomAt(msg.fields[0], Math.exp(-msg.fields[1] * 0.001), model.Camera), model.ViewportW, model.ViewportH, model.LocalCursorWorld, model.LastSentCursor, model.LastHeartbeatAt, model.LastDragCommitAt, model.Drag, model.EditingNoteId, model.SelectedNoteId, model.MyEditingField, undefined, model.Connected, model.DebugLog, model.DebugOpen), Cmd_none()];
        case 9:
            return [new Model(model.Me, model.MyName, model.MyColor, model.Notes, model.NoteOrder, model.Presence, model.Camera, msg.fields[0], msg.fields[1], model.LocalCursorWorld, model.LastSentCursor, model.LastHeartbeatAt, model.LastDragCommitAt, model.Drag, model.EditingNoteId, model.SelectedNoteId, model.MyEditingField, model.Following, model.Connected, model.DebugLog, model.DebugOpen), Cmd_none()];
        case 10: {
            const center = screenToWorld(model.Camera, new Vec2(model.ViewportW / 2, model.ViewportH / 2));
            const jitter = (Math.random() - 0.5) * 60;
            addNote(msg.fields[0], (center.X - 95) + jitter, (center.Y - 75) + jitter);
            return [model, Cmd_none()];
        }
        case 11: {
            const id_10 = msg.fields[0];
            deleteNote(id_10);
            return [new Model(model.Me, model.MyName, model.MyColor, model.Notes, model.NoteOrder, model.Presence, model.Camera, model.ViewportW, model.ViewportH, model.LocalCursorWorld, model.LastSentCursor, model.LastHeartbeatAt, model.LastDragCommitAt, model.Drag, equals(model.EditingNoteId, id_10) ? undefined : model.EditingNoteId, equals(model.SelectedNoteId, id_10) ? undefined : model.SelectedNoteId, model.MyEditingField, model.Following, model.Connected, model.DebugLog, model.DebugOpen), Cmd_none()];
        }
        case 12: {
            const id_11 = msg.fields[0];
            const newModel_1 = new Model(model.Me, model.MyName, model.MyColor, model.Notes, model.NoteOrder, model.Presence, model.Camera, model.ViewportW, model.ViewportH, model.LocalCursorWorld, model.LastSentCursor, model.LastHeartbeatAt, model.LastDragCommitAt, model.Drag, id_11, model.SelectedNoteId, [id_11, "text"], model.Following, model.Connected, model.DebugLog, model.DebugOpen);
            sendAwarenessNow(newModel_1);
            return [newModel_1, Cmd_none()];
        }
        case 13: {
            const id_12 = msg.fields[0];
            const matchValue_13 = tryFind(id_12, model.Notes);
            if (matchValue_13 == null) {
            }
            else {
                editNoteText(id_12, matchValue_13.Text, msg.fields[1]);
            }
            return [model, Cmd_none()];
        }
        case 14: {
            const newModel_2 = new Model(model.Me, model.MyName, model.MyColor, model.Notes, model.NoteOrder, model.Presence, model.Camera, model.ViewportW, model.ViewportH, model.LocalCursorWorld, model.LastSentCursor, model.LastHeartbeatAt, model.LastDragCommitAt, model.Drag, undefined, model.SelectedNoteId, undefined, model.Following, model.Connected, model.DebugLog, model.DebugOpen);
            sendAwarenessNow(newModel_2);
            return [newModel_2, Cmd_none()];
        }
        case 15: {
            const clientId = msg.fields[0];
            return [new Model(model.Me, model.MyName, model.MyColor, model.Notes, model.NoteOrder, model.Presence, model.Camera, model.ViewportW, model.ViewportH, model.LocalCursorWorld, model.LastSentCursor, model.LastHeartbeatAt, model.LastDragCommitAt, model.Drag, model.EditingNoteId, model.SelectedNoteId, model.MyEditingField, equals(model.Following, clientId) ? undefined : clientId, model.Connected, model.DebugLog, model.DebugOpen), Cmd_none()];
        }
        case 16:
            return [new Model(model.Me, model.MyName, model.MyColor, model.Notes, model.NoteOrder, model.Presence, model.Camera, model.ViewportW, model.ViewportH, model.LocalCursorWorld, model.LastSentCursor, model.LastHeartbeatAt, model.LastDragCommitAt, model.Drag, model.EditingNoteId, model.SelectedNoteId, model.MyEditingField, model.Following, model.Connected, model.DebugLog, !model.DebugOpen), Cmd_none()];
        case 17: {
            const matchValue_14 = model.Me;
            let matchResult_3, me_1;
            if (matchValue_14 != null) {
                if (model.Connected) {
                    matchResult_3 = 0;
                    me_1 = matchValue_14;
                }
                else {
                    matchResult_3 = 1;
                }
            }
            else {
                matchResult_3 = 1;
            }
            switch (matchResult_3) {
                case 0: {
                    const now_1 = now_2();
                    let moved;
                    const matchValue_15 = model.LastSentCursor;
                    const matchValue_16 = model.LocalCursorWorld;
                    let matchResult_4, cur, last;
                    if (matchValue_15 == null) {
                        if (matchValue_16 == null) {
                            matchResult_4 = 2;
                        }
                        else {
                            matchResult_4 = 1;
                        }
                    }
                    else if (matchValue_16 == null) {
                        matchResult_4 = 2;
                    }
                    else {
                        matchResult_4 = 0;
                        cur = matchValue_16;
                        last = matchValue_15;
                    }
                    switch (matchResult_4) {
                        case 0: {
                            moved = ((Math.abs(last.X - cur.X) + Math.abs(last.Y - cur.Y)) > 1);
                            break;
                        }
                        case 1: {
                            moved = true;
                            break;
                        }
                        default:
                            moved = false;
                    }
                    const staleKeepAlive = op_Subtraction(now_1, model.LastHeartbeatAt) > 1000;
                    if (moved ? true : staleKeepAlive) {
                        sendAwareness(encodePresence(me_1, model.MyName, model.MyColor, model.LocalCursorWorld, model.MyEditingField));
                        return [new Model(model.Me, model.MyName, model.MyColor, model.Notes, model.NoteOrder, model.Presence, model.Camera, model.ViewportW, model.ViewportH, model.LocalCursorWorld, model.LocalCursorWorld, now_1, model.LastDragCommitAt, model.Drag, model.EditingNoteId, model.SelectedNoteId, model.MyEditingField, model.Following, model.Connected, model.DebugLog, model.DebugOpen), Cmd_none()];
                    }
                    else {
                        return [model, Cmd_none()];
                    }
                }
                default:
                    return [model, Cmd_none()];
            }
        }
        case 20: {
            undo();
            return [model, Cmd_none()];
        }
        case 21: {
            redo();
            return [model, Cmd_none()];
        }
        case 19: {
            const trimmed = msg.fields[0].trim();
            return [new Model(model.Me, (trimmed === "") ? model.MyName : trimmed, model.MyColor, model.Notes, model.NoteOrder, model.Presence, model.Camera, model.ViewportW, model.ViewportH, model.LocalCursorWorld, model.LastSentCursor, model.LastHeartbeatAt, model.LastDragCommitAt, model.Drag, model.EditingNoteId, model.SelectedNoteId, model.MyEditingField, model.Following, model.Connected, model.DebugLog, model.DebugOpen), Cmd_none()];
        }
        case 18:
            return [new Model(model.Me, model.MyName, model.MyColor, model.Notes, model.NoteOrder, model.Presence, model.Camera, model.ViewportW, model.ViewportH, model.LocalCursorWorld, model.LastSentCursor, model.LastHeartbeatAt, model.LastDragCommitAt, model.Drag, model.EditingNoteId, model.SelectedNoteId, model.MyEditingField, model.Following, model.Connected, truncate(maxDebugEntries, cons(new DebugEntry(now_2(), msg.fields[0], msg.fields[1], msg.fields[2]), model.DebugLog)), model.DebugOpen), Cmd_none()];
        case 22:
            return [new Model(model.Me, model.MyName, model.MyColor, model.Notes, model.NoteOrder, model.Presence, model.Camera, model.ViewportW, model.ViewportH, model.LocalCursorWorld, model.LastSentCursor, model.LastHeartbeatAt, model.LastDragCommitAt, model.Drag, model.EditingNoteId, msg.fields[0], model.MyEditingField, model.Following, model.Connected, model.DebugLog, model.DebugOpen), Cmd_none()];
        case 23: {
            setNoteTitle(msg.fields[0], msg.fields[1]);
            return [model, Cmd_none()];
        }
        case 24: {
            const id_14 = msg.fields[0];
            const matchValue_18 = tryFind(id_14, model.Notes);
            if (matchValue_18 == null) {
            }
            else {
                editNoteDescription(id_14, matchValue_18.Description, msg.fields[1]);
            }
            return [model, Cmd_none()];
        }
        case 25: {
            const newModel_3 = new Model(model.Me, model.MyName, model.MyColor, model.Notes, model.NoteOrder, model.Presence, model.Camera, model.ViewportW, model.ViewportH, model.LocalCursorWorld, model.LastSentCursor, model.LastHeartbeatAt, model.LastDragCommitAt, model.Drag, model.EditingNoteId, model.SelectedNoteId, msg.fields[0], model.Following, model.Connected, model.DebugLog, model.DebugOpen);
            sendAwarenessNow(newModel_3);
            return [newModel_3, Cmd_none()];
        }
        default:
            return [new Model(model.Me, model.MyName, model.MyColor, model.Notes, model.NoteOrder, model.Presence, model.Camera, model.ViewportW, model.ViewportH, model.LocalCursorWorld, model.LastSentCursor, model.LastHeartbeatAt, model.LastDragCommitAt, model.Drag, model.EditingNoteId, model.SelectedNoteId, model.MyEditingField, model.Following, true, model.DebugLog, model.DebugOpen), Cmd_none()];
    }
}

