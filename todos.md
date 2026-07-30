# PhoenixWrightTrilogy Accessibility Mod — TODOs

Arbeitsprotokoll nach dem Muster von `Disco-A11y/todos.md`. Offene Punkte aus Janas
Test-Sessions als J-Nummern; Erledigtes bleibt abgehakt als Verlauf stehen.

## 30.07.2026 — J8: "Zahlen-Sprech" des Regisseurs für Screenreader zurückübersetzen

Jana (aus Bug 3, „vor allem die Dialoge vom Regisseur"): Buchstaben werden durch
Zahlen ersetzt („Gef4hr" = „Gefahr"). **Kein Dekodierfehler** — das ist die absichtliche
Leetspeak-Stilisierung des Regisseurs (Sal Manella) in der Vorlage. Für Sehende lesbar,
für den Screenreader Kauderwelsch → echtes A11y-Problem.

**Fix (`Utilities/TextNormalizer.FixLeetDigits`, eingehängt in `DialoguePatches.TryOutputDialogue`
direkt nach `CombineLines`):** Ersetzt Leet-Ziffern durch Buchstaben, aber NUR innerhalb
eines Wortes (Buchstaben-/Ziffernblock mit ≥2 Buchstaben). Echte Zahlen (Uhrzeiten
„14:24", Daten „22.", „Studio 1", reine Ziffernblöcke) bleiben unangetastet, weil deutsche
Wörter nie Ziffern im Inneren haben. Greift für beide Ausgabepfade + Speicherung.

**Mapping — nur 4→a ist durch Janas Beispiel BESTÄTIGT**, der Rest ist Standard-Leet und
geraten: 0→o, 1→i, 3→e, **4→a**, 5→s, 7→t, 8→b, 9→g. 2 und 6 (mehrdeutig) bewusst
ausgelassen. Die Sicherheitsregel verhindert, dass Normaltext beschädigt wird; ein falsch
geratenes Ziel (v. a. 1→i vs. l) beträfe nur die ohnehin stilisierten Regisseur-Wörter.
**Janas Gegentest offen:** Regisseur-Dialoge prüfen, ob alle Wörter korrekt lesbar werden;
falsche/fehlende Ziffern melden → Mapping nachziehen. (Alternativ könnte ich die exakte
Ersetzungstabelle offline aus seinen Dialogen dekodieren, wenn das Spiel frei ist.)

## 30.07.2026 — J7: identische Doppel-Punkte (Studio-Van), via F9 (Bug 4) gefunden

Jana: „Punkt 4 und 5 sind beide der Van. Stimmt das?" — Bug 4: GS1 scenario 9, `bg_no=25`
(Global-Studios-Haupttor). Befund in `Sce2_4_room002`: ZWEI INSPECT_DATA-Einträge mit
`msg=154` und **exakt identischen** Koordinaten (center (1606,569), gleiches Viereck).
Screenshot zeigt genau EINEN Van rechts. Also ein echtes Duplikat in den Spieldaten —
für Sehende unsichtbar, für die Navigation zwei identische „Studio-Van"-Punkte.

**Fix (`HotspotNavigator.RefreshHotspots`):** Beim Aufbau der Liste einen Punkt
überspringen, wenn schon einer mit gleicher Nachricht UND (nahezu) gleicher Position
(<15 px) existiert. Gleiche Nachricht an VERSCHIEDENEN Positionen (dasselbe Objekt aus
zwei Blickwinkeln) bleibt bewusst als zwei Punkte erhalten. Build grün. **Janas
Gegentest steht aus** (greift erst nach Spiel-Neustart).

## 29.07.2026 — J6: Punkt 5 steuert immer Punkt 6 an (Überlappung), via F9 gefunden

**Von Jana beim Testen gefunden und per F9 gespeichert** (2 Bug-Berichte, gleicher Ort
Inspektorenbüro/Global-Studios-Eingang, `bg_no=11`, scenario 5 und 7 — verschiedene
Tage). Symptom: Steuert sie Punkt 5 an, wird beim Untersuchen immer Punkt 6 ausgelöst.

**Ursache (aus den F9-Daten + inspect-tables.json bestätigt):** Der Mod setzt den Cursor
auf den **Schwerpunkt** des Punktes; das Spiel untersucht bei Enter den Hotspot unter dem
Cursor. In `Sce2_0_room005` liegt der Schwerpunkt von Punkt 5 (msg216, Zentrum (684,608),
breites Band) **innerhalb der Trefferfläche von Punkt 6** (msg219, Viereck ~450–745/565–870).
Beide Vierecke überlappen dort; das Spiel wählt den Hotspot mit dem **niedrigeren**
inspect-Index → msg219 statt msg216. Genau dieselbe Konstellation in scenario 7 (msg267/270).

**Fix (`HotspotNavigator.MoveCursorToCurrentHotspot`):** Statt stur den Schwerpunkt zu
nehmen, sucht `GetSafeCursorPoint` einen Cursorpunkt, der **nur** im Ziel-Viereck liegt
(in keinem fremden). Schwerpunkt frei → unverändert; sonst Abtastung vom Schwerpunkt
Richtung der vier Ecken (Even-Odd-Punkt-im-Viereck-Test, auch für konkave Flächen). Für
den Bug-Fall ergibt das ~(947,582), nur in msg216 → korrekt Punkt 5. Kein Treffer möglich
(Ziel ganz überdeckt) → Rückfall auf Schwerpunkt (nicht schlechter als bisher). Eckpunkte
dafür in `HotspotInfo` (X0..Y3) gespeichert. Build grün. **Janas Gegentest steht aus.**

## 26.07.2026 — GS2/GS3 offline NICHT zuverlässig benennbar (bestätigt, mit Beweis)

Versuch, GS2 mit dem Live-Kandidaten-Ansatz zu benennen — **gescheitert, aber jetzt
mit hartem Beweis, warum**. Reihenfolge der Funde:

1. **decode-missing.ps1 -Game 2**: 677 Punkte, nur 277 „mit Text", 400 ohne. Verdächtig.
2. **GS2-Szenariodateien sind anders benannt**: `scenarios 1` liefert dreiteilige Namen
   (`sc1_1_0`, `sc1_3_1`, `sc3_0_0` …), 22 Einträge — nicht das zweiteilige GS1-Schema.
3. **Kohärenz-Test (Sce1_0_room006, msg 297–300)**: ZWEI Szenarien (s3 und s7) decken
   ALLE 4 Nachrichten ab, mit **völlig verschiedenem** Inhalt (s3 Dialog, s7
   „Filmdekoration"-Untersuchung). Ein Raum wird im selben Fall mehrfach besucht →
   Kohärenz-Abstimmung disambiguiert NICHT. (Deckt sich mit alter Protokoll-Warnung.)
4. **Ground-Truth-Test gescheitert**: Die bekannten Kurain-Texte (msg 156 „Großer
   Felsen", 183 „Schriftrolle", 220 „Tatami") kommen in KEINEM mdt-Index an der
   erwarteten Stelle vor. s2/156 = „Ich… hätte es auch sein können" (Dialog).
5. **Viewer-Abbildung ist für Titel≠0 inkonsistent**: `mdtpath 1 S` (Titel 1 = GS2)
   liefert **GS1-Pfade** zurück (36 Einträge mit sc4-Varianten a–d = eindeutig GS1),
   während `scenarios 1` die richtige GS2-Tabelle (22, dreiteilig) gibt. Also mappen
   `mesraw`/`mdtpath` die Szenario-ID anders (über GS1?) als `scenarios`. **Kein
   verlässlicher Ground-Truth vorhanden** (die alten GS2/GS3-Kurznamen stammen vom
   buggy alten in-game-Dumper und taugen NICHT als Referenz).

**Fazit (endgültig):** Der Mod schlägt zur Laufzeit `s<global_work_.scenario>/<message>`
nach (HotspotNameService.cs:101/124). Bei GS1 ist `global_work_.scenario` == mdt-Index
(verifiziert → 511 Keys korrekt). Bei GS2/GS3 ist diese Gleichung **nicht gesichert**,
und der Debug-Viewer liefert für Titel≠0 keine vertrauenswürdige Szenario→Datei-Abbildung.
Offline benannte Keys könnten also am falschen Laufzeit-Szenario hängen → FALSCHE Namen.
**Kein Offline-Benennen von GS2/GS3** (Risiko „nichts erfunden"-Regel).

**Der tragfähige Weg (empfohlen): In-Game-Harvester.** Eine kleine Mod-Erweiterung, die
bei JEDER Untersuchung eines Hotspots im normalen Spielablauf automatisch
`s<global_work_.scenario>/<bg>/<message> = <exakter angezeigter Untersuchungstext>` in
eine Datei schreibt. Beide Werte sind dann GROUND-TRUTH (Laufzeit-Szenario + real
angezeigter Text), titel-unabhängig, korrekt per Konstruktion. GS2/GS3 (und zur
Gegenprobe GS1) füllen sich dann von selbst, während gespielt wird — kein Fokusproblem,
kein erzwungenes Durchspielen. Baustein liegt schon bereit: `DialoguePatches._lastAnnouncedText`
+ `HotspotNavigator` kennen Text und Punkt; der neue F9-BugReportService zeigt das Muster.
Offene Entscheidung für Jana: Harvester bauen (dann sammeln sich Namen beim Spielen) —
ja/nein.

**UMGESETZT (26.07.2026): Harvester gebaut.** `HotspotNameHarvester`
(`AccessibilityMod/Services/HotspotNameHarvester.cs`, in `OnUpdate` eingehängt) schreibt
beim Untersuchen eines Punkts eine Zeile nach
`UserData/AccessibilityMod/HarvestedNames/harvest.log`:
`GS<n> s<scenario>/<message> | bg=<bg> | <verbatim Text>`. Er nutzt den aktuell
gewählten Punkt (Jana navigiert mit den Mod-Tasten → Cursor synchron) und ein
Erntefenster von ~1,5 s um den Ermittlungsmodus (weil das Dialogfenster den Modus
kurz aussetzt). Dedup über Titel|scenario|message, auch über Sitzungen (liest die
Datei beim Start ein). **Schreibt NICHT in GS<n>_Hotspots.json** — reine Sammlung,
Zusammenführen bleibt Handarbeit.
- **VERIFIZIERT + Fehler gefunden + behoben (29.07.2026):** Janas erste Testsitzung
  erzeugte 50 GS1-Zeilen (scenario 5/7). Gegen die verlässliche GS1-Offline-Datei
  geprüft: die meisten stimmten exakt, ABER einige waren fehlgepaart (z. B. `s5/216`
  bekam Banter „He, wir Ermittler…" statt der Beschreibung; `s5/220` falsch; `s5/219`
  fehlte). Ursache: v1 schlüsselte über den vom Mod „aktuell" gehaltenen Punkt —
  bei mehrzeiligen Untersuchungen (Beschreibung + Banter) und bei Überlappung (J6)
  landete der falsche Satz unter dem Punkt.
- **Fix (v2):** Harvester schlüsselt jetzt über `message_work_.now_no` — die
  TATSÄCHLICH angezeigte Nachrichtennummer (Feld per Reflection im Spiel-Assembly
  gefunden). Damit bekommt JEDE Zeile ihre wahre Nummer; die Untersuchungsbeschreibung
  landet unter genau dem Schlüssel, den `HotspotNameService` nachschlägt. Keine
  Abhängigkeit mehr von `HotspotNavigator`/Cursor. Build grün. Alte (fehlerhafte)
  `harvest.log` archiviert als `harvest_buggy_pre-nowno.log`, damit der Dedup den
  Neustart nicht blockiert. **Janas Gegentest von v2 steht aus.**
- Danach: harvest.log sichten (jetzt korrekt-per-Konstruktion), verbatim in
  GS2/3_Hotspots.json übernehmen (`s<scenario>/<message>`), bauen, committen.

## 26.07.2026 — GS1-Restpunkte geprüft: GS1 ist praktisch VOLLSTÄNDIG

Die „~82 offenen GS1-Punkte" aus dem 24.07.-Protokoll waren **stark überschätzt**.
Spiel als Decoder gestartet (Bridge Port 48620, keine Tastendrücke, Spielstände
vorher gesichert nach `~/.claude/backups/pwaat-savedata/2026-07-26_00-41-41/`),
Kandidaten LIVE aus `scenarios 0` rekonstruiert statt aus der unbrauchbaren
`scenario-map.json`. Ergebnis der ehrlichen Analyse (`scratchpad/decode-missing-gs1.ps1`):

- **651 Punkt-Instanzen bereits benannt** (511 eindeutige `s<C>/<M>`-Schlüssel).
- Die „fehlenden" 106 zerfallen fast vollständig in **NICHT-benennbares**:
  - **~27 Terminator-Einträge `msg=65535`** (0xFFFF = Tabellenende-Sentinel, `place==uint.MaxValue`) — keine echten Punkte, der alte Dump zählte die Endmarken mit.
  - **36 `_usa`-Punkte** (Ep4 room006, englische Sprachvariante) — für den deutschen Mod irrelevant.
  - **36 `_ger`-Punkte** (Ep4 room006, Teile 0/2/4) — **waren längst benannt** unter `s18/220–226`, `s22/180–186`, `s28/182–188`; tauchten nur als „fehlend" auf, weil `inspect-tables.json` für die Sprachvarianten-Tabellen Episode/Teil nicht geparst hatte → leere Kandidaten. Gegengeprüft: alle 3 Blöcke existieren mit exaktem Text.
  - **1 Punkt `item!=255`** (Sce1_0_room001 msg=187 item=13) — Name kommt zur Laufzeit vom Beweisstück (Lösung 1), braucht keinen JSON-Eintrag.
  - **3 Punkte SYSTEM `msg=44`** (Ep3 room002 ×3, identisch) — laden aus der **verschlüsselten** `sys_mes_g.mdt`; offline nicht dekodierbar (`mesfile` scheitert, weil nur die Szenario-mdt beim Laden entschlüsselt wird). Generische Meldung, geringer Wert → **bewusst übersprungen**.
  - **1 Punkt `msg=256`** — liefert nur ein Anführungszeichen, Nicht-Punkt.
- **Echte Neuzugänge: nur 2 verbatim-Punkte** (Kandidaten-Fehltreffer, jetzt via
  Live-Kandidaten aufgelöst und eindeutig verifiziert): `s1/243` „(Hmm... Ich will
  wissen, was da drin ist...)" und `s29/225` „(Ich frage mich, was er vorhin
  geschrieben hat?)". In `GS1_Hotspots.json` ergänzt, Build grün.

**Konsequenz:** GS1 gilt als abgeschlossen. Was übrig ist (3 System-msg=44), ist
nur mit einem neuen Bridge-Befehl (System-mdt entschlüsselt laden) erreichbar und
kaum lohnend.

**Werkzeug-/Datenlage-Erkenntnisse (für GS2/GS3 wichtig):**
- Die im Repo liegende `AccessibilityMod/DevBridge/scenario-map.json` ist
  **unbrauchbar** — sie enthält nur `"candidates": <ZAHL>` (Anzahl), NICHT die
  Szenario-Indizes. Die echten Kandidaten-Arrays lagen im inzwischen gelöschten
  Scratchpad. **Lösung, die sich bewährt hat:** Kandidaten LIVE aus dem Bridge-
  Befehl `scenarios <title>` rekonstruieren: Tabelle `Sce<Ep>_<Teil>_room<N>` →
  Datei `sc<Ep>_<Teil>[a-d]` → alle passenden Pfad-Indizes. Das ist zuverlässiger
  als jede vorab gespeicherte Map und funktioniert für GS2/GS3 genauso.
- `decode-hotspot-texts.ps1` zeigt noch auf einen **toten Scratchpad-Pfad** für die
  scenario-map und die Ausgabe — vor Wiederverwendung Pfade fixen bzw. besser die
  Live-Kandidaten-Logik aus `decode-missing-gs1.ps1` übernehmen.

## 26.07.2026 — Bug-Melde-Taste (F9) für Janas Testsessions

Jana wollte beim Testen einen gefundenen Fehler per Tastendruck festhalten, damit
ich die genaue Szene bekomme und sie mir im Chat nur noch sagen muss, was hakt.

- **`BugReportService`** (`AccessibilityMod/Services/BugReportService.cs`): schreibt
  auf **F9** einen Schnappschuss nach `UserData/AccessibilityMod/BugReports/report.txt`
  (angehängt, fortlaufend nummeriert „=== BUG N ===") plus ein Bildschirmfoto
  `bug_N_<zeitstempel>.png` daneben. Erfasst: Spiel, Szenario, bg_no/bg_pos_x, aktiver
  Modus, zuletzt gesprochene Dialogzeile (`DialoguePatches._lastAnnouncedText`), und im
  Ermittlungsmodus die komplette Punkteliste mit dem exakten Namensschlüssel
  `s<scenario>/<message>` + Pfeil auf den Punkt, auf dem der Cursor stand. Jedes Feld in
  eigenem try/catch (wie Bridge-`state`), damit ein fehlender Wert nie die Erfassung
  verhindert. Bestätigung per Sprache „Bug N gespeichert".
- **F9** gewählt, weil Funktionstasten layout-unabhängig sind (keine QWERTZ-Falle wie
  `[ ]`) und F9 von keinem Modus belegt ist. Verdrahtet in `InputManager.ProcessInput()`.
- **`HotspotNavigator.GetCurrentIndex()`** ergänzt (öffentlicher Accessor für den
  aktuell gewählten Punkt).
- Lokalisierung `bug_report.saved` / `bug_report.error` in en + de; übrige Sprachen
  Fallback Englisch.
- Build grün (0 Fehler). **Janas Test steht aus** — F9 in einer Ermittlungsszene drücken,
  dann liegt der Bericht in `UserData/AccessibilityMod/BugReports/`.

## Offene Punkte

- [ ] **J3 Tastenbelegungs-Menü: zugewiesene Taste wird nicht angesagt** (19.07.2026).
  Symptom: Unter Optionen → Tastenbelegung wird nur der Funktionsname gesprochen, nicht
  die gebundene Taste. Ursache: Die Zeilen sind `optionSummaryLAKeyConfig`-Items;
  `GetOptionValue` kannte den Typ nicht → kein Wert. Fix: Branch in `GetOptionValue`
  liest das öffentliche Feld `current_key_code` und spricht den lokalisierten
  Tastennamen ("Bestätigen: Eingabe"); zusätzlich Postfix auf `ChangeKeyConfig`,
  damit nach dem Umbelegen die neue Taste angesagt wird (mit Dedup). Typ-Struktur per
  Reflection über die Spiel-DLL ermittelt (kein Decompiled-Ordner vorhanden).
  **Fix gebaut — Janas Gegentest steht aus.**
- [ ] **J4 Untersuchungspunkte heißen nur „Punkt 1, Punkt 2 …"** (19.07.2026).
  Befund: Die Spieldaten (`INSPECT_DATA`) enthalten NUR Message-ID, Place-ID, ein
  `item`-Feld und vier Eckkoordinaten — das Spiel kennt selbst keine Namen für
  Untersuchungspunkte (Sehende sehen einfach die Grafik unter dem Cursor).
  **Kein Fix ohne Janas Entscheidung** — Lösungsvorschläge siehe Chat vom 19.07.2026
  (handgepflegte Namensdateien à la EvidenceDetails, item-Feld-Auflösung,
  Dialog-Caching nach Erstuntersuchung).
- [ ] **J5 Auswahldialog sagt mehr Optionen an, als wählbar sind** (19.07.2026).
  Symptom: „3 Optionen" angesagt, nur 2 mit Pfeiltasten erreichbar. Ursache: Die Mod
  zählte ihre per `setText` mitgeschnittenen Text-Slots — wenn das Spiel die Platte
  ohne `end()` wiederverwendet, überleben Slots eines früheren, größeren Dialogs.
  Fix: Zahl kommt jetzt aus dem spieleigenen Feld `selectPlateCtrl.cursor_num_`
  (per Reflection, mit Fallback auf die alte Zählung).
  **Fix gebaut — Janas Gegentest steht aus.**

- [ ] **Unsichere deutsche Begriffe im Spiel gegenprüfen** (aus der Übersetzung vom 18.07.2026,
  Jana: „sieht auf den ersten Blick gut aus", Detailprüfung läuft nebenbei weiter):
  „Psyche-Lock" (englisch gelassen), „Blue Badger" (englisch gelassen), Sprecher-IDs
  GS1-43 „Chefin" (vermutlich Lana), GS1-38 „Lehrerin", GS3-50 „Schwalbe" (mehrdeutig —
  pt-BR deutet es als Getränkeschluck), GS3-54 „Klingel", Ortsnamen englisch belassen
  (Heavenly Hall, Dusky Bridge, Eagle River). Unbekannte Sprecher-IDs loggt die Mod
  automatisch; Korrekturen sind per F5-Hot-Reload sofort testbar.
- [ ] **Weitere QWERTZ-Tastenfallen prüfen**: Die Musik-Player-Hilfe nennt Z und X
  (Album wechseln) — auf QWERTZ sind Z und Y vertauscht; klären, ob Unity hier die
  physische Taste oder das Zeichen meldet, und ggf. wie bei J2 Alternativtasten ergänzen.
  Kandidaten fürs Durchsehen: alle einstelligen Buchstaben-Hotkeys in `InputManager`
  und den Hilfetexten.
- [ ] **`menu.choice_intro` in die übrigen Sprachen übersetzen** (ko, pt-BR, zh-Hans;
  aktuell greift der englische Fallback). Am besten Upstream den Übersetzern überlassen —
  im PR erwähnen.
- [ ] **GamePath-Override dauerhaft lösen**: Das Spiel liegt auf `D:\SteamLibrary`, die
  csproj erwartet fest `C:\Program Files (x86)\Steam`. Bisher Build per
  `-p:GamePath="D:\SteamLibrary\steamapps\common\Phoenix Wright Ace Attorney Trilogy"`.
  Saubere Lösung: lokale, nicht committete Override-Datei (z. B. `Directory.Build.props`
  in `.gitignore`) — Jana fragen, ob gewünscht.
- [ ] **Ganz am Schluss: PR an `AccessMods/PhoenixWrightTrilogy`** — erst wenn Jana
  fertig getestet hat und den PR ausdrücklich freigibt (Regel vom 18.07.2026). Vorher
  entscheiden, ob `todos.md` aus dem PR-Branch herausgehalten wird (internes Protokoll).

## BLOCKIEREND: Hintergrundlaeufe koennen das Spielfenster nicht fokussieren

**Das ist die Ursache aller drei Fehlschlaege am Abend des 19.07.2026.**
Windows erlaubt `SetForegroundWindow` nur Prozessen mit Vordergrund-Recht. Ein
per `run_in_background` gestarteter Lauf hat das nicht: Die Tastendruecke gehen
ins Leere, das Spiel bleibt im Startbildschirm ("Druecke Enter") stehen, und
jede Menuepruefung meldet danach voellig zu Recht "nichts gefunden".

Zwei Reparaturversuche gingen daneben, weil sie Symptome behandelten
(Wartezeit verlaengert, Protokollfenster vergroessert). Erst ein **Screenshot**
zeigte den wahren Zustand — daher Janas neue Regel, jede Aktion per Screenshot
oder Sprachprotokoll zu bestaetigen.

Bereits umgesetzt: `gamekey.ps1` versucht den Fokus ueber `AttachThreadInput` zu
erzwingen und **prueft danach nach**; ohne Fokus wird mit klarer Meldung
abgebrochen statt still ins Leere getippt. Der Kniff allein reicht aber nicht.

**Fuer den naechsten Lauf, in dieser Reihenfolge probieren:**
1. **Laeufe im Vordergrund statt im Hintergrund**, in Haeppchen unter zehn
   Minuten (Zeitgrenze pro Werkzeugaufruf). Mit dem Schnelldurchlauf passt
   "Kapitel betreten + eine Szene auslesen" gut hinein. Das umgeht das Problem
   vollstaendig, statt es zu bekaempfen.
2. Falls Hintergrund noetig bleibt: vor dem Start `SystemParametersInfo` mit
   `SPI_SETFOREGROUNDLOCKTIMEOUT = 0` setzen, danach `SetForegroundWindow`.
3. Alternativ `SwitchToThisWindow(hWnd, true)` statt `SetForegroundWindow`.

## Lauf 4 (19.07.2026, ab 21:37) — Doppelarbeit behoben, Beinahe-Unfall

### Behoben: keine doppelten Dialoge mehr

`dump-hotspot-texts.ps1` liest jetzt die Namensdatei und ueberspringt Punkte,
die schon einen Namen haben. Im ersten Testlauf: "2 Punkte schon benannt,
uebersprungen" und "5 Punkte schon benannt, uebersprungen" — kein einziger
Dialog wurde erneut gelesen. Genau der Punkt, den Jana angemerkt hatte.

### Beinahe-Unfall: blindes Bestaetigen im Speichermenue

Derselbe Lauf verfehlte sein Ziel. Die Menuepruefung schlug fehl
("Menueeintrag 'Neues Spiel' nicht gefunden"), das Skript lief aber **trotzdem
weiter**, landete bei "Spiel Laden" und bestaetigte vier Mal
"Diese Speicherdaten laden?". Laden ist harmlos — bei "ueberschreiben?" waere
Janas Fortschritt weg gewesen.

**Ursache:** Nach dem Spielstart braucht der Titelbildschirm laenger als die
feste Wartezeit; die Tastendruecke gingen ins Leere, und `Select-MenuItem`
meldete zwar den Fehlschlag, wurde aber mit `[void](...)` verschluckt.

**Drei Gegenmassnahmen umgesetzt:**
1. Es wird gewartet, bis das Hauptmenue wirklich angesagt wurde, statt auf eine
   feste Zeit zu vertrauen.
2. Eine fehlgeschlagene Menuepruefung bricht ab (`throw`) statt weiterzulaufen.
3. Vor jedem blinden Tastenschwall wird geprueft, ob ein Speicher-Dialog offen
   ist ("Speicherdaten", "speichern", "ueberschreiben", "loeschen") — dann
   Abbruch statt Bestaetigen.

**Ausserdem:** Spielstaende werden jetzt vor jedem Lauf gesichert nach
`~/.claude/backups/pwaat-savedata/<zeitstempel>/`. Erste Sicherung liegt vor.

## WICHTIG fuer den naechsten Lauf: Doppelarbeit abstellen (Janas Einwand, 19.07.)

Jana hat zu Recht angemerkt, dass derzeit viele Dialoge mehrfach gelesen werden.
Das stimmt:

- Jeder Kapitellauf startet das Spiel neu und beginnt am **Kapitelanfang** —
  die Eroeffnungssequenz laeuft jedes Mal erneut durch (nur eben schnell).
- Beim Ortswechsel landet man immer wieder in schon erfassten Szenen
  ("bg=1 schon erfasst, weiter") — die Wege dorthin werden trotzdem gespielt.
- Dieselben Raeume wurden mehrfach erfasst: die Anwaltskanzlei viermal, die
  Strafanstalt dreimal.

**Zwei Auswege, in dieser Reihenfolge pruefen:**

1. **Spielstaende als Sprungmarken (einfach, sicher).** Einmal in einer
   Ermittlungsszene angekommen, dort speichern. Spaetere Laeufe laden den Stand
   direkt — kein Menue, keine Sequenz, kein doppelter Dialog. Das Spiel hat
   mehrere Speicherplaetze; ein Vorrat "ein Spielstand je Szene" waere fuer
   alle weiteren Laeufe wiederverwendbar. Die Bridge braucht dafuer je einen
   Befehl zum Speichern und Laden (SaveLoadUICtrl ist bereits gepatcht, die
   noetigen Einstiegspunkte sind also bekannt).

2. **Direktsprung ueber die spieleigene Kapitelmechanik (maechtiger).**
   Gefunden: `ChapterDataLoader.Load(String, ChapterData)`, dazu
   `LoadCoroutine`, `SceneLoad()`, `LetsGoLabelTop()`, `LetsGoSsCommand()`.
   `ChapterJumpCtrl.set_isEnable(bool)` schaltet die Kapitelauswahl frei.
   **Offen:** `ChapterData` ist ein VERSCHACHTELTER Typ — nicht ueber
   `GetType("ChapterData")` erreichbar, sondern ueber
   `GetType("ChapterDataLoader+ChapterData")` bzw. ueber `GetNestedTypes()`.
   Zuerst dessen Felder ansehen: Enthaelt er eine Nachrichten-/Label-Position,
   liesse sich direkt in die Ermittlung springen statt an den Kapitelanfang.
   `GlobalWork` haelt dazu `scenario`, `sce_flag`, `bk_start_mess`, `Bk_end_mess`.

Beides spart deutlich mehr Zeit als der Schnelldurchlauf, weil es die
Wiederholung ganz vermeidet statt sie nur zu beschleunigen.

## Lauf 3 — Abschluss: 81 Punkte in allen drei Spielen benannt

| Spiel | benannt | gesamt | Abdeckung |
|-------|---------|--------|-----------|
| GS1   | 47      | 757    | 6,2 %     |
| GS2   | 13      | 677    | 1,9 %     |
| GS3   | 21      | 505    | 4,2 %     |
| Summe | 81      | 1939   | 4,2 %     |

**Im Spiel gegengehoert** (GS1 und GS2): "Punkt 1: Grosser Felsen (oben zentral)",
"Punkt 3: Grosses Anwesen (Mitte rechts)". Zwei Punkte mit derselben Nachricht
teilen sich korrekt einen Namen (derselbe Felsen aus zwei Blickwinkeln).

**Zwei Fehler in Lauf 3 gefunden und behoben:**
1. Menuefuehrung zaehlte Tastendruecke blind — das Hauptmenue merkt sich aber die
   letzte Auswahl, wodurch der Lauf im Musikplayer landete. Jetzt liest sie die
   Ansagen der Mod und blaettert bis zum Ziel. Erst dadurch sind GS2 und GS3
   ueberhaupt erreichbar.
2. F5 lud zwar die Namensdateien neu, baute aber die gespeicherten
   Beschreibungen nicht neu auf — Namen aendern und F5 druecken zeigte weiter
   die alte Ansage. Behoben; damit ist der F5-Arbeitsablauf erst brauchbar.

## Lauf 3 (19.07.2026, ab 16:33) — GS3 begonnen

- **Menuefuehrung ist jetzt selbstpruefend.** Das Hauptmenue startet nicht immer
  auf demselben Eintrag (das Spiel merkt sich die letzte Auswahl); blindes
  Tastenzaehlen landete im Musikplayer. `enter-chapter.ps1` liest jetzt die
  Ansagen der Mod und blaettert, bis der gewuenschte Eintrag steht. Dadurch ist
  erstmals ein anderes Spiel als GS1 erreichbar (`-Game 1|2|3`).
- **GS3 angefangen**: 9 Punkte in 2 Szenen benannt (Schatzausstellung und
  Lagerbereich aus Episode 2), Datei `Data/de/GS3_Hotspots.json`. Alle Schluessel
  bereits im genauen Format `<szenario>/<hintergrund>/<nachricht>`.
- Damit ist die Pipeline in zwei verschiedenen Spielen erprobt.

## Offen: Werkzeug-Abhaengigkeit vor dem Pull Request aufloesen

Die Skripte im Repo (`cover-chapter.ps1`, `visit-location.ps1`, `list-chapters.ps1`)
rufen Helfer auf, die NICHT im Repo liegen, sondern unter
`~/.claude/scripts/` (`enter-chapter.ps1`, `gamekey.ps1`, `nav.ps1`, `say.ps1`).
Fuer Jana funktioniert das, fuer jeden anderen nicht.

Vor dem Pull Request entscheiden: entweder die Helfer nach
`AccessibilityMod/DevBridge/` mitnehmen (dann sind die harten Pfade darin zu
ersetzen), oder die Entwicklerskripte ganz aus dem PR heraushalten und nur die
Mod-Aenderungen einreichen. Zweiteres ist wahrscheinlich sauberer — die
DevBridge ist ein Arbeitswerkzeug, kein Teil der Mod fuer Spielerinnen.

## Hinweis fuer den Lauf um 16:33 Uhr: Weckruf um 21:37 pruefen

Es steht ein zusaetzlicher, selbst angelegter Weckruf fuer **21:37 Uhr**
(ID per `CronList` ermitteln). Er wurde angelegt, als noch unklar war, wieviel
der 16:33-Lauf schafft.

**Am Ende des 16:33-Laufs entscheiden:** Reicht das Erreichte bzw. wurde die
Arbeit abgeschlossen, den 21:37-Weckruf per `CronDelete` **loeschen**. Nur wenn
noch nennenswert Arbeit offen ist und das Kontingent nicht reicht, stehen lassen.

Grund (Jana, 19.07.2026): Weckrufe nur bei echtem Bedarf, nicht auf Vorrat —
sonst startet abends ein Lauf, der nichts mehr zu tun hat, und belegt womoeglich
ihren Rechner.

## Lauf 2 (19.07.2026, ab 11:33) — Durchbruch: Hotspots haben Namen

**Die Namensgebung funktioniert jetzt vollstaendig.** Ansage im Spiel z. B.:
"Punkt 1: Gemaelde (oben zentral)", "Punkt 6: Bett (unten rechts)".

### Wie die Namen entstehen (der tragfaehige Weg)

Nicht ueber die verschluesselten Dateien, sondern ueber das laufende Spiel:
1. `dump-hotspot-texts.ps1` faehrt per DevBridge jeden Punkt an, druckt Enter und
   liest mit, was die Mod ins Log schreibt — das ist der deutsche Untersuchungstext.
2. Aus dem Text wird ein Kurzname abgeleitet ("Ein einfaches Bett." -> "Bett").
   Die Namen bleiben damit spielbegriffstreu, nichts ist erfunden.
3. `HotspotNameService` liest sie aus `GS<n>_Hotspots.json`, F5 laedt neu.

### Werkzeuge (alle unter AccessibilityMod/DevBridge/)

- `dump-hotspot-texts.ps1` — alle Punkte der aktuellen Szene auslesen
- `visit-location.ps1` — ueber "Bewegen" zum naechsten Ort und dort auslesen
- `cover-chapter.ps1` — ganzes Kapitel abgrasen (Szene + alle Orte)
- `list-chapters.ps1` — Kapitelnamen einer Episode auflisten
- `~/.claude/scripts/enter-chapter.ps1` — vom Titelbildschirm ins Kapitel

### Wichtige Erkenntnisse

- **Schluessel muss Hintergrund UND Nachrichten-ID enthalten.** Derselbe Raum hat
  je Episode andere Nachrichten-IDs (Kanzlei: 156–160 in Episode 2, 130–134 in
  Episode 3), weil jede Episode eine eigene Szenariodatei hat.
- **Ortswechsel statt Kapitelneustart.** Das Detektivmenue fuehrt zu allen
  freigeschalteten Orten; ein Kapitelneustart kostet dagegen Minuten an
  Zwischensequenz.
- **Nur Ermittlungskapitel anfahren.** Prozesskapitel liefern keine Punkte. Erst
  `list-chapters.ps1`, dann gezielt waehlen.
- **Zeitgrenze pro Befehl sind 10 Minuten.** Laengere Durchlaeufe im Hintergrund
  starten, sonst bricht der Aufruf mitten in der Menuefuehrung ab.
- Zwei Punkte koennen dieselbe Nachricht teilen (z. B. der Studio-Van von zwei
  Seiten) — dann ist derselbe Name fuer beide richtig.

### Abschluss Lauf 2

**47 Untersuchungspunkte benannt** (4,6 % von GS1, 1,8 % vom Gesamtbestand).
Zwoelf davon bereits mit dem genauen Schluessel `<szenario>/<hintergrund>/<nachricht>`,
die uebrigen mit dem aelteren zweiteiligen Schluessel (funktioniert weiter).

Erfasste Szenen: Anwaltskanzlei Fey & Partner (Episoden 2, 3, 4), Strafanstalt
(Episoden 2, 3, 4), Hotelzimmer, Kanzlei Grossberg, Global Studios Eingang,
Gourd-See Parkeingang, Studio Eins (Filmset und Studioweg).

**Der Schnelldurchlauf hat sich bewaehrt**: Der letzte Kapitellauf erreichte die
Ermittlung in der ersten Runde statt nach 190 vergeblichen Tastendruecken.

### Stand der Abdeckung

Benannt: 30 Punkte in 6 Szenen — Anwaltskanzlei Fey & Partner (Episode 2 und 3),
Strafanstalt (Episode 2 und 3), Hotelzimmer, Kanzlei Grossberg, Global Studios
Eingang.

**Korrigierte Gesamtgroesse (Fund vom 19.07.2026 mittags):** Die Hotspot-Tabellen
liegen in DREI Klassen — `scenario` (GS1), `scenario_GS2`, `scenario_GS3`. Der
erste Abzug las nur die erste, deshalb stand vormittags faelschlich "757 Punkte
gesamt" im Protokoll. Tatsaechlich:

| Spiel | Szenen | Punkte |
|-------|--------|--------|
| GS1   | 109    | 757    |
| GS2   | 91     | 677    |
| GS3   | 71     | 505    |
| Summe | 271    | 1939   |

Damit sind die benannten 30 Punkte rund 1,5 % des Gesamtbestands. Die Pipeline
funktioniert, aber alles von Hand durchzuspielen skaliert nicht: pro Szene fallen
mehrere Minuten Zwischensequenz an.

### Empfohlener Ablauf fuer den naechsten Lauf

1. `list-chapters.ps1 -Episode <n>` — nur Kapitel mit "Ermittlung" oder
   "Untersuchung" im Namen taugen; Prozesskapitel ueberspringen.
2. `cover-chapter.ps1 -Episode <n> -Chapter <m>` **im Hintergrund starten**
   (`run_in_background`), weil ein Aufruf hoechstens 10 Minuten laufen darf und
   Kapitelanfaenge laenger dauern koennen.
3. Waehrend der Lauf arbeitet: NICHT parallel die Bridge abfragen. Zwei
   gleichzeitige Zugriffe blockieren sich gegenseitig (am 19.07. passiert,
   beide Laeufe mussten abgebrochen werden).
4. Aus den erzeugten JSON-Dateien die Kurznamen ableiten und in
   `Data/de/GS1_Hotspots.json` eintragen — der Schluessel steht als `key` schon
   fertig in der Datei.
5. Datei zusaetzlich nach `UserData/AccessibilityMod/de/` kopieren und F5
   druecken, dann mit der Punkt-Taste im Spiel gegenhoeren.

### Fehlalarm: "Spiel steht auf Japanisch" — es ist keiner

Beim Start meldet die Mod regelmaessig `Loaded 0 strings for ja` und
`LocalizationService initialized for language: JAPAN`. Das sieht aus, als waere
die Spielsprache verstellt, ist es aber nicht: Die Mod initialisiert sich schon
auf dem allerersten Ladebildschirm, bevor das Spiel seine Spracheinstellung
gesetzt hat, und faellt dabei auf den Vorgabewert JAPAN zurueck. Sobald der
Titelbildschirm steht, korrigiert sie sich selbst — im Log steht dann
`Language changed from JAPAN to GERMAN, reloading localization`.

**Lehre:** Vor dem "Reparieren" eines alarmierenden Signals erst pruefen, ob
ueberhaupt etwas kaputt ist. Ein Bildschirmfoto (`shot`) haette hier sofort
"Druecke Enter" auf Deutsch gezeigt. Die naheliegende "Reparatur" waere gewesen,
in der binaeren `systemdata` von Steam herumzuschreiben — das haette echten
Schaden anrichten koennen.

### Ideen zur Beschleunigung (fuer Lauf 3 zu pruefen)

1. **Zwischensequenzen ueberspringen.** Das Spiel hat eine Skip-Funktion
   (`optionSkip` in den Optionen, im Spiel vermutlich ueber eine Taste). Wenn
   die greift, verkuerzt sich der teuerste Teil jedes Kapiteldurchlaufs drastisch.
2. **Spielstaende als Sprungmarken.** Einmal in einer Ermittlungsszene speichern,
   danach direkt laden statt das Kapitel neu zu spielen. Die Bridge koennte das
   Speichern ausloesen; ein Vorrat an Spielstaenden waere fuer alle spaeteren
   Laeufe wiederverwendbar.
3. **Nur Szenen mit vielen Punkten zuerst.** `inspect-tables.json` sagt, welche
   Szene wieviele Punkte hat — grosse zuerst bringt am meisten Nutzen je Minute.

## Lauf 1 (19.07.2026, 06:33–10:30) — Ergebnisse und Sackgassen

### Erledigt

- [x] **Loesung 1 fertig**: Hotspots mit Beweisbezug sagen jetzt den offiziellen
  Spielnamen an ("Punkt 3: Diebeswerkzeug (oben links)"). Weg:
  `piceDataCtrl.instance.note_data` -> Eintrag mit `no == item` -> `.name`
  (das Spiel loest die Sprach-Text-ID selbst auf). **Wichtig**: Der Marker fuer
  "kein Beweisbezug" ist **255**, nicht 0.
- [x] **ALLE Hotspot-Daten ausgelesen — ohne das Spiel zu spielen.** Die Klasse
  `scenario` in `Assembly-CSharp.dll` enthaelt statische Tabellen
  `Sce<Episode>_<Teil>_room<Nr>_ck_mess_tbl : INSPECT_DATA[]`. Per Reflection
  auslesbar. Ergebnis: **757 Punkte in 109 Szenen**, gespeichert als
  `AccessibilityMod/DevBridge/inspect-tables.json`.
  Skript: `AccessibilityMod/DevBridge/dump-inspect-tables.ps1`.
  **Achtung Namensfalle**: `Sce1_`, `Sce2_`... ist die EPISODE, nicht das Spiel.
  Die Teilnummern 0/2/4 sind Ermittlungsabschnitte, ungerade sind Prozesse.
- [x] **DevBridge live erprobt**: `ping`, `state`, `hotspots`, `shot`, `say`,
  `loadbg`, `crop`, `scenarios`, `mdtpath`, `mes`, `mesraw`, `mesfile`.
- [x] **Menuefuehrung automatisiert**: `~/.claude/scripts/enter-chapter.ps1`
  faehrt vom Titelbildschirm bis in ein beliebiges Kapitel. Menuestruktur ist
  dort dokumentiert (Hauptmenue waagerecht, Spielauswahl senkrecht, zwei
  Rueckfragen mit Vorauswahl "Nein").

### Sackgassen (nicht noch einmal versuchen)

- **SendKeys taugt nicht fuer Unity.** Fenster-Nachrichten werden ignoriert;
  nur `keybd_event` wirkt. Pfeiltasten brauchen zwingend das
  Extended-Key-Kennzeichen. (`~/.claude/scripts/gamekey.ps1`)
- **Spiel-Assembly ausserhalb des Spiels aufrufen geht nicht** — Unity-Methoden
  scheitern mit "ECall-Methoden muessen in ein Systemmodul gepackt werden".
  Reine Datenfelder lesen (siehe scenario-Tabellen) funktioniert dagegen.
- **`.mdt`- und `.unity3d`-Dateien sind verschluesselt/komprimiert.**
  `new MdtData(bytes)` mit rohen Dateibytes wirft "Array index out of range".
  Offline-Extraktion von Texten und Hintergrundbildern ist damit versperrt.
- **`getPageMessage`/`getPageString` des Debug-Viewers scheitern** mit einer
  Nullreferenz, obwohl die Datei geladen ist (`mdt=ok`, `count=203`). Sie
  brauchen Datenstrukturen, die erst im echten Spielablauf gefuellt werden.
  Der Viewer laedt ausserdem nur die japanische Basisdatei; die Sprachfassungen
  liegen als eigene Dateien daneben (`_g` deutsch, `_u` englisch, ...).
- **`bgCtrl.sprite_data` ist bei per `loadbg` geladenen Hintergruenden leer**;
  ueber `sprite_renderer_` kommt nur eine 4x4-Platzhaltertextur. `loadbg` wirkt
  ausserdem nicht sichtbar, solange eine Szene laeuft — sie ueberschreibt das Bild.

### Naechster Schritt (empfohlen fuer Lauf 2)

Der tragfaehigste Weg ist **im Spiel ueber die Mod**, nicht offline:
Die Mod protokolliert bereits zuverlaessig den deutschen Text jeder Dialogzeile.
Ablauf pro Szene: Ermittlungsmodus erreichen -> `hotspots` liefert die Liste ->
je Punkt `hotspot <n>` + Enter -> die Untersuchungsbeschreibung landet im Log ->
daraus einen Kurznamen ableiten.

**Zur "0 Hotspots"-Beobachtung — KEIN Fehler in der Mod.** In der getesteten
Szene meldete die Mod dauerhaft "0 Hotspots", obwohl der Untersuchen-Modus aktiv
war (Lupe sichtbar). Geprueft: `GSStatic.inspect_data_` ist eine Eigenschaft, die
korrekt auf `GSStatic.inspect_work_.inspect_data_` weiterleitet — die Mod liest
also die richtige Quelle. Die Eroeffnung von Episode 2 hat schlicht noch keine
Untersuchungspunkte; 190 Tastendruecke reichten nicht bis zur eigentlichen
Ermittlung. **Konsequenz fuer Lauf 2**: ein Kapitel waehlen, das direkt mit der
Ermittlung beginnt (in Episode 2 z. B. "Untersuchung des Hotels", also Kapitel 2
statt 1), und die Punktzahl per `hotspots` pruefen, bevor Zeit ins Weiterklicken
geht.

## Nachtschicht 19.07.2026 — Stand

- [x] **DevBridge gebaut** (Commit siehe unten). Zeilenbasiertes TCP auf 127.0.0.1,
  Port in `UserData/AccessibilityMod/DevBridge/port.txt`, Antworten enden mit
  `<<END>>`, Ereignisse beginnen mit `! `. Netzwerk in Hintergrund-Threads,
  Ausfuehrung ausschliesslich im Unity-Hauptthread (aus `OnUpdate` abgepumpt) —
  Unity-Objekte ausserhalb des Hauptthreads anzufassen crasht das Spiel hart
  (Lehre aus Disco-A11y). Befehle: `ping`, `help`, `state`, `hotspots`,
  `hotspot <n>`, `dump [ordner]`, `shot [datei]`, `say <text>`.
  Client: `AccessibilityMod/DevBridge/bridge-client.ps1`.
  **Build gruen, im Spiel noch ungetestet.**
- [ ] **DevBridge live testen** — Spiel starten, `ping`/`state`/`hotspots`/`dump`
  gegen eine echte Untersuchungsszene laufen lassen. Erst danach ist die
  Automatisierung belastbar.
- [ ] **Wichtige Erkenntnis fuer `dump`**: Die Hotspot-Bilder werden NICHT per
  Bildschirmfoto geholt, sondern direkt aus der Hintergrundtextur geschnitten
  (`bgCtrl.instance.sprite_data.texture`, ueber RenderTexture lesbar gemacht,
  dann `EncodeToPNG`). Vorteil: unabhaengig von Fenstergroesse und Fokus, und
  enthaelt auch die gerade nicht sichtbaren Teile breiter Schwenk-Szenen.
  Das heisst auch: fuer die Bilderfassung muss Janas Rechner viel weniger lange
  blockiert werden als urspruenglich gedacht.
- [ ] **Loesung 1 (item-Feld zu Beweisstueck-Namen aufloesen) noch offen.**
  Vorarbeit ist erledigt, damit der naechste Lauf nicht neu forschen muss —
  alles per Reflection ueber `Assembly-CSharp.dll` ermittelt (es gibt in diesem
  Checkout KEINEN `Decompiled/`-Ordner, obwohl CLAUDE.md ihn erwaehnt):
  * Beweismittel sind vom Typ **`piceData`** (Tippfehler im Spielcode fuer
    "piece"). Zugriff auf den aktuellen Eintrag: `recordListCtrl.instance.current_pice_`.
  * Felder von `piceData`: `name_id_j_`, `name_id_u_`, `name_id_g_` (Namens-Text-IDs
    je Sprache), `comment_id_`, `detail_id`, `obj_id`, `no`, `type`, `path`,
    `file_id`. Der Name ist also eine **Text-ID**, kein String — deshalb erscheint
    er automatisch in der eingestellten Spielsprache. Die Mod nutzt an anderer
    Stelle bereits einen `.name`-Zugriff (siehe `CourtRecordPatches`), der die
    Aufloesung uebernimmt.
  * **Offener Schritt**: Die Zuordnung finden von `INSPECT_DATA.item` (uint) auf
    den passenden `piceData`-Eintrag — vermutlich ueber `piceData.no` oder
    `obj_id`. In `GSStatic` gibt es kein Feld mit "pice" oder "item" im Namen,
    die Tabelle liegt also woanders (Kandidaten: `recordListCtrl`, ein
    Ressourcen-Loader oder eine statische Tabelle in einer anderen Klasse).
  * `TextDataCtrl` bietet KEINE Item-Namen an (nur Common/Title/Option/Language/
    Save/Platform/System/Gallery) — dieser Weg ist eine Sackgasse.
- [ ] Crons fuer die drei Laeufe stehen: 6:33 (GS1), 11:33 (GS2), 16:33 (GS3).
  **Achtung: Crons leben nur in der laufenden Claude-Sitzung**, nicht auf der
  Platte — wird das Terminal geschlossen, sind sie weg.

## Erledigt

- [x] **Deutsche Lokalisierung komplett hinzugefügt** (18.07.2026, Commit `e33b4b4`):
  86 Dateien unter `AccessibilityMod/Data/de/` — strings.json (alle Schlüssel, Du-Form,
  Screenreader-Tastennamen), GS1–GS3-Namensdateien (Eigennamen englisch wie die offizielle
  deutsche Spielfassung, generische Bezeichnungen übersetzt), StaffRoll.txt, 80
  Beweismittel-Beschreibungen. `LocalizationValidator -- de --strict`: PASSED.
  **Von Jana getestet: sieht auf den ersten Blick gut aus.**
- [x] **J1 Auswahldialoge wurden nicht als solche angesagt** (18.07.2026, Jana:
  „Schön wäre eine Ansage wie ‚Drücke die Pfeiltasten zum Auswählen'"; Fix 19.07.2026,
  Commit `078dfc6`): Beim Öffnen eines Auswahldialogs (selectPlateCtrl) kam nur der Text
  der ersten Option. Es gibt keine einzelne „Dialog geöffnet"-Methode zum Patchen —
  stattdessen gilt der erste `playCursor` nach frisch gesetzten Optionstexten als
  Öffnung (Flag in `MenuPatches`, Reset in `setText`/`end`). Ansage jetzt z. B.:
  „Auswahl, 3 Optionen. Mit den Pfeiltasten wählen, Eingabe bestätigt. …" — als EINE
  Ansage, damit nichts unterbrochen wird. Muster aus Disco-A11y (`ResponseHowTo`).
  Neuer Plural-Schlüssel `menu.choice_intro` (en + de).
  **Von Jana bestätigt (19.07.2026).**
- [x] **J2 Navigation mit [ und ] auf QWERTZ unerreichbar** (19.07.2026, Jana: „Womit
  kann ich navigieren? Das hab ich noch nicht rausgefunden"; Fix Commit `7d51559`):
  Klammertasten existieren auf deutschem Layout nur als AltGr+8/9 — Unitys Legacy-Input
  meldet AltGr-Kombinationen nie als einzelnen Tastendruck, die Navigation war für
  QWERTZ-Nutzer in allen 7 Modi (Ermittlung, Zeigen, Luminol, Fingerabdruck, Videoband,
  3D-Beweis, Punkte-Rätsel) komplett tot. Fix: zentrale Helfer
  `NavigatePreviousPressed`/`NavigateNextPressed` akzeptieren zusätzlich **Komma/Punkt**
  (auf praktisch jedem Layout Direkttasten); deutsche Ansagen nennen jetzt Komma und
  Punkt statt der Klammern. **Von Jana bestätigt (19.07.2026).**

## Lehren

- **Tasten nie nach US-Layout ansagen oder binden**: gleiche Falle wie in Disco-A11y —
  Unity-`KeyCode`s für Satzzeichen sind auf Nicht-US-Layouts teils unerreichbar.
  Alternativtasten anbieten und die Ansagetexte pro Sprache an die realen Tasten anpassen.
- **PowerShell 5.1 + git commit -m mit Anführungszeichen** zerlegt die Nachricht in
  Pathspecs → Commit-Message in Datei schreiben und `git commit -F <datei>` nutzen.
- **`Edit` mit replace_all**: erst Helfer einfügen, dann global ersetzen, ersetzt auch
  die Helfer selbst (Endlos-Rekursion) — Reihenfolge umdrehen oder danach gegenlesen.
  Ebenso auf angrenzende Leerzeichen achten („Punktnavigieren").
- **Kein `Decompiled/`-Ordner im Repo vorhanden** (obwohl CLAUDE.md ihn erwähnt) —
  neue Patches möglichst über bereits verifizierte, existierende Hooks lösen; sonst
  müsste das Spiel erst selbst dekompiliert werden.

## Offline-Textauslese der Untersuchungstexte (24.07.2026)

Ziel: die restlichen ~1858 Untersuchungspunkte benennen, OHNE das Spiel fernzusteuern
(das scheiterte dreimal am Fensterfokus und war zu teuer). Idee: Untersuchungstexte
direkt aus den Nachrichtendateien entschlüsseln, das Spiel nur als passiver Decoder im
Titelbildschirm (Bridge über TCP, kein Fokus nötig).

- [x] **Phase 0 — Spiel als Decoder starten**: `PWAAT.exe` gestartet, Bridge auf Port
  48620 antwortet mit `pong`, keine Tasten geschickt. Läuft zuverlässig ohne
  Fokusproblem. **Erfolgreich.**
- [x] **Phase 1 — Decoder gegen bekannte Namen prüfen** (`scratchpad/verify-decode.ps1`,
  `scan-scenario.ps1`): **Decoder bestätigt**, aber **ID-Zuordnung offen**.
  - Die `-128`-Regel stimmt: Rohwerte 160–255 → ASCII (Wert−128), 12416 → Leerzeichen,
    172 → Komma, 174 → Punkt, 0/1/2/3 → Seiten-/Nachrichtenende, 512/kleine Werte →
    Steuercodes. Ergab überall sauberes Deutsch („7. September, 14:24 Bezirksgericht
    Angeklagter…", „Der Samurai-Speer! Der ist total cool…").
  - **ABER**: `(Szenario, Nachricht)` aus der Hotspot-Tabelle trifft NICHT den
    Untersuchungstext. GS2 `1/2/156` („Großer Felsen") → Gerichtstag-Kopf; GS3
    `2/7/132` („Bücherregal") → Steel-Samurai-Dialog. GS1-Untersuchungs-IDs (242–247)
    existieren in KEINEM der 36 Szenario-mdt (Array-out-of-range) — Szenarien haben nur
    ~200 Nachrichten, die Untersuchungs-IDs liegen darüber.
  - **Schlussfolgerung**: Die Untersuchungstexte liegen NICHT in den ADV-Dialog-mdt,
    die `LoadByTitle_Scenario` lädt, sondern in einer separaten Nachrichtenquelle
    (vermutlich pro Raum/Inspektion). Der frühere Treffer „Eine Vase" war Zufall.
  - **Nächster Schritt (offline, billig)**: herausfinden, aus welcher Datei/welchem
    mdt-Pfad `inspectCtrl` den Untersuchungstext zieht. `Decompiled/` fehlt im Repo →
    Spiel-Assembly per Reflection/ILSpy prüfen, welche mdt beim Untersuchen aktiv ist.
  - **Lehre**: Immer gegen BEKANNTE Namen verifizieren, bevor man einer Auslese
    vertraut. Ein einzelner plausibel aussehender Treffer („Eine Vase") ist kein Beweis
    für die richtige Zuordnung.
- [x] **Spiel dekompiliert** (24.07.2026, `ilspycmd 8.2.0.7535` via dotnet-Tool — net9
  scheiterte, net8-Pin nötig; choco hat nur GUI-ILSpy). PWAAT ist **Mono/net35**, also
  volle Methodenkörper. Ausgabe: 505 Klassen im Scratchpad (`pwaat-src/`, NICHT im Repo).
  **Ursache der falschen Zuordnung gefunden** in `MessageSystem.SetMessage2`:
  - Es sind ZWEI mdt gleichzeitig geladen: `GSStatic.mdt_datas_[0]` = Szenario-Datei
    (`LoadScenarioMdtFromStreamingAssets`), `[1]` = System-Datei
    (`LoadSystemMdtFromStreamingAssets`).
  - **ID ≥ 128 → Szenario-Datei, echter Index = ID − 128. ID < 128 → System-Datei.**
  - mdt-Dateien sind **verschlüsselt** (`decryptionCtrl.load`) → NICHT roh von Platte
    parsebar, das Spiel muss als Entschlüsseler laufen (Bridge). Aber nur Titelbildschirm.
- [x] **`−128`-Fix bewiesen für GS1** (`scratchpad/verify-decode.ps1`): Szenario 5 liefert
  exakt passende Texte (242→„…Kulisse für eine Bühne…", 244→„Umriss von Jack Hammers
  Leiche…", 247→„…Tritt-Leiter."). Decoder+Datei+Index stimmen.
- [ ] **OFFEN — Szenario-Index-Mapping für GS2/GS3**: `global_work_.scenario` (Quelle
  unserer Schlüssel) ≠ `LoadByTitle_Scenario`-Index bei GS2/GS3. GS2 Szen. 2 → Gerichts-
  dialog (falsche Datei); GS3 Szen. 7 → richtige Büro-Texte, aber gegen alte Handnotizen
  verschoben. **Nächster Schritt (offline, aus Decompilat)**: pro Inspect-Tabelle
  (`Sce<Ep>_<Part>_room<N>_ck_mess_tbl` in `scenario`/`_GS2`/`_GS3`) den korrekten
  Szenario-mdt-Index bestimmen, statt den fehleranfälligen Handnotizen zu trauen.
  **Erkenntnis**: Die Auslese ist vertrauenswürdiger als die alten in-game mitgeschriebenen
  Namen → am Ende komplett aus Decompilat + Auslese neu aufbauen.
- [x] **Schritt 1 — Szenario-Index-Mapping gelöst** (24.07.2026, `scratchpad/map-scenarios.ps1`
  → `scratchpad/scenario-map.json`). Grundlage: die drei Pfad-Tabellen aus
  `DebugMdtViewer.cs` (GS1 36, GS2 22, GS3 23 Einträge), die Szenario-Index → Datei
  `sc<Ep>_<Part>...mdt` abbilden. Inspect-Tabellen heißen `Sce<Ep>_<Part>_room<N>` →
  über (Episode, Teil) den Pfad-Index bestimmt. **Unabhängig von global_work_.scenario**
  (der war bei GS2/GS3 der falsche Index). Ergebnis von 271 Tabellen: **238 eindeutig,
  27 mehrdeutig** (v. a. GS1-Ep4-Splits sc4_Xa/sc4_Xb → 2 Kandidaten, per Auslese
  auflösbar), **6 ohne Kandidat** (Sprachvarianten `Sce4_0/4_2/4_4_room006_ger/usa` —
  Ep/Teil beim alten Dump nicht geparst; per Namen nachholbar).
  - **Sprachvarianten**: 3 Räume haben `_ger_ck_mess_tbl` (deutsch) neben `_usa`/Basis.
    Für Deutsch die `_ger_`-Tabelle nutzen (Quelle: `ChapterDataLoader.cs:439`).
- [ ] **Schritt 2 — Auslese aller ~1939 Punkte** (offen): Spiel als Entschlüsseler kurz
  starten, pro Punkt `mesraw title <kandidat> <msg-128>` (bei mehreren Kandidaten den
  nehmen, der lesbaren Text liefert), mit -128-Decoder entschlüsseln → `hotspot-texts.json`.
- [ ] **Schritt 3 — Namen ableiten** und `GS1/2/3_Hotspots.json` neu füllen.

### Nachtlauf 24.07.2026 (~04:35): Auslese begonnen, DREI Blocker gefunden

Spiel als Entschlüsseler gestartet (Jana idle ~2 h, sauber, danach wieder geschlossen).
Auslese per Hand über die Bridge (klammerfrei, weil der Shell-Wrapper `{}` im Inline-
Befehl mit `EPERM uv_spawn` blockiert — Funktionen/Schleifen inline gehen nicht; nur
sequenzielle mesraw-Aufrufe). Decoder erneut bestätigt: liefert überall sauberes Deutsch.
**Aber: mass-benennen ist so NICHT verlässlich möglich.** Gründe:

1. **`place`-Feld ist NICHT die bg-Nummer.** In inspect-tables.json ist `place` fast
   überall 0 (selten 22). Die alte Schlüssel-Mitte (z. B. „39" in „39/254", „26" in
   „5/26/242") war die **Laufzeit-`bgCtrl.bg_no`** — die steht OFFLINE nicht zur Verfügung.
   Der Mod schlägt Namen aber über (bg_no, message) nach → ohne bg kein gültiger Schlüssel.
2. **Laufzeit-`global_work_.scenario` ≠ mdt-Pfad-Index** bei GS2/GS3 (bei GS1 stimmten
   sie zufällig überein — deshalb lief GS1 im Test). D. h. selbst wenn man den Mod auf
   Schlüssel (scenario, message) umbaut, ist die offline abgeleitete Szenario-Nummer nicht
   die, die der Mod zur Laufzeit sieht. Drei verschiedene Nummerierungen (Tabellenname-
   Episode/Teil, mdt-Pfad-Index, Laufzeit-scenario) fluchten für GS2/GS3 nicht.
3. **Viele Tabellen sind Zwischensequenz-Text, keine Objektbeschreibung.** Z. B. GS2
   `Sce2_0_room000`: msg 130 → „Am Morgen arbeitete ich… an der Probe einer Actionszene…",
   132 → „Während alle anderen im Personalbereich zu Mittag aßen…". Aus solchen Monologen
   lässt sich kein Objektname ableiten. Welche Tabellen echte Untersuchungspunkte sind
   (die der Navigator nutzt) vs. Cutscene-Text, ist offline nicht sauber trennbar.

**Fazit:** Texte offline entschlüsseln = gelöst. Aber die **Schlüssel (Laufzeit-bg_no +
Laufzeit-scenario)** sind offline nicht ableitbar — sie existieren erst, wenn ein Raum
im Spiel tatsächlich geladen ist. KEINE Namen produziert (bewusst, um nichts Falsches
zu erzeugen). Spiel geschlossen. hotspot-texts.json wurde NICHT geschrieben.

**Empfehlung / offene Richtungsentscheidung für Jana:**
- Prüfen, ob der Bridge-Befehl `loadbg <n>` (lädt einen Hintergrund direkt) auch
  `bgCtrl.bg_no`, `global_work_.scenario` UND `GSStatic.inspect_data_` setzt. Wenn ja:
  ein **Szenen-Durchlauf per loadbg** (kein Durchspielen!) liefert je Szene die echten
  Laufzeit-Schlüssel; kombiniert mit der Offline-Textauslese = korrekte Namen, billig.
- Wenn `loadbg` das NICHT setzt: entweder eine kleine, saubere C#-Erweiterung im Mod, die
  beim normalen Szenenwechsel (bg_no/scenario/inspect_data_ vorhanden) je Hotspot
  bg_no+scenario+message+Text protokolliert; ODER Scope reduzieren.
- Der Decompile-Ordner (`scratchpad/pwaat-src`, 505 Klassen) und `scenario-map.json`
  bleiben nützliche Referenz. **Lehre:** Janas „wir wissen noch zu wenig" war korrekt —
  die Laufzeit-Schlüssel sind der fehlende Baustein.

#### NACHTRAG gleiche Nacht: Blocker #1 und #2 AUFGELÖST

- **`advCtrl.cs:197`**: `scenario_mdt = GSScenario.GetScenarioMdtPath(global_work_.scenario)`.
  Das Spiel nutzt `global_work_.scenario` DIREKT als Pfad-Index in dieselbe Tabelle wie
  DebugMdtViewer. Also: **Laufzeit-`scenario` == mdt-Pfad-Index == scenario-map-Wert.**
  Die alten GS2/GS3-Handnotizen hatten einfach falsche Szenario-Nummern.
- **scenario-map verifiziert**: „Kulisse" (msg 242) steht in Tabelle `Sce2_0_room006`
  (Episode 2/Teil 0) → scenario-map-Kandidat 5 (`sc2_0`) → im Test lieferte Szenario 5,
  Index 114 exakt „…Kulisse für eine Bühne…". Passt. Die alte „26" war die Laufzeit-bg
  von room006, kein Raum.
- **Blocker #1 (bg) entfällt**, wenn der Mod auf Schlüssel **`(scenario, message)`**
  umgestellt wird (statt `(bg, message)`): beide Werte sind zur Laufzeit vorhanden
  (`global_work_.scenario` + message), und innerhalb eines Szenario-mdt ist `message`
  eindeutig. `HotspotNameService` bekommt zusätzlich das Schlüsselformat
  `<scenario>/<message>`.
- **Blocker #3 (Cutscene-Tabellen)** bleibt reine Qualitätsfrage: aus Erzähltext keinen
  Objektnamen erfinden → markieren/überspringen. Keine echte Sperre.

**Tragfähiger Plan (validiert):**
1. Mod: `HotspotNameService` um Schlüssel `<scenario>/<message>` erweitern (Fallback vor
   `<scenario>/<bg>/<message>`).
2. Texte offline auslesen (Szenario = scenario-map-Index, Index = message-128, Decoder).
3. Namen VON HAND aus dem Spielwortlaut ableiten; Cutscene/kein-Objekt markieren.
4. Neue Namensdateien `GS1/2/3_Hotspots.json` im Format `<scenario>/<message>` füllen.
5. Bauen, committen, pushen (kein PR).

#### Ergebnis Nachtlauf (committet `5bc1819`, gepusht)

- **Mod-Änderung live**: `HotspotNameService` versteht jetzt `s<scenario>/<message>`
  (bg-frei, rückwärtskompatibel). Build ok (0 Fehler) — WICHTIG: mit
  `-p:GamePath="D:\SteamLibrary\...\Phoenix Wright Ace Attorney Trilogy"` bauen, weil
  der csproj-Default auf `C:\Program Files (x86)\Steam\...` zeigt (dort liegt das Spiel
  NICHT → sonst 815 Referenzfehler).
- **Pipeline end-to-end bewiesen**: GS1 `Sce2_0_room002` (Szenario 5) dekodiert exakt zu
  den bekannten Global-Studios-Objekten (Haupttor, Studio-Van, Wachstation, …), deckt
  sich mit den alten bg-25-Namen. `room003` (Studio-Lager) neu benannt: 9 Punkte
  (`s5/194`–`s5/202`), im JSON. Umlaut-Codes bestätigt: ü=380, ä=356, ö=374, ß=357,
  Ü=348, Bindestrich=8341.
- **Wichtige Erkenntnis zur SKALIERUNG**: Das reine Von-Hand-Dekodieren (Rohwerte lesen,
  im Kopf zu Text) ist pro Punkt teuer — 1939 Punkte so zu benennen ist über viele
  Sessions kontingent-prohibitiv. Die mechanische Auslese ist inzwischen VALIDIERT und
  risikoarm; nur die BENENNUNG braucht Urteil. Der Shell-Wrapper blockiert `{}` inline
  (`EPERM uv_spawn`), also sind Inline-Schleifen nicht möglich.
  **Empfehlung/Direktions-Frage an Jana**: einen reinen DECODE-Helfer zulassen (nur
  Texte auslesen → lesbare Datei `table/scenario/message → deutscher Text`), aus der ich
  die Namen dann VON HAND ableite. Das trennt „mechanisch entschlüsseln" (validiert,
  unkritisch) von „benennen" (Urteil, bleibt bei mir) und macht die Menge machbar, ohne
  falsche Annahmen zu backen. Reines Von-Hand über die Bridge bleibt möglich, ist aber
  langsam/teuer.
- Kein weiterer Cron angelegt: Der Skalierungs-Weg ist eine Richtungsentscheidung für
  Jana (Decode-Helfer ja/nein), kein blindes Weitergrinden.

#### 24.07.2026 (Abend): Decode-Helfer freigegeben, GS1 KOMPLETT benannt

Jana hat den Decode-Helfer freigegeben (erst testen + von Hand gegenprüfen, dann auf
alle anwenden; Namen VERBATIM aus dem Spiel, nicht kürzen/umformulieren). Neue Regeln:
Todo-Listen automatisch nutzen; fertige Werkzeuge ins Repo (nicht scratchpad).

- **Helfer gebaut + validiert** (`AccessibilityMod/DevBridge/`): `decode-hotspot-texts.ps1`
  (Auslese über Bridge, argument-korrekter Decoder), `scan-one.ps1` (Szenario-Diagnose),
  `gen-json.ps1` + `assemble-gs.ps1` (Namensdatei bauen), `scenario-map.json` (Referenz).
  Decoder-Details: Wert<128 = Steuercode (samt Argumenten via code_proc_arg_count_table
  überspringen!), Wert>=128 = Zeichen (Unicode = Wert-128); 12416=Leerz., 8341=Bindestr.;
  erste Anzeigeseite endet bei Code 0/2/3/45. Umlaute/Anführungszeichen korrekt.
- **Handprobe bestanden**: GS1 `Sce2_0_room002` deckt sich exakt mit alten bestätigten
  Namen (Haupttor, Wachstation, Übersichtsplan der Studios …).
- **GS1 fertig**: 636 Punkte, **511 verbatim benannt** (`s<scenario>/<message>`) in
  `GS1_Hotspots.json` (+ 47 alte bg-Einträge als Rückfall). Alle Szenarien kohärent,
  gegen Ground-Truth geprüft (Gemälde, Kliententisch, Plastikblumen, Bett, Gourdsee,
  Polizeirevier …). Build ok. **Verbleibend ~82 ohne Text** (SYSTEM-mdt <128 oder
  Kandidaten-Fehltreffer) — später.
- **GS2/GS3 NOCH NICHT** (Task #16): Tabelle→Szenario-Zuordnung offline unzuverlässig
  (msg-IDs wiederholen sich über Episoden; Tabellenname-Präfix != Laufzeit-Szenario;
  mehrere Szenarien dekodieren kohärent → nicht disambiguierbar allein per Kohärenz).
  ChapterDataLoader referenziert ck_mess_tbl nur im Sonderfall; Zuordnung ist vermutlich
  datengetrieben. Braucht andere Lösung (authoritatives room->scenario ODER kurzer
  In-Game-Durchlauf, der pro Raum global_work_.scenario + inspect_data_ erfasst).
