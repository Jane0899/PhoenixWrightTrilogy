# decode-missing.ps1 — findet NICHT benannte Untersuchungspunkte und dekodiert sie.
#
# Kernidee (Lehre vom 26.07.2026): Die Kandidaten-Szenarien werden LIVE aus der
# Bridge rekonstruiert (`scenarios <title>`), NICHT aus einer vorab gespeicherten
# scenario-map. Die im Repo liegende scenario-map.json ist unbrauchbar (sie enthaelt
# nur Zaehlwerte, nicht die Szenario-Indizes), und jede statische Map veraltet.
# Der Weg ueber `scenarios` ist autoritativ und funktioniert fuer GS1/GS2/GS3 gleich.
#
# Zuordnung: Tabelle `Sce<Ep>_<Teil>_room<N>` -> Datei `sc<Ep>_<Teil>[a-d]` ->
# alle passenden Pfad-Indizes = Kandidaten. Punkt gilt als benannt, wenn in der
# Namensdatei ein Schluessel `s<kandidat>/<message>` existiert.
#
# Ein Punkt ist NICHT (sinnvoll) benennbar, wenn:
#   - message == 65535 (Tabellenende-Sentinel, kein echter Punkt),
#   - message  < 128   (SYSTEM-mdt; verschluesselt, offline nicht dekodierbar),
#   - item    != 255   (Name kommt zur Laufzeit vom Beweisstueck, Loesung 1).
# Alles andere wird dekodiert; das Ergebnis ist die Grundlage fuers Benennen VON HAND.
#
# Aufruf:
#   powershell -File decode-missing.ps1 -Game 1
#   powershell -File decode-missing.ps1 -Game 2 -Port 48620 -Out C:\pfad\missing-gs2.json

param(
    [Parameter(Mandatory=$true)][int]$Game,   # 1=GS1, 2=GS2, 3=GS3
    [int]$Port = 48620,
    [string]$Out = ""
)

$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent   # .../PhoenixWrightTrilogy
$title = $Game - 1
if ($Out -eq "") { $Out = Join-Path $env:TEMP ("missing-gs{0}.json" -f $Game) }

# --- Bridge ---------------------------------------------------------------
$client = New-Object System.Net.Sockets.TcpClient
$client.Connect("127.0.0.1", $Port)
$stream = $client.GetStream()
$writer = New-Object System.IO.StreamWriter($stream); $writer.NewLine = "`n"; $writer.AutoFlush = $true
$reader = New-Object System.IO.StreamReader($stream)
function Send-Bridge([string]$cmd) {
    $writer.WriteLine($cmd)
    $lines = @()
    while ($true) {
        $line = $reader.ReadLine()
        if ($null -eq $line -or $line -eq "<<END>>") { break }
        if ($line.StartsWith("! ")) { continue }
        $lines += $line
    }
    return ($lines -join "`n")
}
if ((Send-Bridge "ping") -ne "pong") { Write-Host "Bridge tot — laeuft das Spiel?"; exit 1 }

# --- Pfadtabelle -> stem->index (z. B. "sc2_0" -> 5) ----------------------
$stemToIdx = @{}
foreach ($ln in (Send-Bridge "scenarios $title") -split "`n") {
    if ($ln -match ('^(\d+)\s+GS{0}/scenario/(sc[0-9]+_[0-9]+[a-z]?)_text\.mdt' -f $Game)) {
        $stemToIdx[$matches[2]] = [int]$matches[1]
    }
}
function Get-Candidates([int]$ep, [int]$part) {
    $base = "sc$ep`_$part"
    $c = @()
    foreach ($k in $stemToIdx.Keys) { if ($k -eq $base -or $k -match "^$base[a-z]$") { $c += $stemToIdx[$k] } }
    return ($c | Sort-Object)
}

