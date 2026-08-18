
import * as yjs from "../../lib/yjs.mjs";
import { equals } from "../fable_modules/fable-library-js.5.13.0/Util.js";
import { ofArray } from "../fable_modules/fable-library-js.5.13.0/List.js";
import { tryFindIndex } from "../fable_modules/fable-library-js.5.13.0/Array.js";

export function createDoc() {
    return new yjs.Doc();
}

export function getMap(name, doc) {
    return doc.getMap(name);
}

export function getArray(name, doc) {
    return doc.getArray(name);
}

export const remoteOrigin = "remote-sync";

export function transact(doc, f) {
    doc.transact(f);
}

/**
 * Registers the outgoing-sync handler: fires once per local change (i.e. change
 * whose transaction origin is NOT `remoteOrigin`) with the raw update bytes Yjs
 * wants broadcast to every other client.
 */
export function onLocalUpdate(doc, handler) {
    doc.on("update", ((update, origin) => {
        if (!equals(origin, remoteOrigin)) {
            handler(update);
        }
    }));
}

export function applyRemoteUpdate(doc, update) {
    yjs.applyUpdate(doc, update, remoteOrigin);
}

export function newMap() {
    return new yjs.Map();
}

export function mapGetFloat(key, map) {
    return map.get(key);
}

export function mapGetString(key, map) {
    return map.get(key);
}

export function mapGetObj(key, map) {
    return map.get(key);
}

export function mapSet(key, value, map) {
    map.set(key, value);
}

export function mapDelete(key, map) {
    map.delete(key);
}

/**
 * Defensive existence check - lets us read fields that were added to the note "shape" after
 * some notes already existed (e.g. while iterating on this prototype in a running session).
 */
export function mapHas(key, map) {
    return map.has(key);
}

export function mapKeys(map) {
    return Array.from(map.keys());
}

/**
 * Fires on any change anywhere below `map` - a field on a nested note map, or a
 * keystroke inside one of its Y.Text values. This is what lets a single observer
 * keep the whole board's read-model in sync no matter what changed.
 */
export function observeDeep(callback, map) {
    map.observeDeep((_events, _tx) => {
        callback();
    });
}

export function newText(initial) {
    const t = new yjs.Text();
    t.insert(0, initial);
    return t;
}

export function textToString(t) {
    return t.toString();
}

export function textLength(t) {
    return t.length | 0;
}

export function textInsert(index, content, t) {
    t.insert(index, content);
}

export function textDelete(index, length, t) {
    t.delete(index, length);
}

export function arrayPush(item, arr) {
    arr.push([item]);
}

export function arrayToList(arr) {
    return ofArray(arr.toArray());
}

export function arrayRemoveValue(value, arr) {
    const idx = tryFindIndex((y) => (value === y), arr.toArray());
    if (idx == null) {
    }
    else {
        const i = idx | 0;
        arr.delete(i, 1);
    }
}

/**
 * `scope` is one or more shared types whose changes - including nested content below them,
 * e.g. fields inside a Y.Map living inside this one - should be tracked. By default only
 * local changes are tracked (transactions with no explicit origin), which is exactly the
 * complement of `remoteOrigin` above: undo() can therefore never touch another user's change.
 */
export function newUndoManager(scope) {
    return new yjs.UndoManager(scope);
}

export function undo(um) {
    um.undo();
}

export function redo(um) {
    um.redo();
}

export function canUndo(um) {
    return um.canUndo();
}

export function canRedo(um) {
    return um.canRedo();
}

