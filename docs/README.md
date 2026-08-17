# Echtzeit-Kollaboration mit CRDTs — Doku-Index

Diese Doku erklärt das **Muster**, nicht nur den Prototyp: Wie man Echtzeit-Kollaboration
(mehrere Personen bearbeiten gleichzeitig dieselben Objekte, sehen die Cursor der anderen,
Änderungen laufen ohne Konflikte zusammen) mit einer CRDT-Bibliothek wie Yjs baut. Alles hier
ist so geschrieben, dass es unabhängig vom konkreten Prototyp in diesem Repo verständlich ist
und sich auf andere Software übertragen lässt. Wo es konkret wird, verweist die Doku auf den
lauffähigen Prototyp im Repo-Wurzelverzeichnis ([../README.md](../README.md)) als Beispiel.

## Inhalt

1. **[CRDT & Yjs — Grundlagen](01-crdt-und-yjs.md)**
   Was ist ein CRDT, wie löst Yjs das Merge-Problem, wie reif/verbreitet ist die Bibliothek
   wirklich, und eine neutrale Einordnung gegenüber eigenentwickelten (z.B. aktorbasierten)
   Lösungen.

2. **[Architektur-Muster](02-architektur-muster.md)**
   Die übertragbaren Bausteine: Zwei-Kanal-Prinzip (Dokument vs. Awareness), der Server als
   "dummer Relay", Dokumentstruktur, Integration in ein UI-Framework, Weltkoordinaten für
   geteilte Cursor.

3. **[Fallstricke & Lessons Learned](03-fallstricke.md)**
   Was beim Einbau typischerweise schiefgeht (inkl. zweier echter Bugs aus diesem Projekt),
   und was man vorher wissen sollte, bevor man loslegt.

4. **[Skalierung](04-skalierung.md)**
   Was bei mehr Elementen/Usern gar kein Problem ist, wo es eng wird, Lösungsansätze —
   inklusive eines Abschnitts dazu, wie man den Server selbst Yjs-fähig macht (YDotNet / Ycs).

5. **[Alternativen zu Yjs](05-alternativen.md)**
   Automerge, OT-basierte Systeme, aktorbasierte Eigenbauten — Vergleich und Einordnung,
   wann welcher Ansatz Sinn ergibt.

## Für Eilige

- **CRDT in einem Satz**: Datenstrukturen, die so gebaut sind, dass zwei unabhängig
  geänderte Kopien sich *immer* automatisch und ohne zentrale Instanz zu einem eindeutigen
  Ergebnis zusammenführen lassen — siehe [01](01-crdt-und-yjs.md).
- **Warum nicht selbst bauen**: der schwierige Teil ist nicht "Änderungen verteilen", sondern
  der Merge-Algorithmus selbst (besonders für Text) — das ist jahrelange, publizierte
  Forschungsarbeit, die Yjs bereits enthält — siehe [01](01-crdt-und-yjs.md#warum-ist-das-schwer-selbst-zu-bauen).
- **Wo es beim Einbau typischerweise klemmt**: siehe [03](03-fallstricke.md).
- **Skaliert das?**: ja, bis auf einen Punkt (Log-Wachstum über die Zeit) — siehe [04](04-skalierung.md).
