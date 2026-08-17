# Alternativen zu Yjs

Yjs ist für den hier behandelten Anwendungsfall (Echtzeit-Kollaboration im Browser, geteilte
Cursor, gleichzeitiges Bearbeiten) aktuell die am weitesten verbreitete und am besten mit
Editoren/Frameworks integrierte Lösung (siehe [Kapitel 1](01-crdt-und-yjs.md)) — aber nicht die
einzige. Hier eine neutrale Einordnung.

## Vergleich

| | **Yjs** | **[Automerge](https://automerge.org/)** | **OT (z.B. [ShareDB](https://github.com/share/sharedb))** | **Eigenbau auf Actor-Framework (Akka o.ä.)** |
|---|---|---|---|---|
| Merge-Algorithmus eingebaut? | ✅ (CRDT) | ✅ (CRDT) | ✅, aber je Datentyp selbst implementiert/konfiguriert | ❌ — müsste komplett selbst entworfen werden |
| Offline-/Peer-to-Peer-fähig? | ✅ | ✅ | ⚠️ nur mit erheblichem Zusatzaufwand, zentraler Server nötig | abhängig vom Eigenbau — typischerweise ❌ |
| Läuft nativ im Browser? | ✅ (JS/TS-Bibliothek) | ✅ (Rust+Wasm-Kern mit JS-Bindings) | ✅ (JS-Bibliotheken) | ❌ — Client bräuchte ohnehin eine separate Lösung |
| Editor-/Framework-Ökosystem | sehr groß (ProseMirror, CodeMirror, Monaco, Quill, Slate, Tiptap, …) | wachsend, kleiner als Yjs | vorhanden (v.a. ShareDB + eigene Text-OT), etwas älter | keins — alles selbst zu bauen |
| Verbreitung/Reife | ~920k wöchentl. npm-Downloads, ~17k GitHub-Stars ([Quelle](https://www.pkgpulse.com/guides/yjs-vs-automerge-vs-loro-crdt-libraries-2026)) | ~85k wöchentl. Downloads ([Quelle](https://www.pkgpulse.com/guides/yjs-vs-automerge-vs-loro-crdt-libraries-2026)), akademisch sehr fundiert (Ink & Switch / Martin Kleppmann) | historisch sehr bewährt (Google Docs, Etherpad-Familie) | abhängig von der eigenen Implementierung |
| Lizenz | MIT | MIT | größtenteils MIT/Apache (variiert je Projekt) | — |

## Kurzcharakteristik

**[Automerge](https://automerge.org/)** ist die akademisch profilierteste Alternative — entwickelt
im Umfeld von [Ink & Switch](https://www.inkandswitch.com/) und Martin Kleppmanns
["Local-First Software"](https://www.inkandswitch.com/essay/local-first/)-Arbeit, die die
gesamte "Local-First"-Bewegung mitgeprägt hat. Datenmodell-Philosophie: eher ein vollständiges,
versioniertes JSON-Dokument mit Git-artiger Änderungshistorie, während Yjs stärker auf schlanke,
für UI-Zustände typisierte Shared-Types (Map/Array/Text) optimiert ist. Frühere Automerge-Versionen
waren bei reinen Text-Workloads deutlich langsamer als Yjs; die Rust-basierte Automerge-3.x-Reihe
hat diesen Abstand laut aktuellen Vergleichen stark verkleinert. Für Anwendungsfälle, bei denen
die volle Änderungshistorie/Versionierung selbst ein Feature ist (nicht nur Konfliktfreiheit),
ist Automerge oft die passendere Wahl.

**OT-basierte Systeme (z.B. ShareDB)** sind der historisch ältere, sehr bewährte Ansatz (u.a.
die Grundlage klassischer Google-Docs- und Etherpad-Implementierungen). Reifer Track Record für
reine Text-Kollaboration mit zentralem Server. Der strukturelle Nachteil gegenüber CRDTs bleibt:
ohne zentralen Server keine Zusammenführung, Offline-Unterstützung ist ein nachträglich
aufgesetztes Zusatzproblem statt einer eingebauten Eigenschaft, und der Transform-Algorithmus
muss pro Datentyp neu und sorgfältig entwickelt werden.

**Eigenbau auf einem Actor-Framework (Akka, Akka.NET, Orleans, …)** löst ein anderes Problem:
verteilte Zustandsverwaltung, Fehlerbehandlung, Skalierung über Knoten — nicht den
Merge-Algorithmus selbst. Details und eine ausführlichere Einordnung dazu in
[Kapitel 1](01-crdt-und-yjs.md#neutrale-einordnung-yjs-vs-eigenentwickelte-zb-aktorbasierte-lösung).
Sinnvoll kombinierbar mit Yjs (z.B. um viele Yjs-"Räume" über einen Cluster zu verteilen, siehe
[Skalierung](04-skalierung.md#3-ein-prozess-viele-räume--und-irgendwann-viele-prozesse)), aber
kein Ersatz dafür.

**Gehostete/verwaltete Alternativen** (nicht direkt vergleichbar, sondern "kaufen statt bauen"):
Dienste wie Liveblocks oder PartyKit bieten Echtzeit-Kollaborationsinfrastruktur als Managed
Service an — teils CRDT-basiert, teils mit einfacheren Konsistenzmodellen. Relevant als
Kompromiss, wenn der eigene Betrieb der Infrastruktur (Server, Skalierung, Persistenz) vermieden
werden soll, bringt dafür eine Vendor-Abhängigkeit mit.

> **Randnotiz, Stand der Recherche**: es gibt ein sehr junges NuGet-Paket namens
> [`Crdt`](https://github.com/marcschier/crdt), dessen Beschreibung tatsächlich dieselben
> Sequenz-Algorithmen-Familien wie Yjs auflistet (u.a. `Rga<T>`, `YataSequence<T>`,
> `WootSequence<T>`) — auf dem Papier also eine native .NET-CRDT-Text-Implementierung. In der
> Praxis: 1 GitHub-Stern, 0 Forks, ein einzelner Autor, erste Version vor wenigen Wochen
> veröffentlicht, keine erkennbare Produktionsnutzung. Erwähnenswert als "das gibt es
> jetzt zumindest dem Namen nach", aber (noch) keine Alternative, der man denselben
> Vertrauensvorschuss geben könnte wie einer zehn Jahre lang produktiv gehärteten Bibliothek —
> siehe auch [Fallstricke #6](03-fallstricke.md#6-die-fablejs-grenze-wo-typsicherheit-aufhört).

## Fazit

Für die konkrete Zielsetzung dieses Projekts — Echtzeit-UI-Kollaboration im Browser mit geteilten
Cursorn, Follow-Mode und gleichzeitigem Bearbeiten strukturierter wie Text-Inhalte — ist Yjs
aktuell die am weitesten erprobte und am besten unterstützte Lösung. Automerge ist die
ernstzunehmendste CRDT-Alternative mit anderem Schwerpunkt (Versionierungshistorie statt
schlanker UI-Shared-Types). OT-Systeme und aktorbasierte Eigenbauten adressieren andere bzw.
vorgelagerte Probleme und würden für diesen Anwendungsfall bedeuten, den eigentlich schwierigen
Teil — den Merge-Algorithmus — selbst zu entwickeln, statt eine Lösung mit jahrelanger
Praxiserfahrung zu nutzen.
