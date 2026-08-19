# Schnellstart: Loslegen im eigenen Projekt

Die vorigen Kapitel sind zum **Verstehen** da. Dieses hier ist zum **Loslegen**: eine kompakte
Entscheidungs-Checkliste plus zwei fertige Prompts, die du in eine neue KI-Coding-Session für
dein eigentliches Projekt einfügen kannst.

## Wie man das benutzt

1. Checkliste unten einmal durchgehen — nicht um sie hier zu beantworten, sondern um zu wissen,
   welche Fragen für *dein* Projekt überhaupt relevant sind.
2. **Zielprojekt ist selbst F#/Fable/MVU, ohne Node.js-Abhängigkeit** (wie dieser Prototyp)?
   → weiter unten **Variante A** nehmen — die übernimmt nicht nur die Konzepte, sondern direkt
   die technischen Lösungen für das Fable↔Yjs-Interop, die in diesem Repo schon gelöst und
   getestet sind. Sonst → **Variante B**.
3. Den gewählten Prompt kopieren, Platzhalter füllen, in eine neue Session einfügen, bevor
   irgendein Code geschrieben wird.
4. Falls die KI-Session Zugriff auf dieses Repo hat (z.B. als zweites geöffnetes Verzeichnis):
   umso besser — dann kann sie [Doc.fs](../client/Doc.fs), [Rooms.fs](../server/Rooms.fs) und
   die übrigen `docs/`-Kapitel als lauffähige Referenz konsultieren, nicht nur als Text.

## Die Entscheidungs-Checkliste

