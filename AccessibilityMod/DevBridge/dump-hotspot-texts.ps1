# dump-hotspot-texts.ps1 — Untersucht der Reihe nach jeden Hotspot der aktuellen
# Szene und schneidet mit, was das Spiel dazu sagt.
#
# Hintergrund (Fund vom 19.07.2026): Die Untersuchungstexte liegen verschluesselt
# in den mdt-Dateien und sind von aussen nicht lesbar. Das laufende Spiel
# entschluesselt sie aber ohnehin, und die Accessibility-Mod protokolliert jede
# Dialogzeile mit. Also: Punkt anfahren, untersuchen, Log mitlesen. Damit
# entstehen spielbegriffstreue Beschreibungen in der eingestellten Spielsprache,
# ohne irgendetwas zu erfinden.
#
# Voraussetzung: Das Spiel laeuft, die Szene ist im Ermittlungsmodus
# (Detektivmenue -> "Untersuchen" bereits bestaetigt).
#
# Aufruf:
#     powershell -File dump-hotspot-texts.ps1 -OutFile texte.json

param(
    [string]$OutFile = "C:\Users\Jana Schmidt\games\SRC\PhoenixWrightTrilogy\AccessibilityMod\DevBridge\hotspot-texts.json",

    # Wieviele Enter-Druecke maximal, um nach dem Untersuchen zum
    # Ermittlungsmodus zurueckzukehren. Manche Punkte loesen mehrseitige
    # Dialoge aus.
    [int]$MaxAdvance = 25,

    [int]$Delay = 350
)

$client = "C:\Users\Jana Schmidt\games\SRC\PhoenixWrightTrilogy\AccessibilityMod\DevBridge\bridge-client.ps1"
$key    = "$env:USERPROFILE\.claude\scripts\gamekey.ps1"
$log    = "D:\SteamLibrary\steamapps\common\Phoenix Wright Ace Attorney Trilogy\MelonLoader\Latest.log"

function Get-LogCount { if (Test-Path $log) { (Get-Content $log).Count } else { 0 } }

function Get-NewDialogue([int]$since) {
    $all = Get-Content $log
    if ($all.Count -le $since) { return @() }
    $all[$since..($all.Count - 1)] |
        Where-Object { $_ -match '\[(Dialogue|Narrator|Evidence)\]' } |
        ForEach-Object {
            # Zeitstempel und Modulnamen abschneiden, Typmarke behalten waere Ballast
            ($_ -replace '^\[\d{2}:\d{2}:\d{2}\.\d{3}\] \[Phoenix_Wright_Accessibility\] \[[A-Za-z]+\] ', '')
        }
}

function Test-InInvestigation {
    try { return ((& $client state) -join " ") -match 'investigation=yes' } catch { return $false }
}

# --- Hotspot-Liste holen ----------------------------------------------------
$raw = & $client hotspots
if (($raw -join " ") -match 'keine Hotspots') {
    Write-Error "Keine Hotspots in dieser Szene - ist der Ermittlungsmodus aktiv?"
    exit 1
}

$header = $raw | Select-Object -First 1
$bgNo = if ($header -match 'bg_no=(-?\d+)') { $Matches[1] } else { "?" }
$game = if ($header -match 'game=(\w+)') { $Matches[1] } else { "?" }
# Die Szenario-Nummer gehoert in den Schluessel: Nachrichten-IDs wiederholen
# sich zwischen Episoden mit anderem Inhalt (Kanzlei, Nachricht 134: in
# Episode 3 ein altes Filmplakat, in Episode 4 ein Steel-Samurai-Poster).
$scenario = if ($header -match 'scenario=(-?\d+)') { $Matches[1] } else { "?" }

$points = @()
foreach ($line in ($raw | Select-Object -Skip 1)) {
    if ($line -match '^(\d+)\s+msg=(\d+)\s+idx=(\d+)\s+x=(-?\d+)\s+y=(-?\d+)\s+examined=(\d)\s+item=(\d+)') {
        $points += [pscustomobject]@{
            index   = [int]$Matches[1]
            message = [int]$Matches[2]
            x       = [int]$Matches[4]
            y       = [int]$Matches[5]
            item    = [int]$Matches[7]
            text    = $null
        }
    }
}

"Szene $game szenario=$scenario bg=$bgNo mit $($points.Count) Punkten"

# --- Jeden Punkt untersuchen ------------------------------------------------
foreach ($p in $points) {

    # Sicherstellen, dass wir ueberhaupt im Ermittlungsmodus sind. Nach einem
    # Punkt landet man manchmal im Detektivmenue statt direkt zurueck.
    if (-not (Test-InInvestigation)) {
        & $key -Delay $Delay ENTER | Out-Null
        Start-Sleep -Milliseconds 800
    }

    $nav = & $client "hotspot $($p.index)"
    if (($nav -join " ") -notmatch '^ok') {
        "  Punkt $($p.index): nicht anfahrbar ($nav)"
        continue
    }
    Start-Sleep -Milliseconds 600

    $before = Get-LogCount

    # Untersuchen ausloesen
    & $key -Delay $Delay ENTER | Out-Null
    Start-Sleep -Seconds 2

    # Dialog abarbeiten, bis wir wieder im Ermittlungsmodus sind
    $collected = @()
    for ($i = 0; $i -lt $MaxAdvance; $i++) {
        $collected += Get-NewDialogue $before
        if ((Test-InInvestigation) -and ($i -gt 0)) { break }
        & $key -Delay $Delay ENTER | Out-Null
        Start-Sleep -Milliseconds 900
    }
    $collected += Get-NewDialogue $before

    # Doppelte Zeilen entfernen (das Log wiederholt beim Blaettern), Reihenfolge halten
    $seen = @{}
    $unique = @()
    foreach ($l in $collected) {
        $t = $l.Trim()
        if ($t.Length -gt 0 -and -not $seen.ContainsKey($t)) { $seen[$t] = $true; $unique += $t }
    }

    $p.text = ($unique -join " ")
    $short = if ($p.text.Length -gt 90) { $p.text.Substring(0, 90) + "..." } else { $p.text }
    "  Punkt $($p.index) (msg=$($p.message)): $short"
}

[pscustomobject]@{
    game     = $game
    scenario = $scenario
    bgNo     = $bgNo
    # Fertiger Schluessel je Punkt, so wie ihn GS<n>_Hotspots.json erwartet —
    # spart beim Uebertragen der Namen das Zusammenbauen von Hand.
    points   = $points | ForEach-Object {
        [pscustomobject]@{
            key     = "$scenario/$bgNo/$($_.message)"
            index   = $_.index
            message = $_.message
            x       = $_.x
            y       = $_.y
            item    = $_.item
            text    = $_.text
        }
    }
} | ConvertTo-Json -Depth 5 | Set-Content -Path $OutFile -Encoding utf8

"Geschrieben nach $OutFile"
