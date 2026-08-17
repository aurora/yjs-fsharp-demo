
import { concat, join, replace } from "./fable_modules/fable-library-js.5.13.0/String.js";
import { min } from "./fable_modules/fable-library-js.5.13.0/Double.js";
import { canRedo, canUndo, connect, debugSink } from "./Doc.js";
import { NoteColorModule_toCss, NoteColorModule_ofStorage, Vec2, Msg } from "./Types.js";
import { Operators_IsNull } from "./fable_modules/fable-library-js.5.13.0/FSharp.Core.js";
import { length, tryHead, map } from "./fable_modules/fable-library-js.5.13.0/List.js";
import { disposeSafe, getEnumerator, equals } from "./fable_modules/fable-library-js.5.13.0/Util.js";
import { tryFind, toList } from "./fable_modules/fable-library-js.5.13.0/Map.js";
import { toString } from "./fable_modules/fable-library-js.5.13.0/Date.js";
import { worldToScreen } from "./Camera.js";
import { render, hitTest } from "./Canvas.js";

function byId(id) {
    return document.getElementById(id);
}

function escapeHtml(s) {
    return replace(replace(replace(s, "&", "&amp;"), "<", "&lt;"), ">", "&gt;");
}

function patchTextareaIfChanged(ta, newText) {
    const oldText = ta.value;
    if (oldText !== newText) {
        const selStart = ta.selectionStart | 0;
        const selEnd = ta.selectionEnd | 0;
        const oldLen = oldText.length | 0;
        const newLen = newText.length | 0;
        const maxCommon = min(oldLen, newLen) | 0;
        let prefix = 0;
        while ((prefix < maxCommon) && (oldText[prefix] === newText[prefix])) {
            prefix = ((prefix + 1) | 0);
        }
        let suffix = 0;
        while ((suffix < (maxCommon - prefix)) && (oldText[(oldLen - 1) - suffix] === newText[(newLen - 1) - suffix])) {
            suffix = ((suffix + 1) | 0);
        }
        const insertedLen = ((newLen - prefix) - suffix) | 0;
        const delta = (insertedLen - ((oldLen - prefix) - suffix)) | 0;
        const adjust = (pos) => {
            if (pos <= prefix) {
                return pos | 0;
            }
            else if (pos >= (oldLen - suffix)) {
                return (pos + delta) | 0;
            }
            else {
                return (prefix + insertedLen) | 0;
            }
        };
        ta.value = newText;
        ta.setSelectionRange(adjust(selStart), adjust(selEnd));
    }
}

