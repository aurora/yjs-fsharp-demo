
import { tryPick, truncate, iterateIndexed, reverse, ofArray, empty, cons, fold, singleton, collect } from "./fable_modules/fable-library-js.5.13.0/List.js";
import { split } from "./fable_modules/fable-library-js.5.13.0/String.js";
import { worldToScreen, screenToWorld } from "./Camera.js";
import { NoteColorModule_toShadow, NoteColorModule_toCss, Vec2 } from "./Types.js";
import { max } from "./fable_modules/fable-library-js.5.13.0/Double.js";
import { equals, disposeSafe, getEnumerator } from "./fable_modules/fable-library-js.5.13.0/Util.js";
import { tryFind } from "./fable_modules/fable-library-js.5.13.0/Map.js";
import { Union } from "./fable_modules/fable-library-js.5.13.0/Types.js";
import { union_type, string_type } from "./fable_modules/fable-library-js.5.13.0/Reflection.js";
import { defaultArg } from "./fable_modules/fable-library-js.5.13.0/Option.js";

function closeEnough(deleteHotspotR, p, center) {
    const dx = p.X - center.X;
    const dy = p.Y - center.Y;
    return Math.sqrt((dx * dx) + (dy * dy)) <= deleteHotspotR;
}

function wrapText(ctx, text, maxWidth) {
    return collect((paragraph) => {
        if (paragraph === "") {
            return singleton("");
        }
        else {
            const patternInput = fold((tupledArg, word) => {
                const lines = tupledArg[0];
                const current = tupledArg[1];
                const candidate = (current === "") ? word : ((current + " ") + word);
                if (((ctx.measureText(candidate)).width > maxWidth) && (current !== "")) {
                    return [cons(current, lines), word];
                }
                else {
                    return [lines, candidate];
                }
            }, [empty(), ""], ofArray(split(paragraph, [" "], undefined, 0)));
            return reverse(cons(patternInput[1], patternInput[0]));
        }
    }, ofArray(split(text, ["\n"], undefined, 0)));
}

function roundRect(ctx, x, y, w, h, r) {
    ctx.beginPath();
    ctx.moveTo((x + r), y);
    ctx.arcTo((x + w), y, (x + w), (y + h), r);
    ctx.arcTo((x + w), (y + h), x, (y + h), r);
    ctx.arcTo(x, (y + h), x, y, r);
    ctx.arcTo(x, y, (x + w), y, r);
    return ctx.closePath();
}

function drawGrid(ctx, cam, vw, vh) {
    ctx.save();
    ctx.strokeStyle = "#e7e5e0";
    ctx.lineWidth = 1;
    const topLeft = screenToWorld(cam, new Vec2(0, 0));
    const bottomRight = screenToWorld(cam, new Vec2(vw, vh));
    let x = Math.floor(topLeft.X / 50) * 50;
    while (x < bottomRight.X) {
        const sx = worldToScreen(cam, new Vec2(x, 0)).X;
        ctx.beginPath();
        ctx.moveTo(sx, 0);
        ctx.lineTo(sx, vh);
        ctx.stroke();
        x = (x + 50);
    }
    let y = Math.floor(topLeft.Y / 50) * 50;
    while (y < bottomRight.Y) {
        const sy = worldToScreen(cam, new Vec2(0, y)).Y;
        ctx.beginPath();
        ctx.moveTo(0, sy);
        ctx.lineTo(vw, sy);
        ctx.stroke();
        y = (y + 50);
    }
    return ctx.restore();
}

function drawNote(ctx, cam, note, isEditing, isHovered) {
    const topLeft = worldToScreen(cam, new Vec2(note.X, note.Y));
    const w = note.W * cam.Zoom;
    const h = note.H * cam.Zoom;
    ctx.save();
    ctx.shadowColor = "rgba(0,0,0,0.25)";
    ctx.shadowBlur = (isHovered ? 14 : 8);
    ctx.shadowOffsetY = 3;
    ctx.fillStyle = NoteColorModule_toCss(note.Color);
    roundRect(ctx, topLeft.X, topLeft.Y, w, h, 6 * cam.Zoom);
    ctx.fill();
    ctx.restore();
    ctx.save();
    ctx.lineWidth = (isEditing ? 3 : 1);
    ctx.strokeStyle = (isEditing ? "#2563eb" : NoteColorModule_toShadow(note.Color));
    roundRect(ctx, topLeft.X, topLeft.Y, w, h, 6 * cam.Zoom);
    ctx.stroke();
    ctx.restore();
    if (!isEditing && (cam.Zoom > 0.35)) {
        ctx.save();
        ctx.fillStyle = "#1f2937";
        const fontSize = 14 * cam.Zoom;
        ctx.font = (`${fontSize}px 'Segoe UI', sans-serif`);
        ctx.textBaseline = "top";
        const pad = 10 * cam.Zoom;
        const lines = wrapText(ctx, note.Text, w - (2 * pad));
        const lineHeight = fontSize * 1.3;
        iterateIndexed((i, line) => {
            ctx.fillText(line, (topLeft.X + pad), ((topLeft.Y + pad) + (i * lineHeight)));
        }, truncate(max(1, ~~((h - (2 * pad)) / lineHeight)), lines));
        ctx.restore();
    }
    if (isHovered && !isEditing) {
        const cx = (topLeft.X + w) - (12 * cam.Zoom);
        const cy = topLeft.Y + (12 * cam.Zoom);
        ctx.save();
        ctx.fillStyle = "rgba(0,0,0,0.35)";
        ctx.beginPath();
        ctx.arc(cx, cy, (9 * cam.Zoom), 0, 6.2832);
        ctx.fill();
        ctx.strokeStyle = "white";
        ctx.lineWidth = 1.6;
        const r = 3.5 * cam.Zoom;
        ctx.beginPath();
        ctx.moveTo((cx - r), (cy - r));
        ctx.lineTo((cx + r), (cy + r));
        ctx.moveTo((cx + r), (cy - r));
        ctx.lineTo((cx - r), (cy + r));
        ctx.stroke();
        ctx.restore();
    }
}

