# Yjs × F# Collaboration Prototype

A small, deliberately "clean" prototype that answers one question: **is Yjs a good
fit for real-time collaboration in an F# stack?**

Several people can move around one HTML `<canvas>` together, dropping colored
sticky notes, dragging them, and typing into them *at the same time* - with the
CRDT (Yjs) doing all the merge work. A presence bar shows who's online, and
clicking someone's avatar makes your camera follow their cursor (Miro-style),
even if they have a completely different window size or zoom level.

Everything - client and server - is F#. The client compiles to plain JS via
[Fable](https://fable.io/); the server is a small ASP.NET Core WebSocket relay.
**No Node.js/npm is involved in building or running anything** - the only
non-.NET artifact in the repo is one vendored, dependency-free copy of `yjs.mjs`.

> **Willst du dieses Muster in eigener Software nachbauen?** Die
> **[Doku in `docs/`](docs/README.md)** erklärt CRDTs/Yjs, das Architektur-Muster, typische
> Fallstricke, Skalierung und Alternativen — unabhängig von diesem konkreten Prototyp lesbar.

## Why this is convincing (the pitch)

- **Structural + text merging, for free.** Each sticky note splits its fields
  (`x`, `y`, `w`, `h`, `color`) from its body (a `Y.Text`). Two people can drag a
  note while a third is mid-sentence inside it, and nothing is lost or clobbered -
  see [`client/Doc.fs`](client/Doc.fs).
- **The server never has to understand Yjs.** It just remembers every update
  byte[] it has ever seen and replays them to new joiners - see
  [`server/Rooms.fs`](server/Rooms.fs). That's the whole "CRDT server". No
  merge logic, no conflict resolution code, nothing Yjs-specific at all.
- **Real concurrent text editing is *less* code than a lock would have been.**
  A pessimistic "note is locked while X edits it" scheme needs lock acquire/
  release/timeout/disconnect-cleanup logic. Real co-editing via `Y.Text` needs
  none of that - see the `editNoteText` diffing in `Doc.fs`.
- **Presence/"follow" works across different window sizes** because cursor
  positions are broadcast in *world* (canvas) coordinates, never screen pixels -
  see [`client/Camera.fs`](client/Camera.fs).
- **You can watch every byte move.** A live debug panel (and the browser/server
  consoles) show every WebSocket frame - direction, kind, size/content - as it
  happens. This is the whole point of a demo: you shouldn't have to take it on
  faith.

## Architecture

```
┌─────────────────────────────┐        ws://…/ws?room=default        ┌─────────────────────────────┐
│  Browser tab A              │◄────────────────────────────────────►│  Browser tab B              │
│                             │                                      │                             │
│  Elmish (MVU) ── State.fs   │                                      │  Elmish (MVU) ── State.fs   │
│      │  dispatch            │          F# ASP.NET Core             │      │  dispatch            │
│  Y.Doc (yjs.mjs) ── Doc.fs  │◄──────── Rooms.fs (relay) ──────────►│  Y.Doc (yjs.mjs) ── Doc.fs  │
│      │  binary Y-updates    │   binary = Y-update, replayed to     │      │  binary Y-updates    │
│      │  JSON awareness      │   late joiners from an in-memory     │      │  JSON awareness      │
│  <canvas> ── Canvas.fs      │   log; text = JSON awareness,        │  <canvas> ── Canvas.fs      │
└─────────────────────────────┘   relayed live only                  └─────────────────────────────┘
```

- **Binary WebSocket frames** carry raw Yjs update bytes. The server stores every
  one it sees (in arrival order) and, on every new connection, replays the whole
  log before anything else. Because Yjs updates are CRDT-safe to apply in any
  order, any number of times, that's sufficient for a late joiner's `Y.Doc` to
  converge to the same state as everyone else's - no custom sync protocol needed.
- **Text WebSocket frames** carry small, human-readable JSON: `{"type":"presence",
  "id":…, "name":…, "color":…, "cursor":{"x":…,"y":…}}` or `{"type":"bye","id":…}`.
  These are relayed live only (never logged/replayed) - presence is ephemeral by
  nature, and keeping it as plain JSON (instead of squeezing it through Yjs too)
  makes it easy to eyeball in the debug panel.

## Project layout

```
server/                  ASP.NET Core F# app (the whole "CRDT server")
  Program.fs             minimal API: static files + the /ws upgrade endpoint
  Rooms.fs                the relay: per-room connections, update log, presence cache
  wwwroot/
    index.html / styles.css   static page chrome
    lib/yjs.mjs           vendored, dependency-free Yjs ESM build (see below)
    js/                   Fable's compiled output (generated, not hand-written)

client/                  F# Fable project, MVU via Elmish
  Types.fs                Model / Msg / all shared value types
  Awareness.fs            JSON encode/decode for the presence protocol
  Interop/Yjs.fs          the ONLY file that touches the real Yjs JS API
  Interop/Ws.fs           minimal WebSocket wrapper
  Doc.fs                  sticky-note shape on top of Yjs + the socket wiring
  Camera.fs               screen <-> world coordinate math (pan/zoom/follow)
  Canvas.fs               all <canvas> drawing + hit-testing
  State.fs                Elmish `init`/`update` - the MVU core
  View.fs                 DOM shell (presence bar, toolbar, debug panel, edit overlay)
  Program.fs              entry point

dotnet-tools.json         local tool manifest (pins the `fable` CLI)
NuGet.Config              scopes package restore to nuget.org
```

## Running it

Requirements: .NET SDK 8+ only. No Node/npm needed.

```bash
# once, from the repo root:
dotnet tool restore
dotnet fable client -o server/wwwroot/js

# then, and again any time you change server/*.fs:
cd server
dotnet run
```

Open `http://localhost:5251` in two (or more) browser windows/tabs to see it
collaborate with itself. Re-run the two commands above after changing any
`client/*.fs` file (`dotnet fable client -o server/wwwroot/js`) - there's no
watch mode wired up, this is a prototype.

> `yjs.mjs` under `server/wwwroot/lib/` was produced once via
> `esm.sh/yjs@13.6.18/es2022/yjs.bundle.mjs` (a fully-bundled, dependency-free
> build) with one dead `import "/node/process.mjs"` stubbed out by hand. It's
> checked in - nothing fetches it at build or run time.

## Suggested demo script

1. Open two windows side by side. Rename yourself in each (top-left input) -
   the presence bar and debug panel update immediately.
2. Add a sticky note from either window - watch it appear in both, and watch
   the debug panel: one `note-add` (JSON-ish summary) followed by one
   `y-update` (the actual binary Yjs payload) in each direction.
3. Drag the note around in window A while window B watches it move live.
4. Double-click the note in **both** windows and type in both at the same
   time - both sets of keystrokes land, nothing is lost. Point at the
   `note-edit` debug entries: each is a minimal insert/delete, not a full
   rewrite, so two people typing in different parts of the note never conflict.
5. Resize/zoom one window differently from the other, then click the other
   person's avatar in the presence bar to follow them - your camera keeps
   their cursor centered regardless of the size/zoom mismatch. Click again to
   stop.
6. Open a third window as a "late joiner" - it receives the whole board
   instantly via the server's replay log, not by asking any other client for
   it.
7. Open the server's own console: every relayed frame is logged there too,
   independent of what any browser is doing - this is the server-side half of
   "you can watch every byte move".

## Known simplifications (intentionally out of scope for a prototype)

- **No reconnect/backoff.** If the server restarts, open tabs show
  "verbinde…" and stay disconnected until reloaded.
- **No persistence across server restarts.** The update log is in-memory only
  (this was a deliberate choice for this prototype - see the conversation that
  produced it). Swapping in a file- or DB-backed log would only touch `Rooms.fs`.
- **One hardcoded room** (`?room=default`). The server already supports
  multiple rooms; the client just never asks for a different one.
- **No auth.** Anyone who can reach the server can join and rename themselves.
- **Text merge quality**: the textarea↔Y.Text bridge uses a common
  prefix/suffix diff (see `Doc.editNoteText` / `View.patchTextareaIfChanged`),
  which handles typing, backspacing and pasting well but isn't a full
  operational-transform-grade caret tracker like `y-codemirror` would give you
  for a production editor.
