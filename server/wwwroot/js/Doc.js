
import { applyRemoteUpdate, onLocalUpdate, observeDeep, redo as redo_1, canRedo as canRedo_1, undo as undo_1, canUndo as canUndo_1, textInsert, textDelete, arrayRemoveValue, mapDelete, arrayPush, newText, mapSet, newMap, transact, mapKeys, textToString, mapGetString, mapGetFloat, mapGetObj, arrayToList, newUndoManager, getArray, getMap, createDoc } from "./Interop/Yjs.js";
import { FSharpRef } from "./fable_modules/fable-library-js.5.13.0/Types.js";
import { stringHash, createAtom } from "./fable_modules/fable-library-js.5.13.0/Util.js";
import { substring, concat, format } from "./fable_modules/fable-library-js.5.13.0/String.js";
import { ofArray, map } from "./fable_modules/fable-library-js.5.13.0/List.js";
import { Msg, DebugDirection, NoteColorModule_toStorage, NoteSnapshot, NoteColorModule_ofStorage } from "./Types.js";
import { contains } from "./fable_modules/fable-library-js.5.13.0/Array.js";
import { newGuid, toString } from "./fable_modules/fable-library-js.5.13.0/Guid.js";
import { min } from "./fable_modules/fable-library-js.5.13.0/Double.js";
import { sendText, connect as connect_1, Handlers, sendBinary } from "./Interop/Ws.js";

const doc = createDoc();

const notesMap = getMap("notes", doc);

const noteOrder = getArray("noteOrder", doc);

const undoManager = newUndoManager([notesMap, noteOrder]);

export const myClientId = doc.clientID;

const connRef = new FSharpRef(undefined);

export let debugSink = createAtom((_arg, _arg_1, _arg_2) => {
});

function log(dir, kind, detail) {
    const arrow = (dir.tag === 1) ? "OUT->" : ((dir.tag === 2) ? "....." : "IN <-");
    console.log(`[collab] ${arrow} ${format("{0,-10}", kind)} ${detail}`);
    debugSink()(dir, kind, detail);
}

/**
 * Called after every local or remote change. This is the only place that reads
 * note data back out of Yjs - everything else in the app works off the resulting
 * plain F# records.
 */
export function snapshot() {
    const order = arrayToList(noteOrder);
    return [map((id) => {
        const noteMap = mapGetObj(id, notesMap);
        const text = mapGetObj("text", noteMap);
        return new NoteSnapshot(id, mapGetFloat("x", noteMap), mapGetFloat("y", noteMap), mapGetFloat("w", noteMap), mapGetFloat("h", noteMap), NoteColorModule_ofStorage(mapGetString("color", noteMap)), textToString(text));
    }, ofArray(mapKeys(notesMap))), order];
}

function tryGetNoteMap(id) {
    if (contains(id, mapKeys(notesMap), {
        Equals: (x, y) => (x === y),
        GetHashCode: (x) => (stringHash(x) | 0),
    })) {
        return mapGetObj(id, notesMap);
    }
    else {
        return undefined;
    }
}

export function addNote(color, x, y) {
    const id = toString(newGuid(), "N");
    transact(doc, () => {
        const noteMap = newMap();
        mapSet("x", x, noteMap);
        mapSet("y", y, noteMap);
        mapSet("w", 190, noteMap);
        mapSet("h", 150, noteMap);
        mapSet("color", NoteColorModule_toStorage(color), noteMap);
        mapSet("text", newText(""), noteMap);
        mapSet(id, noteMap, notesMap);
        arrayPush(id, noteOrder);
    });
    log(DebugDirection.Out, "note-add", concat(substring(id, 0, 6), " color=", NoteColorModule_toStorage(color)));
    return id;
}

export function moveNote(id, x, y) {
    const matchValue = tryGetNoteMap(id);
    if (matchValue == null) {
    }
    else {
        const noteMap = matchValue;
        transact(doc, () => {
            mapSet("x", x, noteMap);
            mapSet("y", y, noteMap);
        });
    }
}

