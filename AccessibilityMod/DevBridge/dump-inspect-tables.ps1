# dump-inspect-tables.ps1 — Liest ALLE Untersuchungspunkte aller drei Spiele
# direkt aus der Spiel-Assembly aus, ohne dass das Spiel laufen muss.
#
# Hintergrund (Fund vom 19.07.2026):
# Die Klasse `scenario` in Assembly-CSharp.dll enthaelt fuer jede Szene ein
# statisches Feld nach dem Muster
#     Sce<Episode>_<Teil>_room<Nummer>[_Variante]_ck_mess_tbl : INSPECT_DATA[]
# Das sind reine Datentabellen (Nachrichten-ID, Ort, Item, vier Eckpunkte).
# Weil sie statisch sind und ihr Klassenkonstruktor nur Literale zuweist,
# lassen sie sich per Reflection auslesen — das Spiel muss dafuer NICHT laufen.
#
# Warum das wichtig ist: Der urspruengliche Plan war, jede Szene im laufenden
# Spiel anzusteuern (Rechner-Uebernahme, stundenlang). Mit diesem Weg liegen
# saemtliche Hotspot-Daten in Sekunden vor, ohne Janas Rechner zu blockieren.
#
# Aufruf:
#     powershell -File dump-inspect-tables.ps1
#     powershell -File dump-inspect-tables.ps1 -OutFile pfad\zur\datei.json

param(
    [string]$Managed = "D:\SteamLibrary\steamapps\common\Phoenix Wright Ace Attorney Trilogy\PWAAT_Data\Managed",
    [string]$OutFile = "C:\Users\Jana Schmidt\games\SRC\PhoenixWrightTrilogy\AccessibilityMod\DevBridge\inspect-tables.json"
)

# Alle Unity-Abhaengigkeiten zuerst laden, sonst liefert die Reflection
# teilweise null (halb aufgeloeste Typen).
Get-ChildItem "$Managed\*.dll" | ForEach-Object {
    try { [void][System.Reflection.Assembly]::LoadFile($_.FullName) } catch {}
}
$asm = [System.Reflection.Assembly]::LoadFile("$Managed\Assembly-CSharp.dll")
$flags = [System.Reflection.BindingFlags]'Public,NonPublic,Static'

# Jedes Spiel hat eine eigene Klasse mit seinen Szenentabellen. Das war beim
# ersten Auslesen am 19.07.2026 uebersehen worden — dadurch enthielt der erste
# Abzug nur GS1. Alle drei muessen gelesen werden.
$classNames = @{
    "scenario"     = 1
    "scenario_GS2" = 2
    "scenario_GS3" = 3
}

$tables = @()
foreach ($className in $classNames.Keys) {
    $cls = $asm.GetType($className)
    if ($null -eq $cls) {
        Write-Warning "Klasse '$className' nicht gefunden - uebersprungen."
        continue
    }
    foreach ($f in ($cls.GetFields($flags) | Where-Object { $_.FieldType.ToString() -match 'INSPECT_DATA' })) {
        $tables += [pscustomobject]@{ Field = $f; Game = $classNames[$className] }
    }
}

$result = @()
$skipped = 0

foreach ($entry in $tables) {
    $t = $entry.Field
    $rows = $null
    try { $rows = $t.GetValue($null) } catch { $skipped++; continue }
    if ($null -eq $rows) { $skipped++; continue }

    # Szenenname zerlegen: Sce2_2_room003_3_ck_mess_tbl
    #   -> Episode 2, Teil 2, Raum 003, Variante 3
    # ACHTUNG: Die erste Zahl ist die EPISODE, nicht das Spiel. Das Spiel steht
    # in der Klasse, aus der die Tabelle stammt (scenario / _GS2 / _GS3).
    $game = $entry.Game
    $episode = $null; $part = $null; $room = $null; $variant = $null
    if ($t.Name -match '^Sce(\d+)_(\d+)_room([0-9a-z]+?)(?:_(\d+))?_ck_mess_tbl(?:_(\d+))?$') {
        $episode = [int]$Matches[1]
        $part    = [int]$Matches[2]
        $room    = $Matches[3]
        $variant = if ($Matches[4]) { $Matches[4] } elseif ($Matches[5]) { $Matches[5] } else { $null }
    }

    $points = @()
    foreach ($r in $rows) {
        if ($null -eq $r) { continue }

        # place = 254 markiert deaktivierte Punkte, uint.MaxValue das Listenende
        # (gleiche Regeln wie im HotspotNavigator der Mod).
        if ($r.place -eq [uint32]::MaxValue) { break }

        $cx = [math]::Round(($r.x0 + $r.x1 + $r.x2 + $r.x3) / 4)
        $cy = [math]::Round(($r.y0 + $r.y1 + $r.y2 + $r.y3) / 4)

        $points += [pscustomobject]@{
            message  = [int]$r.message
            place    = [int]$r.place
            item     = [int]$r.item
            centerX  = $cx
            centerY  = $cy
            x0 = [int]$r.x0; y0 = [int]$r.y0
            x1 = [int]$r.x1; y1 = [int]$r.y1
            x2 = [int]$r.x2; y2 = [int]$r.y2
            x3 = [int]$r.x3; y3 = [int]$r.y3
            disabled = ($r.place -eq 254)
        }
    }

    $result += [pscustomobject]@{
        table   = $t.Name
        game    = $game
        episode = $episode
        part    = $part
        room    = $room
        variant = $variant
        count   = $points.Count
        points  = $points
    }
}

$result | ConvertTo-Json -Depth 6 | Set-Content -Path $OutFile -Encoding utf8

$totalPoints = ($result | Measure-Object -Property count -Sum).Sum
"Tabellen gelesen : $($result.Count) (uebersprungen: $skipped)"
"Punkte gesamt    : $totalPoints"
foreach ($g in 1..3) {
    $sub = $result | Where-Object { $_.game -eq $g }
    $p = ($sub | Measure-Object -Property count -Sum).Sum
    "  GS${g}: $($sub.Count) Szenen, $p Punkte"
}
"Geschrieben nach : $OutFile"
