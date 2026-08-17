# Fallstricke & Lessons Learned

Die ersten drei Punkte sind reale Bugs, die beim Bau dieses Prototyps aufgetreten sind — nicht
weil Yjs fehlerhaft wäre, sondern weil die *Integration* zwischen CRDT und UI-Framework an
diesen Stellen nicht offensichtlich ist. Sie sind hier bewusst verallgemeinert, weil sie in
praktisch jeder Yjs-Integration in derselben Form wieder auftauchen können.

## 1. Echo-Race zwischen eigenem Edit und CRDT-Echo

**Symptom im Prototyp**: beim Tippen in einer Note sprang der Cursor gelegentlich nicht mit,
Buchstaben landeten an der falschen Stelle.

**Ursache, verallgemeinert**: Eine lokale Änderung läuft typischerweise so:

```
Nutzer tippt → dispatch(Edit) → Mutation gegen Y.Doc → Y.Doc feuert seinen
eigenen "changed"-Observer SYNCHRON → dispatch(DocChanged) → State wird aktualisiert
```

Zwischen dem ersten und letzten Schritt gibt es (je nach Framework) einen Zwischenzustand, in
dem die UI bereits die neue Eingabe zeigt (z.B. weil der Browser das `<textarea>` nativ schon
aktualisiert hat), der zentrale State aber noch den *alten* Wert hat. Jede Logik, die in diesem
Moment versucht, die UI "zur Sicherheit" auf den (noch alten) State-Wert zu patchen, zerstört
dabei die gerade gemachte Eingabe — die kommt zwar eine Iteration später per `DocChanged` wieder
rein, aber der Cursor ist dann an der falschen Stelle.

**Lösung**: Der UI-Layer muss sich merken, was er selbst zuletzt an das Dokument gemeldet hat,
und darf so lange nicht gegen den zentralen State abgleichen/patchen, bis genau dieser Wert dort
auch wieder ankommt. Erst danach ist eine Abweichung wirklich eine *fremde* Änderung, die
eingepflegt werden muss. Siehe die `pendingSelfText`-Logik in [View.fs](../client/View.fs).

## 2. Hochfrequenter, flüchtiger State treibt teure UI-Neuaufbauten

**Symptom im Prototyp**: das Avatar-Symbol eines anderen Nutzers "hüpfte" beim Hovern, Klicks
darauf wurden manchmal ignoriert.

**Ursache, verallgemeinert**: Awareness-Daten (Cursor-Position) ändern sich sehr oft — potenziell
bei jeder Mausbewegung, auch wenn die Maus nur über die UI-Chrome (Werkzeugleiste, Panels)
bewegt wird, nicht nur über die eigentliche Arbeitsfläche. Wenn UI-Elemente, die von diesem
State *gar nicht inhaltlich betroffen sind* (z.B. eine Präsenz-Leiste, die sich nur ändert, wenn
sich *wer* online ist ändert, nicht *wo* der Cursor gerade ist), bei jeder dieser Änderungen neu
aus dem DOM/Widget-Baum aufgebaut werden, verliert das UI-Element ständig seine Identität — mit
der Folge, dass Hover-Zustände flackern und Klick-Events (die auf Mousedown+Mouseup auf
*demselben* Element beruhen) verloren gehen, weil das Element dazwischen ausgetauscht wurde.

**Lösung**: Rendering von UI-Bereichen, die von hochfrequentem State beeinflusst werden könnten,
an eine **Signatur/einen Diff-Guard** koppeln — nur neu aufbauen, wenn sich die für *dieses*
Element tatsächlich relevanten Fakten geändert haben, nicht bei jeder Zustandsänderung
irgendwo im System. Siehe `renderPresence` in [View.fs](../client/View.fs).

## 3. Netzwerk-Throttling vs. lokale Reaktivität

**Ausgangsfrage, die zu diesem Punkt führte**: "Wenn ich das Senden drossle, ruckelt dann nicht
die UI?"

**Antwort/Lösung, verallgemeinert**: Nein — wenn man zwei Dinge sauber trennt, die leicht an
derselben Stelle zusammenlanden:

- **Was der Nutzer lokal sieht** (die eigene gezogene Note, der eigene Textcursor) muss mit
  voller Eingaberate reagieren — das ist eine rein lokale Zustandsänderung, kein Netzwerk
  beteiligt.
- **Was übers Netzwerk geht** (der eigentliche CRDT-Commit + Broadcast) kann gedrosselt werden,
  ohne dass es auffällt — ~20–25 Aktualisierungen/Sekunde wirken für das menschliche Auge bereits
  wie flüssige Bewegung. Der letzte Zwischenstand (z.B. beim Loslassen der Maus) muss dabei
  garantiert immer noch übertragen werden, auch wenn er zufällig ins Drossel-Fenster fällt.

Analog gilt fürs Zeichnen: Neuzeichnen an die Bildwiederholrate koppeln (`requestAnimationFrame`)
statt bei jeder einzelnen Zustandsänderung sofort zu malen — das macht nie etwas langsamer,
da mehr Repaints als die Bildwiederholrate ohnehin nie sichtbar wären.

## 4. Origin-Tagging nicht vergessen (Echo-Schleifen)

Wenn ein empfangenes Update auf das lokale `Y.Doc` angewendet wird, feuert das denselben
"changed"-Observer, der auch für lokale Änderungen zuständig ist. Ohne Gegenmaßnahme würde ein
Client jedes von einem anderen Client empfangene Update sofort wieder an den Server zurücksenden
— eine Verstärkungsschleife. Yjs' `applyUpdate(doc, update, origin)` nimmt dafür einen dritten
Parameter, eine beliebige Markierung ("Origin"); im "changed"-Handler wird dann nur weitergesendet,
wenn die Änderung **nicht** von dieser Markierung stammt. Siehe `remoteOrigin` in
[Interop/Yjs.fs](../client/Interop/Yjs.fs).

