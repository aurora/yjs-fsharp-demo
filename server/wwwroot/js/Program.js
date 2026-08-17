
import { item } from "./fable_modules/fable-library-js.5.13.0/Array.js";
import { int32ToString } from "./fable_modules/fable-library-js.5.13.0/Util.js";
import { snapshot, myClientId } from "./Doc.js";
import { update, init, StartupConfig } from "./State.js";
import { toArray } from "./fable_modules/fable-library-js.5.13.0/List.js";
import { ProgramModule_mkProgram, ProgramModule_run } from "./fable_modules/Fable.Elmish.5.0.2/program.fs.js";
import { view } from "./View.js";

const palette = ["#e63946", "#2a9d8f", "#e9c46a", "#457b9d", "#f4a261", "#6d597a", "#3a86ff", "#ff6392"];

function randomColor() {
    return item(~~(Math.random() * palette.length), palette);
}

function randomName() {
    const adjectives = ["Fox", "Owl", "Lynx", "Wren", "Hare", "Puma", "Kite", "Newt"];
    return item(~~(Math.random() * adjectives.length), adjectives) + int32ToString(~~(Math.random() * 90) + 10);
}

(function (_arg) {
    const cfg = new StartupConfig(randomName(), randomColor(), myClientId);
    window.__collab = {
        clientId: myClientId,
        snapshot: () => toArray(snapshot()[0]),
    };
    ProgramModule_run(ProgramModule_mkProgram(() => init(cfg, undefined), update, (model_1, dispatch) => {
        view(model_1, dispatch);
    }));
    return 0;
})(typeof process === 'object' ? process.argv.slice(2) : []);