**Datenmodell**
- Welche Felder brauchen zeichengenaues Co-Editing (`Y.Text`), welche reichen als einfacher
  `Y.Map`-Wert (LWW)? → [Architektur-Muster #3](02-architektur-muster.md#3-dokumentstruktur-felder-von-freitext-trennen), [#6](02-architektur-muster.md#6-mehrere-ui-oberflächen-ein-dokument)
- Beziehungen zwischen Objekten (Verbindungen, Gruppen)? Per ID referenzieren, nie als Kopie
  einbetten. → [Architektur-Muster #6](02-architektur-muster.md#6-mehrere-ui-oberflächen-ein-dokument)
- "Ansichten" auf dieselben Objekte nötig (Unterdiagramme, die Änderungen teilen)? →
  [Architektur-Muster #8](02-architektur-muster.md#8-ansichten-auf-geteilte-elemente-kopien-ohne-echte-kopien)

**Transport & Server**
- Client-Server oder Peer-to-Peer? → [CRDT & Yjs — Transport](01-crdt-und-yjs.md#transport-das-provider-konzept--läuft-das-zwingend-über-einen-server)
- Muss der Server Yjs überhaupt verstehen, oder reicht ein dummer Relay + Replay-Log? →
  [Architektur-Muster #2](02-architektur-muster.md#2-der-server-als-dummer-relay--log)
- Awareness (Cursor, Präsenz) getrennt vom Dokument-Kanal halten. → [Architektur-Muster #1](02-architektur-muster.md#1-zwei-kanal-prinzip-dokument-vs-awareness)
- Nutzeridentität ist eine dritte, eigene Schicht, keine Yjs-Client-ID. → [Architektur-Muster #7](02-architektur-muster.md#7-nutzeridentität-ist-eine-dritte-schicht-keine-yjs-client-id)

**Konflikte, Undo, Sperren**
- Kein Locking für gemeinsam editierte Felder — CRDT-Co-Editing braucht weder Lease-Timeouts
  noch Disconnect-Erkennung. → [CRDT & Yjs — Locking](01-crdt-und-yjs.md#server-seitiges-locking-der-preis-der-zuverlässigkeit)
- Undo/Redo über `Y.UndoManager`, nicht selbst bauen. → [Fallstricke](03-fallstricke.md#was-man-sonst-vorher-wissen-sollte)

**UI-Integration**
- Kurze Felder: `<input>`/`<textarea>` + einfache Präfix/Suffix-Diff-Brücke reicht.
- Echte Rich-Text-Editoren mit sichtbaren fremden Cursorn: offizielle Bindings nehmen
  (`y-prosemirror`, `y-codemirror`, `y-monaco`), nicht selbst nachbauen. → [Fallstricke #6](03-fallstricke.md#6-die-fablejs-grenze-wo-typsicherheit-aufhört)
- Awareness-UI an einen Diff-Guard koppeln, nie blind bei jeder Änderung neu rendern. →
  [Fallstricke #2](03-fallstricke.md#2-hochfrequenter-flüchtiger-state-treibt-teure-ui-neuaufbauten)

**Robustheit & Skalierung**
- Hochfrequente lokale Änderungen vor dem Netzwerk-Commit drosseln; lokale Reaktivität bleibt
  uneingeschränkt. → [Fallstricke #3](03-fallstricke.md#3-netzwerk-throttling-vs-lokale-reaktivität)
- Rendering an `requestAnimationFrame` koppeln statt sofort bei jedem Update zu malen.
- Der Update-Log wächst mit der Zeit, nicht mit der Elementzahl — relevant bei langen Sessions.
  → [Skalierung](04-skalierung.md)

## Der Prompt

Zwei Varianten — je nachdem, ob das Zielprojekt denselben Stack wie dieser Prototyp hat
(F#/Fable, MVU, ausdrücklich ohne Node.js-Abhängigkeit) oder einen anderen.

### Variante A: F#/Fable-Projekt (wie dieser Prototyp)

Hier lohnt es sich, nicht nur die *Konzepte*, sondern die *technische Integration* direkt zu
übernehmen — das Fable↔Yjs-Interop-Problem ist in diesem Repo bereits gelöst und getestet,
muss also nicht neu erfunden werden.

```text
Ich baue in F# (Fable-Client, MVU-Pattern über Elmish, F#-Server) Echtzeit-Kollaboration ein,
ausdrücklich OHNE Node.js/npm-Abhängigkeit im Build oder zur Laufzeit. Ich habe Zugriff auf
einen funktionierenden Referenz-Prototyp mit exakt diesem Stack (F# + Fable + Yjs + ASP.NET
Core WebSocket-Relay) - [PFAD/LINK ZUM PROTOTYP-REPO EINFÜGEN]. Übernimm dessen technische
Muster direkt, statt sie neu zu entwickeln:

TECHNISCHE INTEGRATION (aus dem Referenz-Prototyp übernehmen, nicht neu erfinden)
- Yjs wird als vorgefertigtes, gebündeltes ESM-Modul (yjs.mjs) lokal eingebunden statt per npm
  installiert - z.B. via esm.sh mit ?bundle-Flag erzeugt, dabei tote Node-only-Imports (z.B.
  "/node/process.mjs") von Hand rausstubben. Siehe server/wwwroot/lib/yjs.mjs im Referenz-Repo.
  So bleibt der komplette Build ein reines `dotnet`-Kommando, ganz ohne Node/npm.
- Fable-Bindings an Yjs als EINE dünne, bewusst dynamische Interop-Datei (`?`-Operator statt
  vollständiger Typisierung) mit opaken Phantom-Typen (YDoc/YMap/YText/...) - siehe
  client/Interop/Yjs.fs. Das hält die ungetypte Fläche auf diese eine Datei begrenzt, der Rest
  der App bleibt normal typisiertes F#.
- Achtung bei [<ImportAll>]/[<Import>]: Fable löst relative Pfade gegen den *Quelltext*-Ort
  auf, nicht gegen den Ausgabe-Ort des kompilierten JS - das kostet sonst eine überraschende
  Fehlersuche.
- Achtung bei [<Global>] für Browser-Globals (z.B. WebSocket): immer expliziten Namen angeben
  ([<Global("WebSocket")>]), sonst nimmt Fable den eigenen F#-Bezeichnernamen als JS-Namen.
- NuGet-Paketwahl für Elmish: das Paket muss den F#-Quelltext mitliefern (für Fable-Kompilation
  nötig), nicht nur kompilierte DLLs (`Fable.Elmish`, nicht `Elmish`) - sonst kompiliert der
  .NET-Build durch, aber der JS-Build scheitert erst zur Laufzeit im Browser mit einer
  kryptischen Fehlermeldung.
- MVU-Rendering ohne React/Feliz: Elmish-Core (`Program.mkProgram`) mit einer imperativen
  "einmal die DOM-Struktur aufbauen, bei jedem Model-Update neu rendern"-Funktion als
  view-Parameter, siehe client/View.fs (mountShell/renderModel) - React würde eine
  npm-Abhängigkeit bedeuten, das umgehen wir bewusst.
- Server als reiner F#/ASP.NET-Core-Prozess mit rohen WebSockets (System.Net.WebSockets), kein
  SignalR nötig - der Server speichert/leitet nur Byte-Arrays weiter, versteht Yjs nicht.
  Siehe server/Rooms.fs.
- Origin-Tagging nicht vergessen (eigene vs. fremde Änderungen unterscheiden, sonst
  Echo-Schleife), siehe remoteOrigin in client/Interop/Yjs.fs.

DATENMODELL
- Welche Felder brauchen zeichengenaues Co-Editing (Y.Text), welche reichen als einfacher Wert
  in einer Y.Map (last-writer-wins, aber kausal korrekt)? Nicht alles braucht Y.Text.
- Gibt es Beziehungen zwischen Objekten (Verbindungen, Referenzen, Gruppen)? Diese per ID
  referenzieren, nie als Kopie einbetten - abgeleitete Geometrie/Zustand (Position, Routing)
  wird beim Rendern berechnet, nie gespeichert.
- Gibt es "Ansichten" auf dieselben Objekte (z.B. Unterdiagramme, die Änderungen mit dem
  Original teilen)? Dann als zweite Referenzliste auf dieselben Objekte modellieren, keine
  echten Kopien - ggf. mit Aufteilung in geteilte vs. ansichtsspezifische Eigenschaften.

KONFLIKTE, UNDO, SPERREN
- Kein Locking für gemeinsam editierte Felder einbauen - echtes CRDT-Co-Editing braucht dafür
  weder Lease-Timeouts noch Disconnect-Erkennung, und ist am Ende weniger Code als ein
  zuverlässiges Lock.
- Undo/Redo über Y.UndoManager lösen, nicht selbst bauen - Scope (pro Objekt vs. ganzes
  Dokument) bewusst wählen; er macht standardmäßig nur die eigenen Änderungen rückgängig.

UI-INTEGRATION
- Für kurze Felder (Titel, Label) reicht ein normales Eingabefeld mit einer simplen
  Präfix/Suffix-Diff-Brücke zu Y.Text (siehe editNoteTextField in client/Doc.fs +
  patchTextareaIfChanged in client/View.fs für die Cursor-Erhaltung).
- Für echte Rich-Text-Editoren mit sichtbaren fremden Cursorn: eine offizielle Bindung nehmen
  (y-prosemirror, y-codemirror, y-monaco), nicht selbst nachbauen - native Texteingabefelder
  geben keine Zeichenposition her, die man für einen fremden Cursor bräuchte.
- Awareness-getriebenes UI (Präsenz-Leiste, Cursor, Badges) an eine Diff-Prüfung koppeln, nie
  bei jeder Zustandsänderung irgendwo blind neu rendern - sonst flackert/hängt es, sobald
  mehrere Nutzer gleichzeitig aktiv sind.

ROBUSTHEIT & SKALIERUNG
- Hochfrequente lokale Änderungen (Drag, Mausbewegung) vor dem Commit ins Dokument drosseln;
  die lokale UI-Reaktion bleibt dabei uneingeschränkt schnell, nur der Netzwerk-Commit wird
  gedrosselt.
- Rendering an requestAnimationFrame koppeln statt bei jedem Update sofort zu malen.
- Debug-/Konsolen-Logging standardmäßig aus oder stark gedrosselt lassen, nicht bei jeder
  Nachricht mitloggen.
- Der Update-Log auf einem "dummen Relay"-Server wächst mit der Zeit, nicht mit der
  Elementzahl - relevant erst bei langen Sessions/vielen Änderungen, dann über Kompaktierung
  nachdenken.

Frag mich die fachlichen Punkte (Datenmodell, welche Felder Y.Text vs. Y.Map-Wert, Beziehungen/
Ansichten) zuerst durch, bevor du anfängst, Code zu schreiben - die technische Integration oben
ist bereits geklärt und soll so weit wie möglich 1:1 aus dem Referenz-Prototyp übernommen
werden, nicht neu entworfen werden.
```

### Variante B: anderer Tech-Stack

```text
Ich möchte in [TECH-STACK EINTRAGEN, z.B. "einem C#/.NET-Backend mit React-Frontend"]
Echtzeit-Kollaboration einbauen, basierend auf Yjs (CRDT). Bevor du Code schreibst, geh mit mir
erst die folgenden Entscheidungen durch und stell mir gezielte Rückfragen, wo es für unser
Projekt relevant ist - noch kein Code:

DATENMODELL
- Welche Felder brauchen zeichengenaues Co-Editing (Y.Text), welche reichen als einfacher Wert
  in einer Y.Map (last-writer-wins, aber kausal korrekt)? Nicht alles braucht Y.Text.
- Gibt es Beziehungen zwischen Objekten (Verbindungen, Referenzen, Gruppen)? Diese per ID
  referenzieren, nie als Kopie einbetten - abgeleitete Geometrie/Zustand (Position, Routing)
  wird beim Rendern berechnet, nie gespeichert.
- Gibt es "Ansichten" auf dieselben Objekte (z.B. Unterdiagramme, die Änderungen mit dem
  Original teilen)? Dann als zweite Referenzliste auf dieselben Objekte modellieren, keine
  echten Kopien - ggf. mit Aufteilung in geteilte vs. ansichtsspezifische Eigenschaften.

TRANSPORT & SERVER
- Client-Server (WebSocket-Relay) oder Peer-to-Peer (WebRTC)? Im Zweifel: Client-Server, wegen
  Persistenz und einfacherem NAT-Traversal.
- Der Server muss Yjs NICHT verstehen - er kann jedes Update-Byte-Array einfach speichern und
  an alle weiterleiten bzw. an neue Clients abspielen (Yjs-Updates sind kommutativ/idempotent,
  jede Reihenfolge/Wiederholung konvergiert zum selben Zustand).
- Awareness (Cursor, Präsenz, "wer tippt hier") getrennt vom Dokument-Kanal halten - flüchtig,
  nie geloggt, nicht zwingend über Yjs' eigenes Awareness-Protokoll (eigenes JSON ist für die
  Nachvollziehbarkeit oft einfacher).
- Nutzeridentität ist eine eigene, dritte Schicht - Yjs' Client-ID ist pro Sitzung/Tab, keine
  stabile Personenidentität. Über Awareness mit einer echten Auth-User-ID verknüpfen.

KONFLIKTE, UNDO, SPERREN
- Kein Locking für gemeinsam editierte Felder einbauen - echtes CRDT-Co-Editing braucht dafür
  weder Lease-Timeouts noch Disconnect-Erkennung, und ist am Ende weniger Code als ein
  zuverlässiges Lock.
- Undo/Redo über Y.UndoManager lösen, nicht selbst bauen - Scope (pro Objekt vs. ganzes
  Dokument) bewusst wählen; er macht standardmäßig nur die eigenen Änderungen rückgängig.

UI-INTEGRATION
- Für kurze Felder (Titel, Label) reicht ein normales Eingabefeld mit einer simplen
  Präfix/Suffix-Diff-Brücke zu Y.Text.
- Für echte Rich-Text-Editoren mit sichtbaren fremden Cursorn: eine offizielle Bindung nehmen
  (y-prosemirror, y-codemirror, y-monaco), nicht selbst nachbauen - native Texteingabefelder
  geben keine Zeichenposition her, die man für einen fremden Cursor bräuchte.
- Awareness-getriebenes UI (Präsenz-Leiste, Cursor, Badges) an eine Diff-Prüfung koppeln, nie
  bei jeder Zustandsänderung irgendwo blind neu rendern - sonst flackert/hängt es, sobald
  mehrere Nutzer gleichzeitig aktiv sind.

ROBUSTHEIT & SKALIERUNG
- Hochfrequente lokale Änderungen (Drag, Mausbewegung) vor dem Commit ins Dokument drosseln;
  die lokale UI-Reaktion bleibt dabei uneingeschränkt schnell, nur der Netzwerk-Commit wird
  gedrosselt.
- Rendering an requestAnimationFrame koppeln statt bei jedem Update sofort zu malen.
- Debug-/Konsolen-Logging standardmäßig aus oder stark gedrosselt lassen, nicht bei jeder
  Nachricht mitloggen.
- Der Update-Log auf einem "dummen Relay"-Server wächst mit der Zeit, nicht mit der
  Elementzahl - relevant erst bei langen Sessions/vielen Änderungen, dann über Kompaktierung
  nachdenken (Server wird selbst Yjs-fähig, z.B. via einem .NET-Yjs-Port, falls das der Stack
  ist).

Falls dir zu einem Punkt mehr Kontext hilft: [LINK/PFAD ZU DIESEM REPO EINFÜGEN, FALLS
ZUGÄNGLICH] enthält einen vollständigen, funktionierenden Beispiel-Prototyp (F#/Fable-Client,
F#-Server) samt einer Doku, die jeden dieser Punkte ausführlich herleitet - als lauffähige
Referenz, nicht zum 1:1-Kopieren (anderer Stack).

Frag mich die obigen Punkte zuerst durch, bevor du anfängst, Code zu schreiben.
```
