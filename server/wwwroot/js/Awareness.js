
import { Union } from "./fable_modules/fable-library-js.5.13.0/Types.js";
import { union_type, option_type, string_type, float64_type } from "./fable_modules/fable-library-js.5.13.0/Reflection.js";
import { Vec2, Vec2_$reflection } from "./Types.js";
import { defaultOf } from "./fable_modules/fable-library-js.5.13.0/Util.js";
import { Operators_IsNull } from "./fable_modules/fable-library-js.5.13.0/FSharp.Core.js";

export class Incoming extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Presence", "Bye"];
    }
}

export function Incoming_$reflection() {
    return union_type("Client.Awareness.Incoming", [], Incoming, () => [[["Item1", float64_type], ["name", string_type], ["color", string_type], ["cursor", option_type(Vec2_$reflection())]], [["Item", float64_type]]]);
}

export function encodePresence(id, name, color, cursor) {
    let c;
    const value = {
        type: "presence",
        id: id,
        name: name,
        color: color,
        cursor: (cursor == null) ? defaultOf() : ((c = cursor, {
            x: c.X,
            y: c.Y,
        })),
    };
    return JSON.stringify(value);
}

export function tryDecode(json) {
    try {
        const o = JSON.parse(json);
        const kind = o.type;
        switch (kind) {
            case "presence": {
                const id = o.id;
                const name = o.name;
                const color = o.color;
                const cursorRaw = o.cursor;
                return new Incoming(/* Presence */ 0, [id, name, color, Operators_IsNull(cursorRaw) ? undefined : (new Vec2(cursorRaw.x, cursorRaw.y))]);
            }
            case "bye":
                return new Incoming(/* Bye */ 1, [o.id]);
            default:
                return undefined;
        }
    }
    catch (matchValue) {
        return undefined;
    }
}

