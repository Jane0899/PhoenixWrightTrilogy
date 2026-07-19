# list-chapters.ps1 — Kapitelnamen einer Episode auflisten.
#
# Zweck: Nur Ermittlungskapitel liefern Untersuchungspunkte. Prozesskapitel
# kosten dagegen viele Minuten Zwischensequenz und bringen nichts. Vor dem
# Abgrasen also erst nachsehen, wie die Kapitel heissen.
#
# Erwarteter Ausgangszustand: Titelbildschirm.
#
# Aufruf: powershell -File list-chapters.ps1 -Episode 3 -Max 8

param(
    # 1 = GS1, 2 = GS2, 3 = GS3
    [int]$Game = 1,
    [int]$Episode = 2,
    [int]$Max = 8,
    [int]$Delay = 600
)

$key = "$env:USERPROFILE\.claude\scripts\gamekey.ps1"
$log = "D:\SteamLibrary\steamapps\common\Phoenix Wright Ace Attorney Trilogy\MelonLoader\Latest.log"

function Step([string[]]$k, [int]$wait = 2) {
    $before = (Get-Content $log).Count
    & $key -Delay $Delay $k | Out-Null
    Start-Sleep -Seconds $wait
    $all = Get-Content $log
    if ($all.Count -gt $before) {
        $all[$before..($all.Count - 1)] |
            Where-Object { $_ -match '\[Menu\]' } |
            ForEach-Object { ($_ -replace '^\[\d{2}:\d{2}:\d{2}\.\d{3}\] \[Phoenix_Wright_Accessibility\] \[Menu\] ', '') }
    }
}

# Titelbildschirm -> Kapitelauswahl (Weg siehe enter-chapter.ps1)
Step @("ENTER") 4 | Out-Null
Step @("LEFT") 2  | Out-Null
Step @("ENTER") 4 | Out-Null
# Spielauswahl: GS1 ist vorgewaehlt, fuer GS2/GS3 nach rechts blaettern
for ($g = 1; $g -lt $Game; $g++) { Step @("RIGHT") 2 | Out-Null }
Step @("DOWN") 2  | Out-Null
Step @("ENTER") 4 | Out-Null
Step @("LEFT") 2  | Out-Null
Step @("ENTER") 4 | Out-Null

# Episode waehlen
for ($i = 1; $i -lt $Episode; $i++) { Step @("RIGHT") 2 | Out-Null }

"=== Kapitel der Episode $Episode ==="
for ($i = 0; $i -lt $Max; $i++) {
    $out = Step @("DOWN") 2
    $name = $out | Where-Object { $_ -notmatch '^Episode \d' } | Select-Object -First 1
    if ($name) { "  $i : $name" } else { "  $i : (keine Ansage)" }
}
