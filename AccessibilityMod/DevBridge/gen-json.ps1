# gen-json.ps1 — erzeugt aus hotspot-texts.json den JSON-Block fuer eine
# Hotspot-Namensdatei (Schluessel s<scenario>/<message>, Text VERBATIM).
# Dedupliziert gleiche (scenario, message), sortiert nach Szenario/Nachricht,
# gruppiert mit Szenario-Kommentaren. JSON-escaped (Anfuehrungszeichen im Text!).
param([int]$Game = 1)

$scratch = "C:/Users/JANASC~1/AppData/Local/Temp/claude/C--Users-Jana-Schmidt-games-SRC-PhoenixWrightTrilogy/78bf6283-4dd5-40bf-8325-1d585344bdf4/scratchpad"
$data = Get-Content "$scratch/hotspot-texts.json" -Raw | ConvertFrom-Json
$rows = $data | Where-Object { $_.game -eq $Game -and $_.text -and -not $_.text.StartsWith('(') }

# Dedup: gleicher (scenario, message) -> ein Eintrag.
$seen = @{}
$clean = @()
foreach ($r in $rows) {
    $k = "$($r.scenario)/$($r.message)"
    if ($seen.ContainsKey($k)) { continue }
    $seen[$k] = $true
    $clean += $r
}
$clean = $clean | Sort-Object { [int]$_.scenario }, { [int]$_.message }

$sb = New-Object System.Text.StringBuilder
$lastScen = -1
foreach ($r in $clean) {
    if ($r.scenario -ne $lastScen) {
        [void]$sb.AppendLine("")
        [void]$sb.AppendLine("  ""_s$($r.scenario)"": ""=== Szenario $($r.scenario) ==="",")
        $lastScen = $r.scenario
    }
    # JSON-Escape: Backslash zuerst, dann Anfuehrungszeichen.
    $t = $r.text.Replace('\', '\\').Replace('"', '\"')
    [void]$sb.AppendLine("  ""s$($r.scenario)/$($r.message)"": ""$t"",")
}

# In Datei schreiben (ohne letztes Komma-Problem -> beim Einbau geregelt).
$out = "$scratch/gs$Game-block.json"
$sb.ToString() | Out-File -Encoding utf8 $out
Write-Host "Block: $($clean.Count) Eintraege -> $out"