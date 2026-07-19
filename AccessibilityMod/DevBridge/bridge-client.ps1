# bridge-client.ps1 — Minimaler Client fuer die DevBridge der Accessibility-Mod.
#
# Die Bridge ist reines zeilenbasiertes TCP auf 127.0.0.1, jede Antwort endet mit
# einer Zeile "<<END>>". Dieses Skript ist also nur Bequemlichkeit — mit jeder
# Sprache, die einen Socket oeffnen kann, laesst sich dasselbe machen.
#
# Beispiele:
#     .\bridge-client.ps1 ping
#     .\bridge-client.ps1 state
#     .\bridge-client.ps1 hotspots
#     .\bridge-client.ps1 "hotspot 3"
#     .\bridge-client.ps1 dump
#     .\bridge-client.ps1 -Listen 30        # nur Ereignisse mitlesen
#
# Den Port schreibt die Mod beim Start nach
#     <Spielordner>\UserData\AccessibilityMod\DevBridge\port.txt
# Er wird hier automatisch gelesen; -Port ueberschreibt ihn bei Bedarf.

param(
    [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
    [string[]]$Command,

    [string]$GamePath = "D:\SteamLibrary\steamapps\common\Phoenix Wright Ace Attorney Trilogy",

    [int]$Port = 0,

    # Sekunden, die nur auf Ereignisse ("! ...") gelauscht wird, statt einen
    # Befehl zu schicken. Nuetzlich, um live mitzulesen, was gesprochen wird.
    [int]$Listen = 0
)

# --- Port bestimmen ---------------------------------------------------------
if ($Port -le 0) {
    $portFile = Join-Path $GamePath "UserData\AccessibilityMod\DevBridge\port.txt"
    if (Test-Path $portFile) {
        $Port = [int](Get-Content $portFile -Raw).Trim()
    } else {
        Write-Error "port.txt nicht gefunden ($portFile). Laeuft das Spiel mit der Mod?"
        exit 1
    }
}

# --- Verbinden --------------------------------------------------------------
try {
    $client = New-Object System.Net.Sockets.TcpClient("127.0.0.1", $Port)
} catch {
    Write-Error "Keine Verbindung zu 127.0.0.1:$Port - laeuft das Spiel? ($_)"
    exit 1
}

$stream = $client.GetStream()
$reader = New-Object System.IO.StreamReader($stream, [System.Text.Encoding]::UTF8)
$writer = New-Object System.IO.StreamWriter($stream, [System.Text.Encoding]::UTF8)
$writer.AutoFlush = $true

try {
    if ($Listen -gt 0) {
        # Nur zuhoeren: Ereignisse kommen unaufgefordert und beginnen mit "! ".
        $deadline = (Get-Date).AddSeconds($Listen)
        while ((Get-Date) -lt $deadline) {
            if ($stream.DataAvailable) {
                $line = $reader.ReadLine()
                if ($null -ne $line) { $line }
            } else {
                Start-Sleep -Milliseconds 50
            }
        }
    } else {
        if (-not $Command -or $Command.Count -eq 0) {
            Write-Error "Kein Befehl angegeben. 'help' listet alle auf."
            exit 1
        }

        $writer.WriteLine(($Command -join " "))

        # Bis zur Endemarkierung lesen. Ereigniszeilen ("! ...") koennen
        # dazwischenfunken - die werden durchgereicht, beenden aber nichts.
        while ($true) {
            $line = $reader.ReadLine()
            if ($null -eq $line) { break }          # Verbindung zu
            if ($line -eq "<<END>>") { break }      # Antwort komplett
            $line
        }
    }
} finally {
    $reader.Dispose()
    $writer.Dispose()
    $client.Close()
}