export function deleteNote(id) {
    transact(doc, () => {
        mapDelete(id, notesMap);
        arrayRemoveValue(id, noteOrder);
    });
    log(DebugDirection.Out, "note-delete", substring(id, 0, 6));
}

/**
 * Turns a textarea's `input` event into a minimal Y.Text edit instead of a
 * full clear-and-rewrite, by diffing off the common prefix/suffix of old vs new
 * text. This is what lets two people type in different parts of the same note
 * at the same time without stomping on each other - Y.Text's CRDT merges the two
 * small, disjoint edits cleanly instead of one full-text write clobbering the other.
 */
export function editNoteText(id, oldText, newText$0027) {
    const matchValue = tryGetNoteMap(id);
    if (matchValue == null) {
    }
    else {
        const textNode = mapGetObj("text", matchValue);
        const oldLen = oldText.length | 0;
        const newLen = newText$0027.length | 0;
        const maxCommon = min(oldLen, newLen) | 0;
        let prefix = 0;
        while ((prefix < maxCommon) && (oldText[prefix] === newText$0027[prefix])) {
            prefix = ((prefix + 1) | 0);
        }
        let suffix = 0;
        while ((suffix < (maxCommon - prefix)) && (oldText[(oldLen - 1) - suffix] === newText$0027[(newLen - 1) - suffix])) {
            suffix = ((suffix + 1) | 0);
        }
        const removedLen = ((oldLen - prefix) - suffix) | 0;
        const insertedText = substring(newText$0027, prefix, (newLen - prefix) - suffix);
        transact(doc, () => {
            if (removedLen > 0) {
                textDelete(prefix, removedLen, textNode);
            }
            if (insertedText.length > 0) {
                textInsert(prefix, insertedText, textNode);
            }
        });
        log(DebugDirection.Out, "note-edit", `${substring(id, 0, 6)} -${removedLen}/+${insertedText.length} chars @ ${prefix}`);
    }
}

export function undo() {
    if (canUndo_1(undoManager)) {
        log(DebugDirection.Out, "undo", "reverting last local change");
        undo_1(undoManager);
    }
}

export function redo() {
    if (canRedo_1(undoManager)) {
        log(DebugDirection.Out, "redo", "reapplying last undone change");
        redo_1(undoManager);
    }
}

export function canUndo() {
    return canUndo_1(undoManager);
}

export function canRedo() {
    return canRedo_1(undoManager);
}

export function connect(dispatch) {
    observeDeep(() => {
        const patternInput = snapshot();
        dispatch(new Msg(/* DocChanged */ 3, [patternInput[0], patternInput[1]]));
    }, notesMap);
    onLocalUpdate(doc, (bytes) => {
        const matchValue = connRef.contents;
        if (matchValue == null) {
        }
        else {
            const conn = matchValue;
            log(DebugDirection.Out, "y-update", `${bytes.length} bytes`);
            sendBinary(conn, bytes);
        }
    });
    const loc = window.location;
    const url = concat((loc.protocol === "https:") ? "wss" : "ws", "://", loc.host, "/ws?room=default");
    const handlers = new Handlers(() => {
        log(DebugDirection.Info, "socket", "connected");
        dispatch(Msg.SocketOpened);
    }, () => {
        log(DebugDirection.Info, "socket", "disconnected");
        dispatch(Msg.SocketClosed);
    }, (bytes_1) => {
        log(DebugDirection.In, "y-update", `${bytes_1.length} bytes`);
        applyRemoteUpdate(doc, bytes_1);
    }, (text) => {
        log(DebugDirection.In, "awareness", text);
        dispatch(new Msg(/* AwarenessReceived */ 2, [text]));
    });
    connRef.contents = connect_1(url, handlers);
}

export function sendAwareness(json) {
    const matchValue = connRef.contents;
    if (matchValue == null) {
    }
    else {
        const conn = matchValue;
        log(DebugDirection.Out, "awareness", json);
        sendText(conn, json);
    }
}