function mountShell(dispatch) {
    debugSink((dir, kind, detail) => {
        dispatch(new Msg(/* LogDebug */ 18, [dir, kind, detail]));
    });
    connect(dispatch);
    window.setInterval((() => {
        dispatch(Msg.HeartbeatTick);
    }), 120);
    const nameInput = byId("my-name");
    const wrap = byId("canvas-wrap");
    const canvas = byId("board");
    const editOverlay = byId("edit-overlay");
    const presenceBar = byId("presence-bar");
    const debugPanel = byId("debug-panel");
    const debugLog = byId("debug-log");
    const debugToggle = byId("debug-toggle");
    const connStatus = byId("conn-status");
    const connText = connStatus.querySelector(".conn-text");
    const undoBtn = byId("undo-btn");
    const redoBtn = byId("redo-btn");
    const resizeCanvas = () => {
        const w = wrap.clientWidth;
        const h = wrap.clientHeight;
        canvas.width = w;
        canvas.height = h;
        dispatch(new Msg(/* WindowResized */ 9, [w, h]));
    };
    resizeCanvas();
    window.addEventListener("resize", ((_arg) => {
        resizeCanvas();
    }));
    const localPoint = (e) => {
        const rect = canvas.getBoundingClientRect();
        const clientX = e.clientX;
        const clientY = e.clientY;
        return new Vec2(clientX - rect.left, clientY - rect.top);
    };
    canvas.addEventListener("mousedown", ((e_1) => {
        const button = e_1.button;
        dispatch(new Msg(/* MouseDown */ 4, [localPoint(e_1), ~~button]));
    }));
    canvas.addEventListener("dblclick", ((e_2) => {
        dispatch(new Msg(/* DoubleClick */ 7, [localPoint(e_2)]));
    }));
    canvas.addEventListener("wheel", ((e_3) => {
        e_3.preventDefault();
        const deltaY = e_3.deltaY;
        dispatch(new Msg(/* Wheel */ 8, [localPoint(e_3), deltaY]));
    }), {
        passive: false,
    });
    window.addEventListener("mousemove", ((e_4) => {
        dispatch(new Msg(/* MouseMove */ 5, [localPoint(e_4)]));
    }));
    window.addEventListener("mouseup", ((_arg_1) => {
        dispatch(Msg.MouseUp);
    }));
    let currentEditId = undefined;
    let pendingSelfText = undefined;
    editOverlay.addEventListener("input", ((_arg_2) => {
        if (currentEditId == null) {
        }
        else {
            const id = currentEditId;
            const value_1 = editOverlay.value;
            pendingSelfText = value_1;
            dispatch(new Msg(/* EditNoteText */ 13, [id, value_1]));
        }
    }));
    editOverlay.addEventListener("blur", ((_arg_3) => {
        dispatch(Msg.StopEditNote);
    }));
    editOverlay.addEventListener("keydown", ((e_5) => {
        if (e_5.key === "Escape") {
            editOverlay.blur();
        }
    }));
    nameInput.addEventListener("change", ((_arg_4) => {
        dispatch(new Msg(/* RenameSelf */ 19, [nameInput.value]));
    }));
    const swatches = document.querySelectorAll(".swatch");
    swatches.forEach((el) => {
        const color = NoteColorModule_ofStorage(el.getAttribute("data-color"));
        el.onclick = ((_arg_5) => {
            dispatch(new Msg(/* AddNote */ 10, [color]));
        });
    });
    debugToggle.addEventListener("click", ((_arg_6) => {
        dispatch(Msg.ToggleDebugPanel);
    }));
    undoBtn.addEventListener("click", ((_arg_7) => {
        dispatch(Msg.Undo);
    }));
    redoBtn.addEventListener("click", ((_arg_8) => {
        dispatch(Msg.Redo);
    }));
    window.addEventListener("keydown", ((e_6) => {
        if (e_6.ctrlKey ? true : e_6.metaKey) {
            const activeEl = document.activeElement;
            const activeTag = Operators_IsNull(activeEl) ? "" : activeEl.tagName;
            if (!((activeTag === "TEXTAREA") ? true : (activeTag === "INPUT"))) {
                const key_1 = e_6.key.toLocaleLowerCase();
                const shift = e_6.shiftKey;
                switch (key_1) {
                    case "z": {
                        e_6.preventDefault();
                        dispatch(shift ? Msg.Redo : Msg.Undo);
                        break;
                    }
                    case "y": {
                        e_6.preventDefault();
                        dispatch(Msg.Redo);
                        break;
                    }
                    default:
                        undefined;
                }
            }
        }
    }));
    let lastRenderedHead = undefined;
    let lastPresenceSignature = "";
    let pendingPaintModel = undefined;
    let paintScheduled = false;
    return (model_5) => {
        connStatus.className = ("conn-status" + (model_5.Connected ? " online" : ""));
        connText.textContent = (model_5.Connected ? "verbunden" : "verbinde…");
        if (!(document.activeElement === nameInput)) {
            nameInput.value = model_5.MyName;
        }
        undoBtn.disabled = !canUndo();
        redoBtn.disabled = !canRedo();
        if (model_5.DebugOpen) {
            debugPanel.classList.add("open");
            debugToggle.classList.add("active");
        }
        else {
            debugPanel.classList.remove("open");
            debugToggle.classList.remove("active");
        }
        const model = model_5;
        const signature = (((model.MyName + "|") + model.MyColor) + "|") + join(",", map((tupledArg) => {
            const id_1 = tupledArg[0];
            const info = tupledArg[1];
            return `${id_1}:${info.Name}:${info.Color}:${equals(model.Following, id_1)}`;
        }, toList(model.Presence)));
        if (signature !== lastPresenceSignature) {
            lastPresenceSignature = signature;
            presenceBar.innerHTML = "";
            const selfDiv = document.createElement("div");
            selfDiv.className = "avatar self";
            selfDiv.style.background = model.MyColor;
            selfDiv.title = concat(model.MyName, " (du)");
            selfDiv.textContent = ((model.MyName.length > 0) ? model.MyName[0].toLocaleUpperCase() : "?");
            presenceBar.appendChild(selfDiv);
            const enumerator = getEnumerator(model.Presence);
            try {
                while (enumerator["System.Collections.IEnumerator.MoveNext"]()) {
                    const info_1 = enumerator["System.Collections.Generic.IEnumerator`1.get_Current"]()[1];
                    const isFollowing = equals(model.Following, info_1.ClientId);
                    const div = document.createElement("div");
                    div.className = ("avatar" + (isFollowing ? " following" : ""));
                    div.style.background = info_1.Color;
                    div.title = (info_1.Name + (isFollowing ? " (Folgen aktiv - Klick zum Beenden)" : " (Klick zum Folgen)"));
                    div.textContent = info_1.Initial;
                    div.onclick = ((_arg_9) => {
                        dispatch(new Msg(/* ToggleFollow */ 15, [info_1.ClientId]));
                    });
                    presenceBar.appendChild(div);
                }
            }
            finally {
                disposeSafe(enumerator);
            }
        }
        const model_1 = model_5;
        const matchValue = tryHead(model_1.DebugLog);
        let matchResult, entry_1;
        if (matchValue != null) {
            if (!equals(matchValue, lastRenderedHead)) {
                matchResult = 0;
                entry_1 = matchValue;
            }
            else {
                matchResult = 1;
            }
        }
        else {
            matchResult = 1;
        }
        switch (matchResult) {
            case 0: {
                lastRenderedHead = entry_1;
                let dirClass;
                const matchValue_1 = entry_1.Direction;
                dirClass = ((matchValue_1.tag === 1) ? "dir-out" : ((matchValue_1.tag === 2) ? "dir-info" : "dir-in"));
                let arrow;
                const matchValue_2 = entry_1.Direction;
                arrow = ((matchValue_2.tag === 1) ? "&rarr;" : ((matchValue_2.tag === 2) ? "&middot;" : "&larr;"));
                const row = document.createElement("div");
                row.className = concat("debug-entry ", dirClass);
                row.innerHTML = (`<span class="t">${toString(entry_1.Time, "HH:mm:ss.fff")}</span><span class="dir">${arrow}</span><span class="kind">${entry_1.Kind}</span><span class="detail">${escapeHtml(entry_1.Detail)}</span>`);
                debugLog.prepend(row);
                const targetCount = length(model_1.DebugLog) | 0;
                let childCount = debugLog.childElementCount;
                while (childCount > targetCount) {
                    debugLog.removeChild(debugLog.lastElementChild);
                    childCount = debugLog.childElementCount;
                }
                break;
            }
        }
        const model_2 = model_5;
        const matchValue_3 = model_2.EditingNoteId;
        if (matchValue_3 != null) {
            const id_2 = matchValue_3;
            const matchValue_4 = tryFind(id_2, model_2.Notes);
            if (matchValue_4 != null) {
                const note = matchValue_4;
                const topLeft = worldToScreen(model_2.Camera, new Vec2(note.X, note.Y));
                const w_1 = note.W * model_2.Camera.Zoom;
                const h_1 = note.H * model_2.Camera.Zoom;
                editOverlay.style.display = "block";
                editOverlay.style.left = (topLeft.X.toString() + "px");
                editOverlay.style.top = (topLeft.Y.toString() + "px");
                editOverlay.style.width = (w_1.toString() + "px");
                editOverlay.style.height = (h_1.toString() + "px");
                editOverlay.style.background = NoteColorModule_toCss(note.Color);
                editOverlay.style.fontSize = ((14 * model_2.Camera.Zoom).toString() + "px");
                if (!equals(currentEditId, id_2)) {
                    currentEditId = id_2;
                    pendingSelfText = undefined;
                    editOverlay.value = note.Text;
                    editOverlay.focus();
                    const len = note.Text.length | 0;
                    editOverlay.setSelectionRange(len, len);
                }
                else if (pendingSelfText == null) {
                    patchTextareaIfChanged(editOverlay, note.Text);
                }
                else if (pendingSelfText !== note.Text) {
                    const pending_1 = pendingSelfText;
                }
                else {
                    pendingSelfText = undefined;
                }
            }
        }
        else {
            editOverlay.style.display = "none";
            currentEditId = undefined;
            pendingSelfText = undefined;
        }
        pendingPaintModel = model_5;
        if (!paintScheduled) {
            paintScheduled = true;
            window.requestAnimationFrame((_arg_10) => {
                let matchValue_7;
                paintScheduled = false;
                if (pendingPaintModel != null) {
                    const model_3 = pendingPaintModel;
                    pendingPaintModel = undefined;
                    let hoveredNoteId;
                    const matchValue_5 = model_3.LocalCursorWorld;
                    if (matchValue_5 == null) {
                        hoveredNoteId = undefined;
                    }
                    else {
                        const matchValue_6 = hitTest(model_3, matchValue_5);
                        let matchResult_1, id_3;
                        switch (matchValue_6.tag) {
                            case 1: {
                                matchResult_1 = 0;
                                id_3 = matchValue_6.fields[0];
                                break;
                            }
                            case 2: {
                                matchResult_1 = 1;
                                break;
                            }
                            default: {
                                matchResult_1 = 0;
                                id_3 = matchValue_6.fields[0];
                            }
                        }
                        switch (matchResult_1) {
                            case 0: {
                                hoveredNoteId = id_3;
                                break;
                            }
                            default:
                                hoveredNoteId = undefined;
                        }
                    }
                    canvas.style.cursor = ((matchValue_7 = model_3.Drag, (matchValue_7.tag === 2) ? "grabbing" : ((matchValue_7.tag === 0) ? ((hoveredNoteId == null) ? "default" : "grab") : "grabbing")));
                    render(canvas, model_3, hoveredNoteId);
                }
            });
        }
    };
}

let mounted = false;

let cachedRender = (value) => {
};

/**
 * The Elmish `view` function. Elmish hands us the *same* dispatch closure on every
 * call, so it's safe to build the DOM + wire events only the first time and just
 * re-render on every call after that.
 */
export function view(model, dispatch) {
    if (!mounted) {
        mounted = true;
        cachedRender = mountShell(dispatch);
    }
    cachedRender(model);
}

