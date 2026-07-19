# visit-location.ps1 — Zu einem anderen Schauplatz wechseln und dort alle
# Untersuchungspunkte auslesen.
#
# Warum ueber "Bewegen" statt ueber die Kapitelauswahl?
# Ein Kapitel neu zu starten kostet jedesmal mehrere Minuten Zwischensequenz.
# Innerhalb eines Kapitels fuehrt das Detektivmenue dagegen direkt zu allen
# freigeschalteten Orten — jeder Ort ist ein eigener Hintergrund mit eigenen
# Punkten. Das ist der schnellste Weg, viele Szenen abzudecken.
#
# Erwarteter Ausgangszustand: irgendwo im Ermittlungsteil eines Kapitels
# (Untersuchen-Modus oder Detektivmenue).
#
# Aufruf:
#     powershell -File visit-location.ps1 -Steps 1 -OutFile ziel.json
#   -Steps = wie oft im Ziel-Menue nach unten, bevor bestaetigt wird.

param(
    [int]$Steps = 0,
    [string]$OutFile = "",
    [int]$Delay = 450
)

$client = "C:\Users\Jana Schmidt\games\SRC\PhoenixWrightTrilogy\AccessibilityMod\DevBridge\bridge-client.ps1"
$key    = "$env:USERPROFILE\.claude\scripts\gamekey.ps1"
$dump   = "C:\Users\Jana Schmidt\games\SRC\PhoenixWrightTrilogy\AccessibilityMod\DevBridge\dump-hotspot-texts.ps1"

function State { (& $client state) -join " " }

# --- Zurueck ins Detektivmenue ---------------------------------------------
# Aus dem Untersuchen-Modus fuehrt die Ruecktaste heraus. Steht man schon im
# Menue, schadet der Tastendruck nicht.
& $key -Delay $Delay BACKSPACE | Out-Null
Start-Sleep -Seconds 2

# --- "Bewegen" waehlen ------------------------------------------------------
# Das Menue ist waagerecht; "Bewegen" liegt rechts von "Untersuchen".
& $key -Delay $Delay RIGHT | Out-Null
Start-Sleep -Milliseconds 800
& $key -Delay $Delay ENTER | Out-Null
Start-Sleep -Seconds 2

# --- Ziel auswaehlen --------------------------------------------------------
for ($i = 0; $i -lt $Steps; $i++) {
    & $key -Delay $Delay DOWN | Out-Null
    Start-Sleep -Milliseconds 600
}
& $key -Delay $Delay ENTER | Out-Null
Start-Sleep -Seconds 6

# --- Bis zum Ermittlungsmodus vorspulen ------------------------------------
# Nach dem Ortswechsel laeuft meist etwas Dialog. Es wird abwechselnd
# weitergeklickt und geprueft, ob schon Punkte da sind.
for ($r = 1; $r -le 12; $r++) {

    if ((State) -match 'investigation=yes') { break }

    $h = & $client hotspots
    if (($h -join " ") -notmatch 'keine Hotspots') {
        # Punkte sind geladen, aber wir sind noch im Menue -> "Untersuchen"
        & $key -Delay $Delay ENTER | Out-Null
        Start-Sleep -Seconds 3
        if ((State) -match 'investigation=yes') { break }
    }

    & $key -Delay 350 ENTER ENTER ENTER ENTER ENTER ENTER | Out-Null
    Start-Sleep -Seconds 2
}

$s = State
if ($s -notmatch 'investigation=yes') {
    Write-Warning "Ermittlungsmodus nicht erreicht. Zustand: $s"
    exit 1
}

$bg = if ($s -match 'bg_no=(-?\d+)') { $Matches[1] } else { "unbekannt" }
"Am Ziel angekommen, Hintergrund $bg"

if ([string]::IsNullOrEmpty($OutFile)) {
    $OutFile = Join-Path $env:TEMP "hotspot-texts-bg$bg.json"
}

& $dump -OutFile $OutFile
