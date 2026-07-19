# cover-chapter.ps1 — Ein Ermittlungskapitel vollstaendig abgrasen:
# hineingehen, alle erreichbaren Schauplaetze besuchen und ueberall die
# Untersuchungstexte auslesen.
#
# Das ist die Skalierungsstufe ueber visit-location.ps1: Ein Kapitel enthaelt
# mehrere Orte, und jeder Ort ist ein eigener Hintergrund mit eigenen Punkten.
# Bereits gesehene Hintergruende werden uebersprungen, damit dieselbe Szene
# nicht mehrfach ausgelesen wird (die Ortsliste wiederholt sich beim
# Durchblaettern).
#
# Aufruf:
#     powershell -File cover-chapter.ps1 -Episode 3 -Chapter 1 -OutDir C:\...\texte

param(
    # 1 = GS1, 2 = GS2, 3 = GS3. Wird an enter-chapter.ps1 durchgereicht.
    [int]$Game = 1,
    [int]$Episode = 2,
    [int]$Chapter = 2,
    [string]$OutDir = "C:\Users\JANASC~1\AppData\Local\Temp\claude\C--Users-Jana-Schmidt-games-SRC-PhoenixWrightTrilogy\78bf6283-4dd5-40bf-8325-1d585344bdf4\scratchpad\texts",

    # Wieviele verschiedene Ziele im Bewegen-Menue hoechstens probiert werden.
    [int]$MaxLocations = 6,

    [switch]$SkipEnter
)

$client = "C:\Users\Jana Schmidt\games\SRC\PhoenixWrightTrilogy\AccessibilityMod\DevBridge\bridge-client.ps1"
$key    = "$env:USERPROFILE\.claude\scripts\gamekey.ps1"
$dump   = "C:\Users\Jana Schmidt\games\SRC\PhoenixWrightTrilogy\AccessibilityMod\DevBridge\dump-hotspot-texts.ps1"
$visit  = "C:\Users\Jana Schmidt\games\SRC\PhoenixWrightTrilogy\AccessibilityMod\DevBridge\visit-location.ps1"
$log    = "D:\SteamLibrary\steamapps\common\Phoenix Wright Ace Attorney Trilogy\MelonLoader\Latest.log"

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

function State { (& $client state) -join " " }
function BgNo {
    $s = State
    if ($s -match 'bg_no=(-?\d+)') { return $Matches[1] }
    return "?"
}

# --- Ins Kapitel ------------------------------------------------------------
if (-not $SkipEnter) {
    "== Kapitel betreten: GS$Game, Episode $Episode, Kapitel $Chapter =="
    & "$env:USERPROFILE\.claude\scripts\enter-chapter.ps1" -Game $Game -Episode $Episode -Chapter $Chapter | Out-Null
}

# --- Schnelldurchlauf einschalten ------------------------------------------
# Ohne diesen Schalter ist die Zwischensequenz vor der Ermittlung der mit
# Abstand teuerste Teil (Episode 2 Kapitel 1: ueber 190 Tastendruecke ohne
# Erfolg). Mit ihm dauert derselbe Weg wenige Sekunden.
$fast = & $client "fast on"
"== Schnelldurchlauf: $($fast -join ' ') =="

# --- Bis zum Ermittlungsmodus vorspulen ------------------------------------
# Manche Kapitel beginnen mit sehr langen Zwischensequenzen (Episode 2 brauchte
# ueber 190 Tastendruecke). Deshalb wird der Fortschritt laufend gemeldet statt
# stumm zu warten — sonst ist von aussen nicht unterscheidbar, ob das Skript
# arbeitet oder haengt.
"== Warte auf Ermittlungsmodus =="
$reached = $false
for ($r = 1; $r -le 25; $r++) {

    if ((State) -match 'investigation=yes') { $reached = $true; break }

    $h = & $client hotspots
    if (($h -join " ") -notmatch 'keine Hotspots') {
        # Punkte geladen -> im Detektivmenue "Untersuchen" bestaetigen
        "   Runde ${r}: Punkte geladen, waehle Untersuchen"
        & $key -Delay 500 ENTER | Out-Null
        Start-Sleep -Seconds 3
        if ((State) -match 'investigation=yes') { $reached = $true; break }
    } else {
        # Letzte gesprochene Zeile mitloggen, damit man sieht, wo die Sequenz steht
        $last = Get-Content $log -Tail 25 |
            Where-Object { $_ -match '\[(Dialogue|Menu)\]' } |
            Select-Object -Last 1
        $short = ($last -replace '^.*\] \[(Dialogue|Menu)\] ', '')
        if ($short.Length -gt 70) { $short = $short.Substring(0, 70) + "..." }
        "   Runde ${r}: $short"
    }

    & $key -Delay 350 ENTER ENTER ENTER ENTER ENTER ENTER ENTER ENTER | Out-Null
    Start-Sleep -Seconds 2
}

if (-not $reached) {
    Write-Warning "Ermittlungsmodus in Episode $Episode / Kapitel $Chapter nicht erreicht."
    exit 1
}

# --- Erste Szene ------------------------------------------------------------
$seen = @{}
$bg = BgNo
"== Szene bg=$bg =="
& $dump -OutFile (Join-Path $OutDir "bg$bg.json")
$seen[$bg] = $true

# --- Weitere Orte -----------------------------------------------------------
# Es wird der Reihe nach jeder Eintrag im Bewegen-Menue angesteuert. Landet man
# auf einem schon bekannten Hintergrund, wird nur weitergeschaltet.
for ($step = 0; $step -lt $MaxLocations; $step++) {
    "== Ortswechsel (Schritt $step) =="

    & $visit -Steps $step -OutFile (Join-Path $OutDir "tmp.json") 2>$null | Out-Null

    $bg = BgNo
    if ($seen.ContainsKey($bg)) {
        "   bg=$bg schon erfasst, weiter"
        continue
    }

    "== Neue Szene bg=$bg =="
    & $dump -OutFile (Join-Path $OutDir "bg$bg.json")
    $seen[$bg] = $true
}

"Fertig. Erfasste Hintergruende: $(($seen.Keys | Sort-Object) -join ', ')"