function drawCursor(ctx, cam, p, color, label, highlighted) {
    const s = worldToScreen(cam, p);
    ctx.save();
    ctx.translate(s.X, s.Y);
    ctx.beginPath();
    ctx.moveTo(0, 0);
    ctx.lineTo(0, 16);
    ctx.lineTo(4.5, 12.5);
    ctx.lineTo(8, 19);
    ctx.lineTo(10.5, 17.7);
    ctx.lineTo(7.2, 11.3);
    ctx.lineTo(12.5, 11);
    ctx.closePath();
    ctx.fillStyle = color;
    ctx.strokeStyle = "white";
    ctx.lineWidth = 1.5;
    ctx.fill();
    ctx.stroke();
    if (highlighted) {
        ctx.beginPath();
        ctx.arc(0, 0, 20, 0, 6.2832);
        ctx.strokeStyle = color;
        ctx.lineWidth = 2;
        ctx.setLineDash(new Int32Array([4, 3]));
        ctx.stroke();
        ctx.setLineDash([]);
    }
    ctx.font = "12px \'Segoe UI\', sans-serif";
    const textWidth = (ctx.measureText(label)).width;
    ctx.fillStyle = color;
    roundRect(ctx, 14, 14, textWidth + 12, 20, 4);
    ctx.fill();
    ctx.fillStyle = "white";
    ctx.textBaseline = "middle";
    ctx.fillText(label, 20, 24);
    return ctx.restore();
}

/**
 * Full redraw. Called imperatively from View.fs after every model update - the
 * canvas is not part of Elmish's diffing, we own it directly.
 */
export function render(canvas, model, hoveredNoteId) {
    const ctx = canvas.getContext("2d");
    const vw = model.ViewportW;
    const vh = model.ViewportH;
    ctx.fillStyle = "#faf9f6";
    ctx.fillRect(0, 0, vw, vh);
    drawGrid(ctx, model.Camera, vw, vh);
    const enumerator = getEnumerator(model.NoteOrder);
    try {
        while (enumerator["System.Collections.IEnumerator.MoveNext"]()) {
            const id = enumerator["System.Collections.Generic.IEnumerator`1.get_Current"]();
            const matchValue = tryFind(id, model.Notes);
            if (matchValue == null) {
            }
            else {
                drawNote(ctx, model.Camera, matchValue, equals(model.EditingNoteId, id), equals(hoveredNoteId, id));
            }
        }
    }
    finally {
        disposeSafe(enumerator);
    }
    const enumerator_1 = getEnumerator(model.Presence);
    try {
        while (enumerator_1["System.Collections.IEnumerator.MoveNext"]()) {
            const kv = enumerator_1["System.Collections.Generic.IEnumerator`1.get_Current"]();
            const matchValue_1 = kv[1].Cursor;
            if (matchValue_1 == null) {
            }
            else {
                drawCursor(ctx, model.Camera, matchValue_1, kv[1].Color, kv[1].Name, equals(model.Following, kv[0]));
            }
        }
    }
    finally {
        disposeSafe(enumerator_1);
    }
}

export class Hit extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["HitNote", "HitDeleteButton", "HitNothing"];
    }
    static HitNothing = new Hit(2, []);
}

export function Hit_$reflection() {
    return union_type("Client.Canvas.Hit", [], Hit, () => [[["id", string_type]], [["id", string_type]], []]);
}

/**
 * Topmost note (i.e. last in NoteOrder) whose rect contains the world point.
 */
export function hitTest(model, worldPoint) {
    const deleteHotspotWorld = 9 / model.Camera.Zoom;
    return defaultArg(tryPick((id) => {
        const matchValue = tryFind(id, model.Notes);
        if (matchValue == null) {
            return undefined;
        }
        else {
            const note = matchValue;
            if (closeEnough(deleteHotspotWorld, worldPoint, new Vec2((note.X + note.W) - 12, note.Y + 12))) {
                return new Hit(/* HitDeleteButton */ 1, [id]);
            }
            else if ((((worldPoint.X >= note.X) && (worldPoint.X <= (note.X + note.W))) && (worldPoint.Y >= note.Y)) && (worldPoint.Y <= (note.Y + note.H))) {
                return new Hit(/* HitNote */ 0, [id]);
            }
            else {
                return undefined;
            }
        }
    }, reverse(model.NoteOrder)), Hit.HitNothing);
}

