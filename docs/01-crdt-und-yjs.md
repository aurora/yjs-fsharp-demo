# CRDT & Yjs — Grundlagen

## Was ist ein CRDT?

CRDT steht für **Conflict-free Replicated Data Type**. Die Grundidee: eine Datenstruktur wird
so entworfen, dass zwei (oder beliebig viele) unabhängig geänderte Kopien davon sich **immer**
automatisch, deterministisch und ohne zentrale Instanz zu demselben Endergebnis zusammenführen
lassen — egal in welcher Reihenfolge oder wie oft die Änderungen eintreffen.

Das ist der entscheidende Unterschied zu klassischen Ansätzen für kollaborative Software:

- **Operational Transformation (OT)** — der Ansatz, mit dem z.B. Google Docs historisch und
  Etherpad/ShareDB gebaut wurden — braucht einen **zentralen, autoritativen Server**, der
  eintreffende Änderungen gegeneinander "transformiert", damit sie in der richtigen Reihenfolge
  angewendet werden. Der Transform-Algorithmus muss für jeden Datentyp separat und sehr
  sorgfältig entwickelt werden; historisch ein bekannter Quell subtiler Korrektheitsfehler.
  Ohne Server keine Zusammenführung — Offline-Bearbeitung ist nur mit erheblichem
  Zusatzaufwand möglich.
- **CRDTs** brauchen diese zentrale Instanz nicht. Jeder Client kann offline arbeiten, beliebig
  viele Änderungen sammeln, und sie später mit jedem anderen Client (oder direkt Peer-to-Peer,
  ganz ohne Server) zusammenführen. Ein Server wird dadurch optional — er kann ein reiner
  "dummer" Verteiler sein (siehe [Architektur-Muster](02-architektur-muster.md)), muss die
  Datenstruktur selbst aber gar nicht verstehen.

Der Preis dafür: die Datenstruktur und ihr Merge-Algorithmus müssen von Anfang an so entworfen
sein, dass diese Eigenschaft mathematisch garantiert ist (Fachbegriffe: *Kommutativität*,
*Assoziativität*, *Idempotenz* der Änderungsoperationen). Das lässt sich nicht nachträglich in
eine beliebige Datenstruktur reinpatchen — es ist eine grundlegende Entwurfsentscheidung.

## Wie löst Yjs das konkret?

