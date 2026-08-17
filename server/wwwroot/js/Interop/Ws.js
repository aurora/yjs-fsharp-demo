
import { Record } from "../fable_modules/fable-library-js.5.13.0/Types.js";
import { obj_type, record_type, string_type, array_type, uint8_type, lambda_type, unit_type } from "../fable_modules/fable-library-js.5.13.0/Reflection.js";

export class Handlers extends Record {
    constructor(OnOpen, OnClose, OnBinary, OnText) {
        super();
        this.OnOpen = OnOpen;
        this.OnClose = OnClose;
        this.OnBinary = OnBinary;
        this.OnText = OnText;
    }
}

export function Handlers_$reflection() {
    return record_type("Client.Interop.Ws.Handlers", [], Handlers, () => [["OnOpen", lambda_type(unit_type, unit_type)], ["OnClose", lambda_type(unit_type, unit_type)], ["OnBinary", lambda_type(array_type(uint8_type), unit_type)], ["OnText", lambda_type(string_type, unit_type)]]);
}

export class Connection extends Record {
    constructor(Socket) {
        super();
        this.Socket = Socket;
    }
}

export function Connection_$reflection() {
    return record_type("Client.Interop.Ws.Connection", [], Connection, () => [["Socket", obj_type]]);
}

export function connect(url, handlers) {
    const socket = new WebSocket(url);
    socket.binaryType = "arraybuffer";
    socket.onopen = ((_arg) => {
        handlers.OnOpen();
    });
    socket.onclose = ((_arg_1) => {
        handlers.OnClose();
    });
    socket.onmessage = ((ev) => {
        const data = ev.data;
        if (typeof data === "string") {
            const s = data;
            handlers.OnText(s);
        }
        else {
            handlers.OnBinary(new Uint8Array(data));
        }
    });
    return new Connection(socket);
}

export function isOpen(conn) {
    return conn.Socket.readyState === 1;
}

export function sendBinary(conn, bytes) {
    if (isOpen(conn)) {
        conn.Socket.send(bytes);
    }
}

export function sendText(conn, text) {
    if (isOpen(conn)) {
        conn.Socket.send(text);
    }
}