## 5. Fable/.NET-spezifisch: gleich aussehende NuGet-Pakete sind nicht gleich

Nicht CRDT-spezifisch, aber beim Bau dieses Prototyps aufgetreten und für jeden relevant, der
F#+Fable einsetzt: Manche Pakete existieren in zwei Varianten mit fast identischem Namen — eine
rein für .NET kompilierte Variante (nur DLLs im Paket) und eine für Fable gedachte Variante (die
den F#-*Quelltext* im Paket mitliefert, weil Fable aus Quelltext nach JS übersetzt, nicht aus
DLLs). Die falsche Variante kompiliert und läuft im .NET-Build anstandslos durch — der Fehler
zeigt sich erst beim JS-Kompilierschritt, als kryptischer "Funktion X ist nicht definiert" zur
Laufzeit im Browser. Konkret in diesem Projekt: `Elmish` (nur DLLs) vs. `Fable.Elmish` (mit
Quelltext) — siehe [Client.fsproj](../client/Client.fsproj).

## Was man sonst vorher wissen sollte

- **Awareness-Protokoll**: Yjs bringt mit `y-protocols/awareness` ein offizielles,
  ausgereiftes Awareness-Protokoll mit (Timeout-Handling, kompakte Kodierung). Dieser Prototyp
  verwendet bewusst simples, lesbares JSON statt dessen — gut für Nachvollziehbarkeit in einer
  Demo, für ein Produktivsystem eher das offizielle Protokoll in Betracht ziehen, v.a. wenn man
  mit bestehenden Yjs-Providern (`y-websocket`, `y-webrtc`) zusammenarbeiten will.
- **Undo/Redo**: Yjs bringt mit `Y.UndoManager` einen fertigen, CRDT-bewussten Undo-Stack mit —
  nicht selbst nachbauen. Man instanziiert ihn gegen einen bestimmten Scope (einen einzelnen
  geteilten Typ oder mehrere, z.B. die `Y.Map` eines Objekts oder gleich die ganze
  Dokument-Wurzel) und ruft `undo()`/`redo()` auf. Der interessante Teil: er nutzt denselben
  Origin-Mechanismus wie das [Origin-Tagging gegen Echo-Schleifen](#4-origin-tagging-nicht-vergessen-echo-schleifen)
  (Option `trackedOrigins`, standardmäßig nur Änderungen ohne explizite Origin, also die eigenen
  lokalen) — dadurch macht `undo()` **immer nur die eigene letzte Änderung** rückgängig, egal was
  andere Nutzer zwischenzeitlich geändert haben, und zwar strukturell korrekt statt "ganzen
  Abschnitt auf alten Stand zurücksetzen". Das ist auch bewusstes Design, nicht nur Technik:
  in Kollaborationssoftware soll Undo i.d.R. nicht fremde Änderungen zurücknehmen können.
  Weitere eingebaute Details: schnell aufeinanderfolgende Änderungen werden über ein Zeitfenster
  (`captureTimeout`, Default 500ms) zu einem Undo-Schritt zusammengefasst; an jedem Undo-Schritt
  lassen sich Zusatzdaten wie die Cursor-Position speichern und beim Undo wiederherstellen; und
  `undo()` selbst erzeugt einfach eine normale Transaktion, die wie jede andere Änderung an alle
  anderen Clients gebroadcastet wird — kein Sonderprotokoll nötig
  ([Quelle](https://docs.yjs.dev/api/undo-manager)).
- **Rich-Text-Editoren**: Für echte Editoren (nicht ein einfaches `<textarea>` wie in diesem
  Prototyp) gibt es offizielle, ausgereifte Bindings: `y-prosemirror`, `y-codemirror`,
  `y-monaco`, `y-quill`, sowie Tiptaps eingebaute Yjs-Kollaboration. Diese kümmern sich u.a.
  korrekt um IME-Komposition (z.B. asiatische Eingabemethoden), Cursor-in-Text-Anzeige anderer
  Nutzer, und Undo — die selbstgebaute Prefix/Suffix-Diff-Logik dieses Prototyps ist eine für
  eine Demo angemessene Vereinfachung, aber **nicht** empfehlenswert für einen echten Editor.
  Besonders bei `contenteditable`-basierten Editoren (statt `<textarea>`) wird das schnell
  heikel: `contenteditable` hat ein *eigenes* natives Undo, das auf rohen DOM-Mutationen
  basiert statt auf einem einfachen String+Cursor-Offset — patcht man den Inhalt gleichzeitig
  programmatisch (wegen eingehender Remote-Updates), gerät dieser native Undo-Stack leicht
  durcheinander. Offizielle Bindings wie `y-prosemirror` lösen das, indem sie natives Undo im
  editierbaren Bereich bewusst abschalten und alles, inklusive Undo, exklusiv durch Yjs'
  `UndoManager` leiten.
- **Auth/Zugriffsschutz**: Yjs selbst hat keine Meinung zu Authentifizierung — das muss auf der
  Transport-/Raum-Ebene selbst gelöst werden (siehe auch [Skalierung](04-skalierung.md)).
- **Große Dokumente**: `Y.Doc` unterstützt verschachtelte Sub-Dokumente, um Teile eines sehr
  großen Dokuments erst bei Bedarf zu laden — relevant, falls die geteilte Struktur mal deutlich
  über hunderte Elemente hinauswächst.