[Yjs](https://github.com/yjs/yjs) (MIT-Lizenz) ist eine JS/TS-Bibliothek, die genau das für die
in UI-Software gängigen Datentypen bereitstellt:

- `Y.Map`, `Y.Array` — strukturierte Daten (Key-Value bzw. Listen)
- `Y.Text`, `Y.XmlFragment` — Freitext bzw. Rich-Text
- `Y.Doc` — der Container, der beliebig viele dieser Typen zusammenhält

Jede Änderung erzeugt ein kompaktes binäres **Update** (ein Byte-Array). Diese Updates sind so
konstruiert, dass sie sich in *jeder* Reihenfolge und *beliebig oft* auf jeden Dokumentzustand
anwenden lassen und trotzdem immer im selben Endzustand konvergieren — das ist exakt die
Eigenschaft, die den "dummen Relay-Server" in diesem Prototyp erst möglich macht (siehe
[Rooms.fs](../server/Rooms.fs) und [Architektur-Muster](02-architektur-muster.md)).

Für Freitext verwendet Yjs einen eigenen, 2019 publizierten Algorithmus namens **YATA**
("Yet Another Transformation Approach", Kevin Jahns, GROUP '19) — eine Weiterentwicklung von
RGA-artigen (Replicated Growable Array) Sequenz-CRDTs. Das ist der Teil, der bei
Freitext-Kollaboration am meisten Sorgfalt braucht: naive Ansätze produzieren bei gleichzeitigem
Tippen an derselben Stelle leicht vertauschte oder duplizierte Zeichen.

## Transport: das Provider-Konzept — läuft das zwingend über einen Server?

Nein. `Y.Doc` und die CRDT-Typen wissen selbst nichts über Netzwerk — sie feuern nur ein
`update`-Event mit den Änderungs-Bytes und können beliebige eingehende Updates per
`applyUpdate` entgegennehmen. **Wie** diese Bytes von A nach B kommen, ist bei Yjs bewusst
ausgelagert in austauschbare **Provider**, die sich an dieses Event andocken. Offiziell
gepflegt/dokumentiert gibt es u.a.:

- **`y-websocket`** — Client-Server, das Muster aus diesem Prototyp (Yjs liefert dafür eine
  Node-Referenzimplementierung; [Rooms.fs](../server/Rooms.fs) ist die F#-Variante desselben
  Prinzips).
- **`y-webrtc`** — echtes Peer-to-Peer. Dokument-Updates fließen direkt zwischen den Browsern,
  kein Server sieht die Nutzdaten. WebRTC selbst braucht trotzdem einen minimalen
  "Signaling"-Kanal, um zwei Peers überhaupt erst zueinander zu finden (Verbindungsdaten
  austauschen) — das ist eine WebRTC-Grundeigenschaft, keine Yjs-Beschränkung, und dieser
  Signaling-Server muss das Dokument ebenso wenig verstehen wie ein `y-websocket`-Relay.
- **`y-indexeddb`** — kein Netzwerk-Provider, sondern lokale Persistenz im Browser
  (Offline-Fähigkeit, schnelles Neuladen). Mehrere Provider können gleichzeitig an demselben
  `Y.Doc` hängen, da jeder nur auf dasselbe `update`-Event lauscht — üblich ist z.B.
  `y-indexeddb` (lokal) **plus** `y-websocket` (Server-Sync) gleichzeitig.

**In der Praxis** setzen die meisten produktiven Yjs-Systeme trotzdem auf Client-Server statt
WebRTC-Mesh — JupyterLab, Hocuspocus/Tiptap und y-sweet (alle unten genannt) eingeschlossen.
Gründe: Persistenz (jemand muss den Stand halten, wenn gerade niemand online ist), einfacheres
NAT-Traversal, und Mesh-P2P skaliert mit wachsender Personenzahl schlechter (potenziell jeder
mit jedem verbunden). WebRTC/P2P wird vor allem dann interessant, wenn explizit kein Server die
Nutzdaten sehen soll (Datenschutz, Kosten, reine LAN-Szenarien).

Dieser Prototyp verwendet Client-Server — u.a. weil das genau den Zweck erfüllt, jedes Byte auf
dem Server mitverfolgen zu können (siehe Debug-Panel); bei echtem P2P gäbe es serverseitig
nichts zu beobachten.

## Warum ist das schwer, selbst zu bauen?

Verteilen von Änderungen (Netzwerk, WebSockets, Broadcast) ist der leichte Teil — das kann jedes
halbwegs erfahrene Team bauen. Der schwere Teil ist der **Merge-Algorithmus selbst**, besonders
für Text: zwei Personen tippen gleichzeitig an nah beieinanderliegenden Stellen — was ist das
korrekte, für beide intuitive Ergebnis? Das ist ein seit den 2000ern aktiv beforschtes Feld
(OT-Papers, dann CRDT-Papers wie RGA, WOOT, Logoot, Treedoc, bis hin zu Yjs' YATA und
[Automerge](05-alternativen.md)'s Ansätzen) — mit vielen dokumentierten Korrektheitsfallen, die
erst nach Jahren an Praxiseinsatz gefunden und gefixt wurden.

Wer eine eigene Lösung baut (egal auf welcher Basis — auch aktorbasierte Systeme wie Akka),
muss diesen Teil entweder selbst neu erfinden oder bewusst durch etwas Einfacheres ersetzen
(z.B. Server-seitiges Locking, "letzter Schreiber gewinnt", oder ein simples eigenes OT für
den eigenen Anwendungsfall) — mit den jeweiligen Einschränkungen, die CRDTs gerade vermeiden
sollen (kein Offline-Modus, kein Peer-to-Peer, potenziell verlorene Änderungen bei Konflikten).

Zwei konkrete Beispiele, was das in der Praxis bedeutet — beides Dinge, die man bei Yjs geschenkt
bekommt, bei einer Eigenentwicklung aber vollständig selbst entwerfen müsste:

### Server-seitiges Locking: der Preis der Zuverlässigkeit

Ein naheliegender Ersatz für echtes Co-Editing ist ein **Lock**: Feld wird für andere
schreibgeschützt, sobald jemand es öffnet. Das Problem dabei ist nicht die Sperre selbst,
sondern ihre Freigabe — ein **hartes Lock ("gesperrt bis explizit freigegeben")** ist nur so
zuverlässig wie die Fähigkeit, zu erkennen, dass der Halter weg ist. Bricht die Verbindung
mitten im Editieren ab (Internet weg, Rechner abgestürzt, Tab hart geschlossen), meldet sich
das per Definition nicht sauber ab — ohne Gegenmaßnahme bleibt das Feld für alle anderen für
immer gesperrt.

Die Standard-Lösung dafür ist ein **Lease statt eines Locks**: nicht "gesperrt bis freigegeben",
sondern "gesperrt für die nächsten paar Sekunden, muss aktiv erneuert werden (Heartbeat), sonst
verfällt es automatisch". Technisch lösbar, aber zusätzlicher, nicht-trivialer Code: Timeout
wählen, Erneuerung verdrahten, das Wettrennen zwischen "Lease läuft gerade ab" und "Nutzer tippt
noch" behandeln.

**Ein CRDT hat dieses Problem strukturell gar nicht.** Es gibt kein Lock, das hängen bleiben
könnte — bricht jemand mitten im Tippen weg, bleibt einfach stehen, was er bis dahin geschrieben
hat, nichts muss aufgeräumt werden. Diese ganze Fehlerklasse (Lease-Timeouts, Disconnect-Erkennung,
Race zwischen Ablauf und aktivem Tippen) existiert bei echtem Co-Editing schlicht nicht — das ist
kein Nebeneffekt, sondern einer der Kerngründe, warum CRDTs für UI-Kollaboration so gut passen.

Praktische Konsequenz für die UI, unabhängig davon: ein zeichengenauer Cursor anderer Nutzer
*innerhalb* eines Textfelds bräuchte ohnehin eine eigene Editor-Engine statt eines nativen
`<textarea>`/`<input>` (siehe [Fallstricke #6](03-fallstricke.md#6-die-fablejs-grenze-wo-typsicherheit-aufhört))
— ein einfaches Präsenz-Badge ("X tippt hier gerade") reicht dafür in der Praxis meist aus und
ist genau das Muster, das z.B. Google Sheets und Airtable beim Zellen-Editieren selbst verwenden,
statt eines Locks oder eines präzisen In-Text-Cursors.

### Undo/Redo: auch das müsste komplett selbst gebaut werden

Der `Y.UndoManager` aus [Fallstricke #Undo/Redo](03-fallstricke.md#was-man-sonst-vorher-wissen-sollte)
ist mehr als Komfort — korrektes Undo in einer Mehrbenutzer-Umgebung ist ein eigenständig
schwieriges Problem, das eng mit dem Merge-Algorithmus zusammenhängt, nicht getrennt davon
lösbar ist:

- Man muss jede Änderung ihrem Urheber zuordnen können (sonst lässt sich "nur meine letzte
  Änderung rückgängig machen" gar nicht ausdrücken).
- Man muss die *Umkehrung* einer Operation korrekt berechnen können, selbst wenn zwischenzeitlich
  andere Nutzer an derselben Stelle etwas geändert haben — ein naives "meine ursprüngliche
  Tastenfolge rückwärts abspielen" trifft nach fremden Zwischenänderungen leicht die falsche
  Position oder zerstört, was andere geschrieben haben. Undo in OT-Systemen gilt in der
  Forschung als einer der fehleranfälligsten Teile überhaupt, gerade weil es scheinbar einfach
  aussieht.
- Bei Yjs hängt das direkt an derselben Struktur, die auch den Merge korrekt macht (siehe YATA
  oben) — `UndoManager` nutzt sie einfach mit. Bei einer Eigenentwicklung (egal ob eigenes OT
  oder eigenes einfaches Locking-Schema) wäre das ein zweites, unabhängiges Forschungsproblem
  obendrauf, nicht mit erledigt.

## Wie reif/verbreitet ist Yjs wirklich?

Kurz: **sehr.** Das ist keine Nischen- oder Frickelbibliothek:

- Laut aktuellen Vergleichsartikeln ~920.000 wöchentliche npm-Downloads und ~17.000
  GitHub-Stars — nach übereinstimmenden Einschätzungen aktuell die am weitesten verbreitete
  CRDT-Bibliothek überhaupt. ([Quelle](https://www.pkgpulse.com/guides/yjs-vs-automerge-vs-loro-crdt-libraries-2026))
- **[JupyterLab](https://jupyterlab.readthedocs.io/en/stable/user/rtc.html)** nutzt Yjs seit
  Version 4 offiziell als Grundlage seiner Echtzeit-Kollaboration (Notebooks, Dateien,
  Cursor-Anzeige anderer Nutzer — inhaltlich sehr nah an dem, was dieser Prototyp zeigt). Der
  Erfinder von Yjs, Kevin Jahns, hat den entsprechenden
  [Blogpost im offiziellen Jupyter-Blog](https://blog.jupyter.org/how-we-made-jupyter-notebooks-collaborative-with-yjs-b8dff6a9d8af)
  selbst geschrieben. Serverseitig läuft dort mit `pycrdt` übrigens eine Python-Anbindung an
  dieselbe Rust-Implementierung (`y-crdt`/`yrs`), die auch [YDotNet](04-skalierung.md#server-wird-yjs-fähig-ydotnet--ycs)
  nutzt — dasselbe Protokoll, andere Sprache.
- **[Tiptap](https://tiptap.dev/docs/collaboration/getting-started/overview)** (ein sehr
  verbreitetes Rich-Text-Editor-Framework) hat Yjs-Kollaboration als offizielles Kernfeature;
  das dazugehörige Backend **[Hocuspocus](https://github.com/ueberdosis/hocuspocus)** wird nach
  eigenen Angaben von einer Demo bis zu tausenden gleichzeitigen Verbindungen eingesetzt.
- Editor-Anbindungen existieren offiziell für ProseMirror, CodeMirror, Monaco, Quill, Slate.
- **BlockSuite** (die Editor-Engine hinter dem Notion-artigen Open-Source-Tool **AFFiNE**)
  baut ebenfalls auf Yjs auf.

Das heißt nicht, dass Yjs alternativlos ist — siehe [Alternativen](05-alternativen.md) — aber
die Aussage "das ist unausgereift" lässt sich anhand dieser Faktenlage nicht halten.

## Trägt das langfristig? Wartung, Lock-in, Kosten

Drei Fragen, die ein vorsichtiger Entscheider zurecht stellt, bevor eine Abhängigkeit
langfristig eingeht wird — losgelöst davon, wie gut der eigene Prototyp ist:

**"Ein-Personen-Projekt — was, wenn der Maintainer abspringt?"** Berechtigt: Yjs wird primär
von einer Person entwickelt, [Kevin Jahns](https://github.com/dmonad). Das relativiert sich
aber bei genauerem Hinsehen: MIT-Lizenz (forkbar, keine rechtliche Abhängigkeit von einer
Person), eine transparente Förderorganisation drumherum
([Y-Collective](https://opencollective.com/y-collective), finanziert mehrere aktive
Contributor über die reine Kernbibliothek hinaus), und kommerzielle Unternehmen mit echtem
Eigeninteresse am Fortbestand (Tiptap/Hocuspocus baut sein gesamtes Geschäftsmodell darauf
auf). Aktuell fördern laut eigenen Angaben sogar **ZenDiS** (deutsches Zentrum für Digitale
Souveränität, hinter openDesk) und **DINUM** (französische Digitalbehörde, hinter La Suite
Docs) Yjs-Weiterentwicklung — kein Hobby-Projekt mehr, sondern mit digitaler Souveränität auf
Behördenebene verknüpft.

**"Kommen wir da später wieder raus, oder ist das Lock-in?"** Die Antwort steckt im
Architektur-Muster selbst, siehe [Architektur-Muster #9](02-architektur-muster.md#9-ein-reiches-domänenmodell-behalten-yjs-als-dünne-projektionsschicht):
weil die Yjs-Berührungsfläche bewusst auf eine dünne Interop-Schicht plus eine schmale
Projektion des Domänenmodells begrenzt bleibt, statt Yjs-Typen im ganzen Code zu verstreuen,
wäre ein späterer Wechsel deutlich kontrollierter als bei tief verwobenen Abhängigkeiten. Kein
Zero-Cost-Exit, aber auch kein Full-Rewrite.

**"Versteckte Kosten/Lizenzfallen?"** Yjs selbst: MIT-Lizenz, komplett kostenlos, keine
Stufen oder Limits. Was kommerziell existiert (Tiptap Cloud, Liveblocks, y-sweet) sind
**optionale** gehostete Infrastruktur-Angebote für alle, die nicht selbst hosten wollen — nie
eine Pflicht. Dieser Prototyp beweist das indirekt schon selbst: komplett selbst gehostet,
nichts davon bezahlt oder auch nur benötigt.

## Neutrale Einordnung: Yjs vs. eigenentwickelte (z.B. aktorbasierte) Lösung

Ein Actor-Framework wie Akka (oder Akka.NET, Orleans, …) ist ein exzellentes Werkzeug für
**verteilte Systeme im Allgemeinen**: Zustandsverwaltung über Knoten hinweg, Supervision/
Fehlerbehandlung, Backpressure, Clustering, Sharding. Das sind reale, schwierige Probleme, die
Akka gut löst.

Was ein Actor-Framework **nicht** mitbringt, ist ein Merge-Algorithmus für gleichzeitig
bearbeitete geteilte Daten. Akka sagt nichts darüber aus, *wie* zwei gleichzeitige Änderungen
an derselben Note/demselben Textabschnitt zusammengeführt werden sollen — das müsste komplett
selbst entworfen und implementiert werden (im Kern: entweder OT oder eine CRDT, s.o.), und zwar
mit derselben Sorgfalt, die in Yjs' YATA-Algorithmus über Jahre eingeflossen ist.

Das gilt selbst dort, wo Akka(.NET) bereits CRDTs mitbringt:
**[`Akka.DistributedData`](https://getakka.net/articles/clustering/distributed-data.html)**
liefert `GCounter`, `PNCounter`, `GSet`, `ORSet`, `ORDictionary`, `LWWRegister`,
`LWWDictionary` — Zähler, Mengen, Register, Maps. **Kein Sequenz-/Text-Typ.** Die Doku
positioniert das Modul explizit für hochverfügbare, verteilte Key-Value-Stores in
Cluster-Backends, nicht für Dokument-Kollaboration. Für einen Textkörper bliebe damit
bestenfalls ein `LWWRegister` übrig — "letzter Schreiber gewinnt, ganzer String" — exakt die
grobe Semantik, die `Y.Text` (siehe [Architektur-Muster](02-architektur-muster.md)) gerade
vermeidet. Akka *hat* also CRDTs, nur nicht die eine Sorte, die für kollaboratives
Text-Editieren gebraucht wird.

Dazu kommt ein praktischer Punkt: Actor-Frameworks dieser Art laufen serverseitig (JVM/.NET).
Es gibt keine praxistaugliche Variante, dieselben Actors direkt im Browser-Tab laufen zu lassen
und dort Peer-to-Peer oder Offline zu arbeiten — der Client bräuchte also ohnehin eine eigene,
separate Lösung für lokale Zwischenspeicherung und Konfliktbehandlung, während der
eigentlich schwierige Teil (der Merge) beim Server läge. Das führt in der Praxis meist zu einem
von zwei Ergebnissen: entweder jede Änderung braucht einen Server-Roundtrip, bevor sie als
"bestätigt" gilt (spürbar trägere UI, kein Offline-Modus), oder man baut sich am Ende doch
wieder eine CRDT- oder OT-ähnliche Logik selbst — nur ohne die Jahre an Praxiserfahrung, die in
Yjs bereits stecken.

Kurz: Akka (o.ä.) und Yjs lösen unterschiedliche Probleme und schließen sich nicht gegenseitig
aus — ein Actor-System könnte z.B. sehr sinnvoll sein, um *viele parallele Yjs-Räume* auf einem
Cluster zu verteilen (siehe [Skalierung](04-skalierung.md)). Es ersetzt aber nicht den
CRDT-Algorithmus selbst.
