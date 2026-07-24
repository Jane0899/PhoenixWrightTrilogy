# assemble-gs.ps1 — baut die finale GS<n>_Hotspots.json zusammen:
#   Header + alte bg-basierte Eintraege (Rueckfall) + neuer s<scenario>/<message>-Block.
# Jede Zeile endet mit Komma; ein abschliessender "_end"-Schluessel (vom Parser als
# Kommentar ignoriert) macht das Komma-Handling trivial.
param([int]$Game = 1)

$scratch = "C:/Users/JANASC~1/AppData/Local/Temp/claude/C--Users-Jana-Schmidt-games-SRC-PhoenixWrightTrilogy/78bf6283-4dd5-40bf-8325-1d585344bdf4/scratchpad"
$repo = "C:/Users/Jana Schmidt/games/SRC/PhoenixWrightTrilogy"
$target = "$repo/AccessibilityMod/Data/de/GS$Game`_Hotspots.json"

# Alte bg-basierte Eintraege aus der bestehenden Datei retten (Schluessel beginnt
# mit einer Ziffer -> altes Format <bg>/<msg> oder <szenario>/<bg>/<msg>).
$oldLines = @()
if (Test-Path $target) {
    foreach ($line in (Get-Content $target -Encoding UTF8)) {
        if ($line -match '^\s*"[0-9]') {
            $t = $line.Trim()
            if (-not $t.EndsWith(',')) { $t += ',' }
            $oldLines += ('  ' + $t)
        }
    }
}

# Generierten Block lesen (BOM/Leerzeilen am Anfang entfernen).
$block = (Get-Content "$scratch/gs$Game-block.json" -Raw).TrimStart([char]0xFEFF, "`r", "`n", ' ')

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('{')
[void]$sb.AppendLine('  "_comment": "Namen fuer Untersuchungspunkte GS' + $Game + '. Offline ausgelesen aus der dt. Szenario-mdt.",')
[void]$sb.AppendLine('  "_format": "Neu: s<szenario>/<nachricht> (bg-frei). Alt (Rueckfall): <bg>/<nachricht> bzw. <szenario>/<bg>/<nachricht>.",')
[void]$sb.AppendLine('  "_verbatim": "Texte VERBATIM aus dem Spiel (erste Anzeigeseite), nicht gekuerzt/umformuliert.",')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('  "_altesFormat": "=== Alte, im Spiel bestaetigte bg-Eintraege (Rueckfall fuer Punkte ohne ausgelesenen Text) ===",')
foreach ($l in $oldLines) { [void]$sb.AppendLine($l) }
[void]$sb.Append($block.TrimEnd())
[void]$sb.AppendLine('')
[void]$sb.AppendLine('  "_end": "Ende"')
[void]$sb.AppendLine('}')

$utf8 = New-Object System.Text.UTF8Encoding($false)  # ohne BOM
[System.IO.File]::WriteAllText($target, $sb.ToString(), $utf8)
Write-Host "geschrieben: $target ($($oldLines.Count) alte + Block)"