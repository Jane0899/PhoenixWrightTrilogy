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

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

function State { (& $client state) -join " " }
function BgNo {
    $s = State
    if ($s -match 'bg_no=(-?\d+)') { return $Matches[1] }
    return "?"
}

# --- Ins Kapitel ------------------------------------------------------------
if (-not $SkipEnter) {
    "== Kapitel betreten: Episode $Episode, Kapitel $Chapter =="
    & "$env:USERPROFILE\.claude\scripts\enter-chapter.ps1" -Episode $Episode -Chapter $Chapter | Out-Null
}

# --- Bis zum Ermittlungsmodus vorspulen ------------------------------------
"== Warte auf Ermittlungsmodus =="
$reached = $false
for ($r = 1; $r -le 25; $r++) {
    if ((State) -match 'investigation=yes') { $reached = $true; break }

    $h = & $client hotspots
    if (($h -join " ") -notmatch 'keine Hotspots') {
        # Punkte geladen -> im Detektivmenue "Untersuchen" bestaetigen
        & $key -Delay 500 ENTER | Out-Null
        Start-Sleep -Seconds 3
        if ((State) -match 'investigation=yes') { $reached = $true; break }
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