# --- validierter Decoder (Steuercodes samt Argumenten ueberspringen) -------
$script:ArgCount = @(
    0,0,0,1,1,2,2,0,2,3, 1,1,1,0,1,2,1,0,3,1,
    0,0,0,1,1,2,4,1,1,1, 3,0,1,0,2,2,0,1,1,2,
    1,1,3,0,1,0,0,2,1,2, 2,5,1,2,1,2,1,1,3,2,
    1,1,1,0,0,0,1,1,1,0, 1,2,2,0,1,1,0,2,1,7,
    1,2,1,0,2,1,2,1,0,1, 1,2,3,0,0,3,4,3,0,0,
    1,2,3,0,0,4,1,3,0,1, 1,1,3,3,0,0,2,4,2,2,
    1,0,1,2,0,1,1,1,0
)
function Decode-Page1([int[]]$vals) {
    $sb = New-Object System.Text.StringBuilder
    $started = $false; $i = 0; $n = $vals.Count
    while ($i -lt $n) {
        $v = $vals[$i]
        if ($v -eq 1) { [void]$sb.Append(' '); $i++; continue }
        if ($started -and ($v -eq 0 -or $v -eq 2 -or $v -eq 3 -or $v -eq 45)) { break }
        if ($v -lt 128) { $i += 1 + $script:ArgCount[$v]; continue }
        $cp = $v - 128
        if ($cp -eq 0x3000) { [void]$sb.Append(' '); $started = $true }
        elseif ($cp -eq 0x2015 -or $cp -eq 0x2014 -or $cp -eq 0x2010 -or $cp -eq 0x2212) { [void]$sb.Append('-'); $started = $true }
        elseif ($cp -ge 32) { [void]$sb.Append([char]$cp); $started = $true }
        $i++
    }
    return (($sb.ToString() -replace '\s+', ' ').Trim())
}
function Read-Page1([int]$scen, [int]$idx) {
    $resp = Send-Bridge "mesraw $title $scen $idx 220"
    if ($resp -match 'ERROR') { return $null }
    $werte = ($resp -split "`n" | Where-Object { $_ -match '^werte:' } | Select-Object -First 1)
    if (-not $werte) { return $null }
    $nums = ($werte -replace '^werte:\s*', '') -split '\s+' | Where-Object { $_ -match '^\d+$' } | ForEach-Object { [int]$_ }
    $txt = Decode-Page1 $nums
    if ($txt.Length -lt 2) { return $null }
    return $txt
}

# --- schon benannte s<C>/<M>-Schluessel -----------------------------------
$named = @{}
$hot = Get-Content (Join-Path $repo ("AccessibilityMod/Data/de/GS{0}_Hotspots.json" -f $Game)) -Raw | ConvertFrom-Json
foreach ($p in $hot.PSObject.Properties) { if ($p.Name -match '^s(\d+)/(\d+)$') { $named[$p.Name] = $true } }
Write-Host "Benannte s<C>/<M>-Schluessel: $($named.Count)"

# --- Tabellen durchgehen ---------------------------------------------------
$tables = Get-Content (Join-Path $repo "AccessibilityMod/DevBridge/inspect-tables.json") -Raw | ConvertFrom-Json
$missing = @()
$namedCount = 0
foreach ($t in $tables) {
    if ($t.game -ne $Game) { continue }
    $cands = Get-Candidates $t.episode $t.part
    foreach ($p in $t.points) {
        $m = [int]$p.message
        if ($m -eq 65535) { continue }                          # Tabellenende-Sentinel
        if ($m -lt 128) {
            $missing += [pscustomobject]@{ table=$t.table; message=$m; item=$p.item; type="SYSTEM"; scenario=$null; text=$null; cands=($cands -join ',') }
            continue
        }
        $isNamed = $false
        foreach ($c in $cands) { if ($named.ContainsKey("s$c/$m")) { $isNamed = $true; break } }
        if ($isNamed) { $namedCount++; continue }
        $idx = $m - 128
        $text = $null; $used = $null
        foreach ($c in $cands) { $tx = Read-Page1 $c $idx; if ($tx) { $text = $tx; $used = $c; break } }
        $missing += [pscustomobject]@{ table=$t.table; message=$m; item=$p.item; type="SCENARIO"; scenario=$used; text=$text; cands=($cands -join ',') }
    }
}
$client.Close()

$missing | ConvertTo-Json -Depth 5 | Out-File -Encoding utf8 $Out
$sys = @($missing | Where-Object { $_.type -eq 'SYSTEM' }).Count
$noText = @($missing | Where-Object { $_.type -eq 'SCENARIO' -and -not $_.text }).Count
$withText = @($missing | Where-Object { $_.text }).Count
Write-Host "=== GS$Game : $($missing.Count) offene Punkte ==="
Write-Host "  bereits benannt (uebersprungen): $namedCount"
Write-Host "  SYSTEM (<128, offline nicht dekodierbar): $sys"
Write-Host "  SCENARIO ohne Text (item pruefen / Nicht-Punkt): $noText"
Write-Host "  SCENARIO mit Text (zum Benennen VON HAND): $withText"
Write-Host "-> $Out"
