# Architektur-Muster

Dieses Kapitel beschreibt die übertragbaren Bausteine — unabhängig davon, ob das eigene
Backend in F#, C#, TypeScript oder etwas anderem geschrieben ist, und unabhängig vom
UI-Framework auf dem Client. Wo hilfreich, wird auf die konkrete Umsetzung in diesem Repo
verwiesen.

```
┌───────────────────────────────┐        ws://…/ws?room=default        ┌───────────────────────────────┐
│  Client A                     │◄────────────────────────────────────►│  Client B                     │
│                               │                                      │                               │
│  UI-State (Redux/Elmish/…)    │                                      │  UI-State (Redux/Elmish/…)    │
│      │  Projektion            │            Relay-Server              │      │  Projektion            │
│  Y.Doc (Yjs) ── Doc-Layer     │◄──────── "dummer" Relay + Log ──────►│  Y.Doc (Yjs) ── Doc-Layer     │
│      │  binäre Y-Updates      │   binär = Y-Update, für Nachzügler   │      │  binäre Y-Updates      │
│      │  Awareness (JSON/Bin.) │   geloggt & abgespielt; Awareness    │      │  Awareness (JSON/Bin.) │
│  UI-Rendering                 │   nur live weitergeleitet            │  UI-Rendering                 │
└───────────────────────────────┘                                      └───────────────────────────────┘
```

## 1. Zwei-Kanal-Prinzip: Dokument vs. Awareness

Trenne von Anfang an zwei fundamental verschiedene Arten von geteiltem Zustand:

