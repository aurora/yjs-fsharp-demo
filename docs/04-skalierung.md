# Skalierung

Kurzfassung vorweg: Bei einigen hundert Elementen und einigen zehn gleichzeitigen Nutzern ist so
gut wie nichts hiervon spürbar. Die Punkte unten sind dafür da, zu wissen, *worauf* man achten
sollte, bevor es zum Problem wird — nicht als Warnung, dass es bald eng wird.

> **Ehrlichkeitshinweis**: die Fixes in diesem Prototyp (Drag-Throttling, an
> `requestAnimationFrame` gekoppeltes Zeichnen, paralleler Server-Broadcast) sind begründet und
> mit zwei Browser-Tabs verifiziert — nicht mit zehn oder mehr echten gleichzeitigen Clients
> unter Last gemessen. Das Design ist vernünftig hergeleitet, aber empirisch nur im Kleinen
> geprüft. Für eine belastbare Aussage bei echter Mehrbenutzerlast bräuchte es einen
> Lasttest-Aufbau (mehrere simulierte WebSocket-Clients, die gleichzeitig Notes bewegen/tippen).

## Was praktisch nie zum Problem wird

- **Yjs' eigener Merge-Algorithmus.** Yjs ist für große Dokumente und hohe Änderungsraten
  ausgelegt und benchmarkt (Größenordnung 26.000–156.000 Operationen/Sekunde je nach
  Szenario, [Quelle](https://www.pkgpulse.com/guides/yjs-vs-automerge-vs-loro-crdt-libraries-2026)).
  Für UI-Objektzahlen im zwei- bis dreistelligen Bereich ist das nicht der Engpass.
- **WebSocket-Verbindungen selbst.** Moderne Server (Kestrel/ASP.NET Core eingeschlossen)
  verkraften mühelos tausende gleichzeitige Verbindungen pro Prozess.
- **Rendering hunderter Elemente** in Canvas oder DOM — das ist Größenordnung, mit der auch
  einfache 2D-Engines/Widget-Bäume klarkommen, solange man nicht bei *jeder* Zustandsänderung
  irgendwo im System blind *alles* neu zeichnet/neu aufbaut (siehe
  [Fallstricke #2](03-fallstricke.md#2-hochfrequenter-flüchtiger-state-treibt-teure-ui-neuaufbauten)).
- **Präsenz-/Awareness-Daten** bei zehn, zwanzig, auch fünfzig gleichzeitigen Nutzern — das ist
  ein kleines JSON-Objekt pro Nutzer pro Bewegung, kein relevantes Datenvolumen.

## Was zum Problem werden kann — und wie man es löst

### 1. Der Update-Log wächst unbegrenzt — mit der Zeit, nicht mit der Elementzahl

Das ist der wichtigste Punkt. Ein "dummer Relay-Server" (siehe
[Architektur-Muster](02-architektur-muster.md#2-der-server-als-dummer-relay--log)) speichert
jedes Update für immer und spielt bei jeder neuen Verbindung die *komplette* Historie ab. Das
Problem ist nicht "wie viele Elemente gibt es", sondern "wie viele Änderungsoperationen gab es
insgesamt in dieser Sitzung" — ein einzelner Drag-Vorgang kann, wenn man nicht aufpasst, hunderte
Mini-Updates erzeugen (eines pro Mausbewegung). Über eine lange Sitzung wächst der Log dadurch
unbegrenzt, und Beitretende laden zunehmend mehr Historie, bevor sie den aktuellen Stand haben.

**Lösungsansätze, aufsteigend im Aufwand:**

- *Sofort und ohne Zusatzaufwand*: hochfrequente lokale Änderungen (Drag, Resize) client-seitig
  drosseln, bevor sie überhaupt committet werden — reduziert die Wachstumsrate direkt, ohne den
  Server anzufassen.
- *Ohne neue Server-Abhängigkeit*: einen verbundenen Client periodisch bitten, den kompletten
  aktuellen Zustand als **ein** zusammengeführtes Update zu senden (`Y.encodeStateAsUpdate`),
  und damit den serverseitigen Log zu ersetzen. Der Server bleibt "dumm" — er tauscht nur
  viele kleine Einträge gegen einen großen aus, ohne den Inhalt zu verstehen.
- *Server wird selbst Yjs-fähig*: siehe [nächster Abschnitt](#server-wird-yjs-fähig-ydotnet--ycs).

### 2. Naives Broadcasting

Ein Server, der bei N verbundenen Clients ein eingehendes Update nacheinander (sequenziell
`await`) an alle anderen sendet, summiert bei wachsendem N Latenz auf. Lösung: parallel
verschicken (`Task.WhenAll` o.ä.) — **aber Vorsicht**: die meisten WebSocket-Implementierungen
(inkl. .NETs) erlauben keine zwei gleichzeitigen Sende-Operationen auf demselben Socket. Sends
an dieselbe Zielverbindung müssen weiterhin serialisiert werden (z.B. über ein Semaphore pro
Verbindung), auch wenn sie von unterschiedlichen eingehenden Nachrichten ausgelöst wurden. Siehe
`broadcast`/`SendLock` in [Rooms.fs](../server/Rooms.fs).

### 3. Ein Prozess, viele Räume — und irgendwann viele Prozesse

Ein einzelner Server-Prozess kann viele unabhängige "Räume" gleichzeitig bedienen (jeder mit
eigenem Log/Verbindungsliste). Sobald die Last über einen einzelnen Prozess hinauswächst und
horizontal skaliert werden muss (mehrere Server-Instanzen hinter einem Load Balancer), reicht
das In-Memory-Modell nicht mehr — zwei Clients desselben Raums könnten dann auf unterschiedlichen
Prozessen landen. Das braucht dann eine gemeinsame Verteil-Schicht zwischen den Prozessen (z.B.
Redis Pub/Sub, NATS) — dasselbe Muster, das z.B. SignalR mit seinem "Backplane"-Konzept für
denselben Zweck nutzt. Für die meisten Anwendungsfälle (auch mit hunderten gleichzeitigen
Nutzern) ist ein einzelner Prozess aber lange ausreichend — das ist ein Punkt, den man kennen
sollte, aber selten sofort braucht.

## Server wird Yjs-fähig: YDotNet / Ycs

> Betrifft nur den **Server**. Für die Frage, ob diese beiden auch dem Client hin zu mehr
> Typsicherheit gegenüber der JS-Yjs-Bibliothek verhelfen können, siehe
> [Fallstricke #6](03-fallstricke.md#6-die-fablejs-grenze-wo-typsicherheit-aufhört) — kurze
> Antwort: nicht ohne einen kompletten Wechsel von Fable zu Blazor WebAssembly.

Beide Optionen lösen das Log-Wachstum-Problem "richtig": der Server hält selbst ein echtes
Dokument, kann eingehende Updates darin mergen und jederzeit einen kompakten Gesamtzustand
(`EncodeStateAsUpdate`) statt der vollen Historie ausliefern. Neu Beitretende laden dann
O(aktueller Zustand) statt O(gesamte Historie). Zusätzlich werden dadurch state-vector-basierte
Differenz-Syncs möglich (ein Client sagt "das habe ich schon", Server schickt nur das Delta) und
eine sinnvolle Persistenz auf Platte/DB (regelmäßig den kompakten Zustand wegschreiben statt
entweder gar nichts oder einen unbegrenzt wachsenden Log).

Es gibt für .NET zwei Wege dahin, mit unterschiedlichen Kompromissen:

| | **[YDotNet](https://github.com/SebastianStehle/ydotnet)** | **[Ycs](https://github.com/yjs/ycs)** |
|---|---|---|
| Basis | Bindings an `yrs` (Rust-Portierung von Yjs) | Eigenständige, vollständig verwaltete C#-Portierung |
| Abhängigkeit | native, plattformspezifische Binary (FFI) | reines .NET, keine native Abhängigkeit |
| Vollständigkeit | Y.Map, Y.Array, Y.Text, Y.XmlFragment | Y.Map, Y.Array, Y.Text (noch **kein** Y.Xml) |
| Träger | Community-Projekt (Sebastian Stehle) | im offiziellen `yjs`-GitHub-Namespace |
| Reifegrad | selbst als früh/API-instabil deklariert (Version < 1.0) | ebenfalls noch vor Version 1.0 |
| Passt gut, wenn … | volle Yjs-Feature-Parität wichtiger ist als eine reine .NET-Toolchain | die "alles ist nur .NET/dotnet build"-Eigenschaft (wie in diesem Prototyp) erhalten bleiben soll |

Beide Projekte sind noch vor Version 1.0 und bezeichnen sich selbst als "früh" bzw. mit
möglichen API-Änderungen — vor einem Produktiveinsatz lohnt sich ein Blick auf den aktuellen
Entwicklungsstand, unabhängig davon, welche der beiden Optionen man wählt.

**Aufwand gegenüber dem "dummen Relay"-Muster**: spürbar höher — der Server muss dann pro Raum
ein lebendiges Dokumentobjekt verwalten (inkl. Thread-Sicherheit bei gleichzeitigen Zugriffen
aus mehreren Verbindungs-Handlern) statt nur eine Liste von Byte-Arrays. Das ist der Preis für
die Kompaktierung — und der Grund, warum dieser Prototyp bewusst beim einfacheren Muster
geblieben ist, wo für die reine Yjs-Demonstration kein Mehrwert entstanden wäre.
