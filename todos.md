# PhoenixWrightTrilogy Accessibility Mod — TODOs

Arbeitsprotokoll nach dem Muster von `Disco-A11y/todos.md`. Offene Punkte aus Janas
Test-Sessions als J-Nummern; Erledigtes bleibt abgehakt als Verlauf stehen.

## Offene Punkte

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