| | **Dokument-Kanal** | **Awareness-Kanal** |
|---|---|---|
| Beispiele | Notiz-Inhalte, Positionen, Struktur | Cursor-Position, wer ist online, wer tippt gerade |
| Lebensdauer | dauerhaft, muss überleben | flüchtig, nur "jetzt gerade" relevant |
| Transport | binäre Yjs-Updates | einfaches JSON (oder Yjs' eigenes Awareness-Protokoll) |
| Serverseitig | wird geloggt & an Nachzügler abgespielt | wird nur live weitergeleitet, nie geloggt |

Der Grund, das zu trennen: Cursor-Positionen ändern sich sehr oft (jede Mausbewegung) und sind
für einen neu beitretenden Client uninteressant, sobald sie veraltet sind. Würde man sie durch
denselben Mechanismus wie die Dokumentdaten schleusen (geloggt, für immer aufbewahrt), würde der
Log mit größtenteils irrelevanten Daten überflutet. In diesem Prototyp ist der Awareness-Kanal
bewusst simples, lesbares JSON (siehe [Awareness.fs](../client/Awareness.fs)) statt Yjs' eigenem
binären Awareness-Protokoll — für ein Demo-System gut nachvollziehbar, für ein Produktivsystem
eher das offizielle `y-protocols/awareness` in Betracht ziehen (siehe
[Fallstricke](03-fallstricke.md)).

## 2. Der Server als "dummer" Relay + Log

Das ist der Kernvorteil aus [Kapitel 1](01-crdt-und-yjs.md#wie-löst-yjs-das-konkret): weil Yjs-
Updates sich in *jeder* Reihenfolge und *beliebig oft* anwenden lassen und trotzdem konvergieren,
muss der Server das Dokument **nicht verstehen**. Er muss nur:

1. jedes empfangene Update-Byte-Array für den jeweiligen "Raum" merken (Reihenfolge egal für die
   Korrektheit, aber praktischerweise in Ankunftsreihenfolge), und
2. es live an alle anderen verbundenen Clients weiterleiten, und
3. bei einer neuen Verbindung die gesamte bisherige Sammlung an diesen einen Client abspielen.

Damit konvergiert der neue Client von selbst zum aktuellen Zustand — ganz ohne dass der Server
je ein `Y.Map` oder `Y.Text` gesehen hätte. Siehe [Rooms.fs](../server/Rooms.fs) für eine
vollständige Umsetzung in ca. 100 Zeilen.

**Grenze dieses Musters**: der Log wächst unbegrenzt mit der Anzahl der Änderungsoperationen
(nicht mit der Anzahl der Elemente!) — dazu mehr in [Skalierung](04-skalierung.md).

## 3. Dokumentstruktur: Felder von Freitext trennen

Für jedes kollaborativ bearbeitbare "Objekt" (in diesem Prototyp: eine Sticky-Note) lohnt es
sich, strukturierte Felder (Position, Farbe, Größe) von freiem Text in **getrennte
CRDT-Typen** zu legen, auch wenn sie fachlich zusammengehören:

```
notes: Y.Map<id, Y.Map>
  └─ jede Note: { x, y, w, h, color, text: Y.Text }
```

Der Grund: `Y.Map`-Felder werden pro Schlüssel unabhängig gemerged (letzte Schreiboperation pro
Feld gewinnt, kausal korrekt nachverfolgt), während `Y.Text` zeichengenau merged. Wenn Position
und Text im selben Feld lägen, würde ein gleichzeitiges Verschieben und Tippen zu einem "wer
zuletzt schreibt gewinnt komplett" führen — mit getrennten Feldern verlieren weder die
Positionsänderung noch die Texteingabe etwas. Siehe [Doc.fs](../client/Doc.fs) für die konkrete
Umsetzung inkl. Kommentaren.

## 4. Integration in ein UI-Framework (MVU, Redux, MobX, …)

Unabhängig vom konkreten Framework hat sich folgendes Muster bewährt:

- Das Yjs-Dokument ist die **Quelle der Wahrheit**. Der UI-State (Model/Store/…) hält nur eine
  **Projektion** davon — ein günstig zu lesendes, plain-data Abbild, das bei jeder Änderung neu
  aus dem Dokument erzeugt wird.
- Mutationen (Nutzer verschiebt eine Note, tippt Text) werden **direkt an der Stelle, an der die
  Absicht entsteht**, gegen das Yjs-Dokument ausgeführt — nicht über einen Umweg über den
  UI-State.
- Ein einziger `observeDeep`-artiger Beobachter auf dem Dokument erzeugt bei *jeder* Änderung
  (egal ob lokal oder von einem anderen Client) eine neue Projektion und speist sie über den
  normalen Dispatch-Mechanismus des Frameworks zurück in den UI-State.

Das funktioniert identisch mit Redux (`store.dispatch` im Observer-Callback), MobX (Observable
direkt im Callback aktualisieren), Vue/Svelte-Reactivity, oder — wie hier — Elmish/MVU (siehe
[Doc.fs](../client/Doc.fs) `connect`-Funktion). Wichtig dabei: siehe unbedingt
[Fallstricke](03-fallstricke.md#1-echo-race-zwischen-eigenem-edit-und-crdt-echo) — die
Rückkopplung des eigenen Updates über diesen Observer ist die häufigste Fehlerquelle beim
Einbau.

## 5. Geteilte Cursor: Weltkoordinaten statt Bildschirmkoordinaten

Für jede Art von "wo zeigt der Cursor der anderen Person gerade hin"-Feature (Cursor-Anzeige,
Follow-Mode) gilt: **niemals Bildschirm-/Pixelkoordinaten übertragen.** Zwei Clients haben fast
nie dieselbe Fenstergröße, denselben Zoom-Level oder denselben Kamera-Ausschnitt. Stattdessen:

1. Jeder Client hält eine eigene, lokale Kamera-/Viewport-Transformation (Pan, Zoom).
2. Cursor-Positionen werden **vor dem Versenden** von Bildschirm- in Weltkoordinaten umgerechnet
   (invertierte Kamera-Transformation).
3. Beim Empfang wird die Weltkoordinate **mit der eigenen, lokalen Kamera** wieder auf den
   Bildschirm projiziert.

Damit sieht jeder Client den Cursor der anderen an der fachlich richtigen Stelle im gemeinsamen
Koordinatensystem — unabhängig von Fenstergröße oder Zoom. "Follow"-Modus ist dann nur noch:
die eigene Kamera kontinuierlich auf die zuletzt empfangene Weltkoordinate der gefolgten Person
zentrieren. Siehe [Camera.fs](../client/Camera.fs).

## 6. Mehrere UI-Oberflächen, ein Dokument

Nichts an den bisherigen Mustern ist auf "ein Canvas" beschränkt. Eine komplexere Anwendung
— z.B. ein Canvas *plus* ein zeilenbasierter Property-Editor (Label/Type/Value, verschachtelt,
im Prinzip ein JSON-Editor) *plus* eingefügte Bilder *plus* Verbindungspfeile zwischen Objekten —
braucht dafür kein zweites Muster. Jede UI-Oberfläche ist einfach eine weitere Projektion
desselben Dokuments (siehe [Punkt 4](#4-integration-in-ein-ui-framework-mvu-redux-mobx-)), und
alle Mutationen aus jeder dieser Oberflächen laufen gegen dieselben geteilten Typen. Konkret:

- **Property-Editor**: dieselbe `Y.Map` einer Note, nur rekursiv als Zeilen statt als Box auf
  dem Canvas dargestellt. Ein `Y.Map`-Feld wird zur Zeile, ein verschachtelter `Y.Map`/`Y.Array`-
  Wert zur aufklappbaren Unterzeile. Nicht jedes Feld braucht dabei `Y.Text` — kurze Skalare
  (Zahl, Bool, Typ-Enum) einfach als Rohwert in der `Y.Map` ablegen, das ergibt automatisch
  kausal-korrektes "letzter Schreiber gewinnt" pro Feld. `Y.Text` lohnt sich nur dort, wo
  wirklich zeichengenaues gleichzeitiges Tippen gewünscht ist (Freitext-Beschreibungen).
  Praxistipp: den Change-Observer nur auf der Map des *aktuell selektierten* Objekts
  registrieren, nicht auf dem ganzen Dokument — sonst rendert der Editor bei jeder Änderung
  irgendwo neu (siehe [Fallstricke #2](03-fallstricke.md#2-hochfrequenter-flüchtiger-state-treibt-teure-ui-neuaufbauten)).
- **Binäre Assets (Screenshots etc.)**: gehören nicht ins CRDT-Dokument. Bild separat hochladen
  (normaler HTTP-Upload, mit Yjs nicht verknüpft), nur die resultierende URL/ID landet als
  String-Feld in einer ganz normalen `Y.Map` (`{ type: "image", url, x, y, w, h }`). Das Bild
  selbst wird nie kollaborativ bearbeitet, nur seine Platzierung — und die ist wieder dasselbe
  Muster wie bei jedem anderen Element.
- **Ein-/ausblendbare Elemente**: ein weiteres Feld (`visible: bool`) auf derselben Map,
  direkt gemutet, beim Zeichnen bei Bedarf übersprungen. Kein neues Muster.
- **Verbindungen zwischen Objekten (Pfeile)**: eine Verbindung ist selbst ein Objekt mit
  eigener `Y.Map` (eigene ID, `fromId`, `toId`, ggf. Stil), typischerweise in einer eigenen
  Dokument-Wurzel (`doc.getMap("connectors")`). Referenziert wird **per ID**, nicht indem das
  verbundene Objekt eingebettet wird — dieselbe Technik wie bei der `noteOrder`-Liste im
  Prototyp, die auch nur IDs hält.

### Die Faustregel: Nutzerabsicht wird synchronisiert, Berechnungen werden abgeleitet

Das ist die Regel, die sich durch alle vier Beispiele oben zieht, und die man auf jede neue
UI-Oberfläche anwenden kann: **alles, was eine echte Entscheidung eines Nutzers ist, gehört ins
Dokument. Alles, was sich aus bereits synchronisierten Daten berechnen lässt, gehört nicht
hinein — es wird bei jedem Rendern neu abgeleitet.**

Bei Pfeilen zeigt sich das besonders deutlich: *dass* Note A mit Note B verbunden ist, ist eine
Entscheidung (→ gespeichert). *Wo genau* die Linie dazwischen verläuft, ist bei einem Tool mit
automatischem Routing (Hindernisse umgehen, keine Überlappung mit anderen Objekten) dagegen
keine Entscheidung, sondern eine Berechnung aus bereits bekannten Daten (Positionen/Größen aller
Objekte + welche zwei IDs verbunden sind) — sie muss **gar nicht synchronisiert werden**. Jeder
Client führt denselben Routing-Algorithmus lokal gegen dieselben (synchronisierten) Eingabedaten
aus und kommt zwangsläufig zum selben Ergebnis, ganz ohne dass die berechnete Route je über das
Netzwerk müsste.

Zwei Dinge sind dabei zu beachten:

- **Der Algorithmus muss deterministisch sein** — reine Funktion der Eingabedaten, kein Zufall,
  kein externer mutabler Zustand. Die häufigste Falle: wenn der Algorithmus bei Gleichstand
  (z.B. zwei gleich kurze Routen) die Iterationsreihenfolge einer Map/eines Arrays als
  Tie-Breaker nutzt, und diese Reihenfolge nicht explizit definiert ist, können zwei Clients in
  seltenen Fällen unterschiedliche, beide gültige Routen berechnen (kein Datenfehler, nur ein
  kurzzeitiger visueller Unterschied bis zum nächsten Recompute) — vermeidbar, indem
  Hindernislisten für die Berechnung explizit nach etwas Stabilem sortiert werden (z.B. ID),
  statt sich auf zufällige Map-Iterationsreihenfolge zu verlassen.
- **Ergebnis cachen**: da die Route sonst bei jedem Rendern neu berechnet würde (z.B. während
  eine verbundene Note gezogen wird, mit bis zu ~25 Positions-Updates/Sekunde bei gedrosseltem
  Netzwerk-Commit, siehe [Fallstricke #3](03-fallstricke.md#3-netzwerk-throttling-vs-lokale-reaktivität)) —
  Route nur neu berechnen, wenn sich einer der beiden Endpunkte oder ein relevantes Hindernis
  tatsächlich bewegt hat, nicht bei jedem Frame pauschal.

Würde das Tool später erlauben, eine automatisch berechnete Route manuell zu verbiegen (einen
Wegpunkt von Hand verschieben), wäre genau dieser eine Wegpunkt wieder echte Nutzerabsicht und
müsste gespeichert werden — der Rest der Route bliebe trotzdem abgeleitet.

Der entscheidende Punkt dahinter: nichts davon ändert etwas am Server. Er bleibt exakt so
"dumm" wie in [Punkt 2](#2-der-server-als-dummer-relay--log) beschrieben, egal wie viele
Dokument-Wurzeln oder UI-Oberflächen der Client anlegt. Die Komplexität einer wachsenden UI ist
vollständig eine Client-seitige Datenmodellierungsfrage.
