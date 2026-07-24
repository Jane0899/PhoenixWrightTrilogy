# decode-hotspot-texts.ps1 — reiner DECODE-Helfer (Jana, 24.07.2026 freigegeben).
#
# Zweck: das stumpfe Entschluesseln automatisieren. Er liest je Untersuchungspunkt
# ueber die DevBridge die Rohwerte der zugehoerigen Nachricht und setzt daraus die
# ERSTE SEITE des Spieltextes zusammen — VERBATIM, ohne Umformulierung. Die so
# gewonnenen Texte werden 1:1 zu den Namen (kein nachtraegliches Kuerzen).
#
# Er faellt NICHT selbst ein Urteil (keine Namensfindung, keine Auswahl-Heuristik
# ausser "welcher Kandidat liefert ueberhaupt Text"). Das Benennen/Pruefen bleibt
# beim Menschen.
#
# Grundlagen (aus dem Dekompilat bestaetigt):
#   - Nachricht >= 128 -> Szenario-mdt, Index = Nachricht - 128.
#   - global_work_.scenario == mdt-Pfad-Index == scenario-map-Kandidat.
#   - Zeichen = Wert - 128 (Latin-1) fuer Buchstaben, Satzzeichen UND Umlaute.
#     Sondercodes: 12416 = Leerzeichen, 8341 = Bindestrich.
#     Seiten-/Nachrichtenende: Code 0, 2 oder 3.
#
# Aufruf:
#   powershell -File decode-hotspot-texts.ps1                 # alle Punkte
#   powershell -File decode-hotspot-texts.ps1 -OnlyGame 1     # nur GS1
#   powershell -File decode-hotspot-texts.ps1 -OnlyTable Sce2_0_room003_ck_mess_tbl -OnlyGame 1

param(
    [int]$OnlyGame = 0,          # 0 = alle; sonst 1/2/3
    [string]$OnlyTable = "",     # leer = alle Tabellen
    [int]$Port = 48620
)

$scratch = "C:/Users/JANASC~1/AppData/Local/Temp/claude/C--Users-Jana-Schmidt-games-SRC-PhoenixWrightTrilogy/78bf6283-4dd5-40bf-8325-1d585344bdf4/scratchpad"
$repo = "C:/Users/Jana Schmidt/games/SRC/PhoenixWrightTrilogy"
$outFile = "$scratch/hotspot-texts.json"

$tables = Get-Content "$repo/AccessibilityMod/DevBridge/inspect-tables.json" -Raw | ConvertFrom-Json
$map    = Get-Content "$scratch/scenario-map.json" -Raw | ConvertFrom-Json
$cand = @{}
foreach ($m in $map) { $cand[($m.game.ToString() + '|' + $m.table)] = @($m.candidates) }

# --- Wiederaufnahme: schon vorhandene Ergebnisse behalten ------------------
$done = @{}
$results = @()
if ((Test-Path $outFile) -and $OnlyTable -eq "" -and $OnlyGame -eq 0) {
    $prev = Get-Content $outFile -Raw | ConvertFrom-Json
    foreach ($r in $prev) { $results += $r; $done[($r.game.ToString() + '|' + $r.table + '|' + $r.message)] = $true }
    Write-Host "Wiederaufnahme: $($results.Count) Punkte bereits vorhanden."
}

# --- Eine dauerhafte Bridge-Verbindung -------------------------------------
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

# Argument-Anzahl je Steuercode (0..128), 1:1 aus MessageSystem.code_proc_arg_count_table.
# Ein Steuercode belegt 1 + arg-Anzahl Werte; seine Argumente duerfen NICHT als
# Zeichen ausgegeben werden (sonst Streu-Buchstaben wie "ADieser").
$script:ArgCount = @(
    0,0,0,1,1,2,2,0,2,3, 1,1,1,0,1,2,1,0,3,1,
    0,0,0,1,1,2,4,1,1,1, 3,0,1,0,2,2,0,1,1,2,
    1,1,3,0,1,0,0,2,1,2, 2,5,1,2,1,2,1,1,3,2,
    1,1,1,0,0,0,1,1,1,0, 1,2,2,0,1,1,0,2,1,7,
    1,2,1,0,2,1,2,1,0,1, 1,2,3,0,0,3,4,3,0,0,
    1,2,3,0,0,4,1,3,0,1, 1,1,3,3,0,0,2,4,2,2,
    1,0,1,2,0,1,1,1,0
)

