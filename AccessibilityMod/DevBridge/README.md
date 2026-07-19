# DevBridge — Entwicklerwerkzeuge der Accessibility-Mod

Die DevBridge ist eine Fernsteuerung für das laufende Spiel. Sie wurde gebaut, um
die Untersuchungspunkte (Hotspots) aller drei Spiele zu erfassen und zu benennen —
die Spieldaten selbst kennen dafür keine Namen (siehe `todos.md`, Punkt J4).

Sie ist ein reines Werkzeug für die Entwicklung. Für Spielerinnen und Spieler
ändert sie nichts: Der Server lauscht nur lokal und tut ohne Verbindung nichts.

## Verbindung

Zeilenbasiertes TCP auf `127.0.0.1`. Den Port schreibt die Mod beim Start nach
`UserData/AccessibilityMod/DevBridge/port.txt`. Jede Antwort endet mit einer Zeile
`<<END>>`; unaufgeforderte Ereignisse beginnen mit `! `.

```powershell
.\bridge-client.ps1 ping
.\bridge-client.ps1 state
.\bridge-client.ps1 hotspots
```

## Befehle

| Befehl | Wirkung |
|--------|---------|
| `ping` | Lebenszeichen |
| `state` | Spiel, Szenario, Hintergrund, Ermittlungsmodus, Punktzahl |
| `hotspots` | Untersuchungspunkte der aktuellen Szene mit Koordinaten |
| `hotspot <n>` | Cursor auf Punkt n setzen |
| `dump [ordner]` | Bildausschnitt je Hotspot als PNG |
| `shot [datei]` | Bildschirmfoto |
| `crop <x> <y> <r> <datei>` | Ausschnitt des Hintergrunds speichern |
| `loadbg <nr>` | Hintergrund direkt laden |
| `say <text>` | Text über den Screenreader ausgeben |
| `fast on\|off` | **Schnelldurchlauf** für Zwischensequenzen |
| `scenarios <spiel>` | Nachrichtendateien eines Spiels auflisten |
| `mes` / `mesraw` / `mesfile` | Nachrichtentexte (eingeschränkt, siehe unten) |

### `fast` ist der wichtigste Befehl

Zwischensequenzen sind der mit Abstand größte Zeitfresser. `fast on` setzt zwei
Entwicklerschalter des Spiels (`debug_skip_`, `debug_no_key_wait_`) und verkürzt
den Weg bis zur Ermittlung von über 190 erfolglosen Tastendrücken auf **wenige
Sekunden**. Vor dem Zurückgeben des Spiels wieder ausschalten.

## Skripte

| Skript | Zweck |
|--------|-------|
| `dump-inspect-tables.ps1` | Liest **alle** Hotspot-Daten aller drei Spiele direkt aus der Spiel-Assembly — ohne laufendes Spiel. Ergebnis: `inspect-tables.json` (1939 Punkte, 271 Szenen). |
| `dump-hotspot-texts.ps1` | Untersucht jeden Punkt der aktuellen Szene und schneidet mit, was das Spiel dazu sagt. Grundlage für die Namen. |
| `visit-location.ps1` | Wechselt über das Detektivmenü zum nächsten Ort und liest dort aus. |
| `cover-chapter.ps1` | Grast ein ganzes Kapitel ab (Szene + alle erreichbaren Orte). |
| `list-chapters.ps1` | Listet die Kapitelnamen einer Episode. |
| `scene-priority.md` | Szenen nach Punktzahl sortiert — die ergiebigsten zuerst. |

## Woher die Namen kommen

Nicht erfunden, sondern aus dem Spiel selbst: Beim Untersuchen sagt Phoenix, was
er sieht („Ein einfaches Bett."). Daraus wird der Kurzname („Bett"). Eingetragen
werden sie in `Data/<sprache>/GS<n>_Hotspots.json` unter dem Schlüssel
`<szenario>/<hintergrund>/<nachricht>`; F5 im Spiel lädt sie neu.

Die Szenario-Nummer gehört zwingend dazu: Dieselbe Nachrichten-Nummer bedeutet in
verschiedenen Episoden Verschiedenes — in der Kanzlei ist 134 in Episode 3 ein
altes Filmplakat, in Episode 4 ein Steel-Samurai-Poster.

## Zwei Fallen

1. **Nie die Bridge abfragen, während ein Skript sie bedient.** Beides blockiert
   sich gegenseitig, und das Spiel muss anschließend neu gestartet werden.
2. **Ein Werkzeugaufruf darf höchstens zehn Minuten laufen.** Kapiteldurchläufe
   gehören deshalb in den Hintergrund.

## Was nicht funktioniert

- Die Spieldateien (`.mdt`, `.unity3d`) sind verschlüsselt; Texte und
  Hintergrundbilder lassen sich nicht offline auslesen.
- Die Spiel-Assembly außerhalb des Spiels aufzurufen scheitert an Unity
  („ECall-Methoden müssen in ein Systemmodul gepackt werden"). Reine Datenfelder
  lesen geht dagegen — genau das nutzt `dump-inspect-tables.ps1`.
