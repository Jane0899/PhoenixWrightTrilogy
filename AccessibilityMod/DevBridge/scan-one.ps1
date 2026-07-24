# scan-one.ps1 — Diagnose: in welchem Szenario steckt eine Nachricht?
# Testet fuer (title, idx) alle Szenarien 0..max und zeigt den dekodierten
# Text (erste ~40 Zeichen). Dient dazu, die richtige Szenario-Zuordnung zu finden,
# wenn der scenario-map-Kandidat kein Ergebnis liefert.
param([int]$Title, [int]$Idx, [int]$Max = 25, [int]$Port = 48620)

$client = New-Object System.Net.Sockets.TcpClient
$client.Connect("127.0.0.1", $Port)
$stream = $client.GetStream()
$writer = New-Object System.IO.StreamWriter($stream); $writer.NewLine = "`n"; $writer.AutoFlush = $true
$reader = New-Object System.IO.StreamReader($stream)

function Send-Bridge([string]$command) {
    $writer.WriteLine($command)
    $lines = @()
    while ($true) {
        $line = $reader.ReadLine()
        if ($null -eq $line -or $line -eq "<<END>>") { break }
        if ($line.StartsWith("! ")) { continue }
        $lines += $line
    }
    return ($lines -join "`n")
}

function Is-Letter([int]$v) {
    $c = $v - 128
    if ($c -ge 65 -and $c -le 90) { return $true }
    if ($c -ge 97 -and $c -le 122) { return $true }
    if ($c -eq 196 -or $c -eq 214 -or $c -eq 220 -or $c -eq 228 -or $c -eq 246 -or $c -eq 252 -or $c -eq 223) { return $true }
    return $false
}
function Decode-Page1([int[]]$vals) {
    $sb = New-Object System.Text.StringBuilder; $started = $false
    foreach ($v in $vals) {
        if (-not $started) { if (Is-Letter $v) { $started = $true; [void]$sb.Append([char]($v - 128)) }; continue }
        if ($v -eq 0 -or $v -eq 2 -or $v -eq 3) { break }
        if ($v -eq 12416) { [void]$sb.Append(' '); continue }
        if ($v -eq 8341) { [void]$sb.Append('-'); continue }
        if ($v -eq 1) { [void]$sb.Append(' '); continue }
        $c = $v - 128
        if (($c -ge 32 -and $c -le 126) -or ($c -ge 160 -and $c -le 255)) { [void]$sb.Append([char]$c) }
    }
    return (($sb.ToString() -replace '\s+', ' ').Trim())
}

for ($s = 0; $s -le $Max; $s++) {
    $resp = Send-Bridge "mesraw $Title $s $Idx 120"
    if ($resp -match 'ERROR') { continue }
    $werte = ($resp -split "`n" | Where-Object { $_ -match '^werte:' } | Select-Object -First 1)
    if (-not $werte) { continue }
    $nums = ($werte -replace '^werte:\s*', '') -split '\s+' | Where-Object { $_ -match '^\d+$' } | ForEach-Object { [int]$_ }
    $txt = Decode-Page1 $nums
    if ($txt.Length -ge 3) {
        if ($txt.Length -gt 55) { $txt = $txt.Substring(0, 55) }
        Write-Output ("Szenario {0,2}: {1}" -f $s, $txt)
    }
}
$client.Close()