# Erste Seite eines Textes aus den Rohwerten zusammensetzen (verbatim), nach dem
# echten Modell des Spiels: Wert < 128 = Steuercode (mit Argumenten), Wert >= 128 =
# Zeichen (Unicode-Codepunkt = Wert - 128). So wird der Kopf sauber uebersprungen,
# und Argumente landen nicht im Text.
function Decode-Page1([int[]]$vals) {
    $sb = New-Object System.Text.StringBuilder
    $started = $false
    $i = 0
    $n = $vals.Count
    while ($i -lt $n) {
        $v = $vals[$i]
        if ($v -eq 1) { [void]$sb.Append(' '); $i++; continue }          # weicher Zeilenumbruch -> Leerzeichen
        # Seiten-/Nachrichtenende: 0/2/3 = Nachrichtenende, 45 = neue Textbox
        # (Seitenumbruch, argcount 0 - empirisch als Trenner zwischen Anzeigeseiten
        # bestimmt). Nur die ERSTE Anzeigeseite wird als Name genommen.
        if ($started -and ($v -eq 0 -or $v -eq 2 -or $v -eq 3 -or $v -eq 45)) { break }
        if ($v -lt 128) {                                                 # Steuercode: samt Argumenten ueberspringen
            $i += 1 + $script:ArgCount[$v]
            continue
        }
        $cp = $v - 128                                                    # Zeichen (Unicode)
        if ($cp -eq 0x3000) { [void]$sb.Append(' '); $started = $true }   # ideografisches Leerzeichen
        elseif ($cp -eq 0x2015 -or $cp -eq 0x2014 -or $cp -eq 0x2010 -or $cp -eq 0x2212) { [void]$sb.Append('-'); $started = $true } # Gedankenstriche
        elseif ($cp -ge 32) { [void]$sb.Append([char]$cp); $started = $true } # druckbares Zeichen (inkl. Umlaute)
        $i++
    }
    return (($sb.ToString() -replace '\s+', ' ').Trim())
}

function Read-Page1([int]$title, [int]$scen, [int]$idx) {
    $resp = Send-Bridge "mesraw $title $scen $idx 220"
    if ($resp -match 'ERROR') { return $null }
    $werte = ($resp -split "`n" | Where-Object { $_ -match '^werte:' } | Select-Object -First 1)
    if (-not $werte) { return $null }
    $nums = ($werte -replace '^werte:\s*', '') -split '\s+' | Where-Object { $_ -match '^\d+$' } | ForEach-Object { [int]$_ }
    $txt = Decode-Page1 $nums
    if ($txt.Length -lt 2) { return $null }
    return $txt
}

# Lebenszeichen
if ((Send-Bridge "ping") -ne "pong") { Write-Host "FEHLER: Bridge antwortet nicht. Laeuft das Spiel?"; exit 1 }

$processed = 0
foreach ($t in $tables) {
    if ($OnlyGame -ne 0 -and $t.game -ne $OnlyGame) { continue }
    if ($OnlyTable -ne "" -and $t.table -ne $OnlyTable) { continue }

    $title = $t.game - 1
    $cands = $cand[($t.game.ToString() + '|' + $t.table)]
    if (-not $cands -or $cands.Count -eq 0) { continue }  # 6 Sonderfaelle spaeter

    foreach ($p in $t.points) {
        $key = $t.game.ToString() + '|' + $t.table + '|' + $p.message
        if ($done.ContainsKey($key)) { continue }

        # System-mdt (<128) hier nicht abrufbar -> markieren.
        if ($p.message -lt 128) {
            $results += [pscustomobject]@{ game=$t.game; table=$t.table; scenario=$null; message=$p.message; item=$p.item; text="(SYSTEM-mdt, <128)" }
            $done[$key] = $true; continue
        }

        $idx = $p.message - 128
        $text = $null; $used = $null
        foreach ($c in $cands) {
            $tx = Read-Page1 $title $c $idx
            if ($tx) { $text = $tx; $used = $c; break }
        }

        $results += [pscustomobject]@{ game=$t.game; table=$t.table; scenario=$used; message=$p.message; item=$p.item; text=$text }
        $done[$key] = $true
        $processed++
        if ($processed % 50 -eq 0) {
            $results | ConvertTo-Json -Depth 5 | Out-File -Encoding utf8 $outFile
            Write-Host "  ...$processed neue Punkte (zwischengespeichert)"
        }
    }
}

$results | ConvertTo-Json -Depth 5 | Out-File -Encoding utf8 $outFile
$client.Close()
$withText = @($results | Where-Object { $_.text -and -not $_.text.StartsWith('(') }).Count
Write-Host "=== Fertig: $($results.Count) Punkte, davon $withText mit Text. -> $outFile ==="