# PhoenixWrightTrilogy Accessibility Mod — TODOs

Arbeitsprotokoll nach dem Muster von `Disco-A11y/todos.md`. Offene Punkte aus Janas
Test-Sessions als J-Nummern; Erledigtes bleibt abgehakt als Verlauf stehen.

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
