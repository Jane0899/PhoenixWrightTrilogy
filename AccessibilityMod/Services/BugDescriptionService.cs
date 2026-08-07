using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using AccessibilityMod.Core;
using UnityAccessibilityLib;

namespace AccessibilityMod.Services
{
    /// <summary>
    /// Zeigt nach einer F9-Bug-Erfassung ein Eingabefenster, in das Jana kurz
    /// beschreiben kann, was an dem gefundenen Fehler falsch war — waehrend sie
    /// weiterspielt. Die Beschreibung landet als zusaetzliche Zeile im selben
    /// Bug-Block in report.txt (BugReportService schreibt den Rest).
    ///
    /// Warum ein EXTERNER Dialog (PowerShell + System.Windows.Forms) statt eines
    /// selbstgebauten Unity-Eingabefelds?
    /// 1. Zugaenglichkeit: Ein natives WinForms-Fenster wird von NVDA/JAWS sofort
    ///    erkannt und vorgelesen (Fenstertitel, Beschriftung, Fokus) — das muesste
    ///    ein Unity-OnGUI-Textfeld erst muehsam nachbauen, mit ungewissem Ergebnis.
    /// 2. Eingabekonflikt: Solange das Fenster den Tastaturfokus haelt, bekommt
    ///    das Spiel GAR KEINE Tastendruecke mehr (Windows leitet Eingaben immer an
    ///    das fokussierte Fenster). So tippt Jana ihren Text, ohne dass Menuecursor
    ///    oder Dialogfortschritt im Spiel versehentlich mitreagieren — ganz ohne
    ///    dass wir dem Spiel selbst die Eingabe wegfangen muessten (waere ueber
    ///    Unitys altes Input-System gar nicht sauber moeglich).
    /// Der Dialog laeuft in einem eigenen Prozess auf einem Hintergrund-Thread:
    /// Das Spiel friert waehrend der Eingabe NICHT ein, Musik und Animation laufen
    /// weiter — nur der Tastaturfokus ist kurz woanders. Sobald sie fertig ist,
    /// bekommt das Spielfenster automatisch wieder den Fokus (Standard-Windows-
    /// Verhalten beim Schliessen eines Fensters).
    /// </summary>
    public static class BugDescriptionService
    {
        // Verhindert, dass zwei Hintergrund-Threads gleichzeitig in dieselbe
        // report.txt schreiben (z. B. wenn zwei Beschreibungen kurz hintereinander
        // abgeschlossen werden) und sich dabei die Datei gegenseitig zerschiessen.
        private static readonly object _fileLock = new object();

        /// <summary>
        /// Stoesst die Eingabe asynchron an und kehrt sofort zurueck — der
        /// Spielablauf wird nicht blockiert, waehrend Jana tippt oder ueberlegt.
        /// </summary>
        public static void RequestDescriptionAsync(int bugNumber, string reportFile)
        {
            var thread = new System.Threading.Thread(() => RunDialogAndPatch(bugNumber, reportFile));
            // Hintergrund-Thread: darf den Spielprozess beim Beenden nicht aufhalten,
            // falls Jana das Spiel schliesst, waehrend der Dialog noch offen steht.
            thread.IsBackground = true;
            thread.Start();
        }

        private static void RunDialogAndPatch(int bugNumber, string reportFile)
        {
            string description = null;
            try
            {
                description = ShowInputDialog(bugNumber);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    "Bug-Beschreibungsdialog fehlgeschlagen: " + ex.Message
                );
                CoroutineRunner.Instance?.EnqueueMainThreadAction(
                    () => SpeechManager.Announce(L.Get("bug_report.description_error"))
                );
                return;
            }

            if (string.IsNullOrEmpty(description))
            {
                // Abgebrochen (Escape) oder leer gelassen und mit "Speichern"
                // bestaetigt — kein Fehler, aber Jana per Ansage bestaetigen,
                // dass der Dialog nicht einfach spurlos haengen geblieben ist.
                CoroutineRunner.Instance?.EnqueueMainThreadAction(
                    () => SpeechManager.Announce(L.Get("bug_report.description_skipped"))
                );
                return;
            }

            try
            {
                lock (_fileLock)
                {
                    PatchDescriptionIntoReport(reportFile, bugNumber, description);
                }

                // Sprachausgabe muss auf dem Unity-Hauptthread laufen (SpeechManager/
                // UniversalSpeech sind nicht als thread-sicher dokumentiert) — deshalb
                // ueber die Aktionswarteschlange des CoroutineRunner nachreichen statt
                // direkt von hier aus aufzurufen.
                CoroutineRunner.Instance?.EnqueueMainThreadAction(
                    () => SpeechManager.Announce(L.Get("bug_report.description_saved", bugNumber))
                );
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    "Beschreibung konnte nicht in report.txt gespeichert werden: " + ex.Message
                );
            }
        }

        /// <summary>
        /// Baut ein kleines PowerShell/WinForms-Skript, fuehrt es als eigenen,
        /// unsichtbaren Prozess aus (nur das Eingabefenster selbst ist sichtbar,
        /// die PowerShell-Konsole dahinter nicht) und liefert den eingegebenen
        /// Text zurueck — oder null bei Abbruch/leerer Eingabe.
        /// </summary>
        private static string ShowInputDialog(int bugNumber)
        {
            string scriptPath = Path.Combine(
                Path.GetTempPath(),
                "pwaat_bugdesc_" + Guid.NewGuid().ToString("N") + ".ps1"
            );

            // Titeltext ist bei uns immer fest (nur die Bug-Nummer wechselt), aber
            // Hochkommata sicherheitshalber verdoppeln — so bricht das PowerShell-
            // Skript nicht, falls der Titelaufbau sich mal aendert.
            string title = ("Bug " + bugNumber + " - Beschreibung").Replace("'", "''");

            // Ergebnis kommt ueber eine Datei zurueck, NICHT ueber die Prozess-
            // Standardausgabe. Grund: Write-Output auf die Konsole laeuft ueber
            // die Konsolen-Codepage von PowerShell, die C#s Process.StandardOutput
            // nicht zuverlaessig kennt — bei deutschen Umlauten (ä/ö/ü) kam dabei
            // Muell heraus ("tats�chlich" statt "tatsächlich", von Jana in Bug 7/8
            // gemeldet). Eine Datei mit explizit angegebener UTF8-Kodierung auf
            // beiden Seiten (hier beim Schreiben, in ReadResultFile() beim Lesen)
            // umgeht das Codepage-Problem komplett.
            string resultPath = Path.Combine(
                Path.GetTempPath(),
                "pwaat_bugdesc_result_" + Guid.NewGuid().ToString("N") + ".txt"
            );
            string resultPathEscaped = resultPath.Replace("'", "''");

            // WICHTIG: Der Beschreibungstext selbst (was Jana tippt) landet NICHT
            // in diesem Skript — er kommt erst hinterher ueber die Ergebnisdatei
            // zurueck. Damit gibt es keinerlei Injection-Risiko durch Sonder-
            // zeichen, die sie eintippt.
            string script =
                "Add-Type -AssemblyName System.Windows.Forms\n"
                + "Add-Type -AssemblyName System.Drawing\n"
                + "$form = New-Object System.Windows.Forms.Form\n"
                + "$form.Text = '"
                + title
                + "'\n"
                + "$form.StartPosition = 'CenterScreen'\n"
                + "$form.TopMost = $true\n"
                + "$form.Width = 480\n"
                + "$form.Height = 160\n"
                + "$form.FormBorderStyle = 'FixedDialog'\n"
                + "$form.MinimizeBox = $false\n"
                + "$form.MaximizeBox = $false\n"
                + "$label = New-Object System.Windows.Forms.Label\n"
                + "$label.Text = 'Kurze Beschreibung des Bugs (Enter = speichern, Escape = ueberspringen):'\n"
                + "$label.AutoSize = $true\n"
                + "$label.Left = 10\n"
                + "$label.Top = 10\n"
                + "$label.Width = 450\n"
                + "$form.Controls.Add($label)\n"
                + "$textBox = New-Object System.Windows.Forms.TextBox\n"
                + "$textBox.Left = 10\n"
                + "$textBox.Top = 45\n"
                + "$textBox.Width = 450\n"
                // AccessibleName explizit setzen: manche Screenreader verlassen
                // sich nicht zuverlaessig auf die raeumliche Naehe zum Label.
                + "$textBox.AccessibleName = 'Beschreibung fuer Bug "
                + bugNumber
                + "'\n"
                + "$form.Controls.Add($textBox)\n"
                + "$okButton = New-Object System.Windows.Forms.Button\n"
                + "$okButton.Text = 'Speichern'\n"
                + "$okButton.Left = 300\n"
                + "$okButton.Top = 80\n"
                + "$okButton.DialogResult = [System.Windows.Forms.DialogResult]::OK\n"
                + "$form.Controls.Add($okButton)\n"
                + "$cancelButton = New-Object System.Windows.Forms.Button\n"
                + "$cancelButton.Text = 'Ueberspringen'\n"
                + "$cancelButton.Left = 380\n"
                + "$cancelButton.Top = 80\n"
                + "$cancelButton.DialogResult = [System.Windows.Forms.DialogResult]::Cancel\n"
                + "$form.Controls.Add($cancelButton)\n"
                + "$form.AcceptButton = $okButton\n"
                + "$form.CancelButton = $cancelButton\n"
                // Fokus sofort ins Textfeld, damit sie ohne Tab-Druecke direkt
                // lostippen kann - wichtig fuer eine schnelle Notiz waehrend des
                // Spielens.
                + "$form.Add_Shown({ $textBox.Focus() })\n"
                + "$result = $form.ShowDialog()\n"
                + "if ($result -eq [System.Windows.Forms.DialogResult]::OK) {\n"
                + "  [System.IO.File]::WriteAllText('"
                + resultPathEscaped
                + "', $textBox.Text, [System.Text.Encoding]::UTF8)\n"
                + "}\n";

            // ASCII genuegt hier: das Skript selbst enthaelt bewusst keine
            // Umlaute (siehe "ueberspringen" statt "überspringen") - Lehre aus
            // frueheren PowerShell-5.1-Mojibake-Problemen in diesem Projekt
            // (siehe decode-missing.ps1). Nur die Ergebnisdatei braucht UTF8,
            // weil DORT Janas eigener, nicht auf ASCII beschraenkter Text landet.
            File.WriteAllText(scriptPath, script, Encoding.ASCII);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments =
                        "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \""
                        + scriptPath
                        + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using (Process process = Process.Start(psi))
                {
                    process.WaitForExit();
                }

                if (!File.Exists(resultPath))
                    return null; // Abgebrochen (Escape/Ueberspringen) - keine Ergebnisdatei geschrieben.

                string text = File.ReadAllText(resultPath, Encoding.UTF8);
                return string.IsNullOrEmpty(text) ? null : text.Trim();
            }
            finally
            {
                // Aufraeumen, auch wenn PowerShell fehlschlaegt - keine
                // Temp-Datei-Leichen pro Bugmeldung.
                try
                {
                    File.Delete(scriptPath);
                }
                catch { }
                try
                {
                    File.Delete(resultPath);
                }
                catch { }
            }
        }

        /// <summary>
        /// Fuegt eine description-Zeile in den passenden Bug-Block von report.txt
        /// ein — direkt nach der screenshot-Zeile, damit der Block optisch
        /// zusammenbleibt und beim Lesen sofort neben Screenshot/Kontext steht.
        /// </summary>
        private static void PatchDescriptionIntoReport(
            string reportFile,
            int bugNumber,
            string description
        )
        {
            if (!File.Exists(reportFile))
                return;

            string marker = "=== BUG " + bugNumber + " ===";
            string[] lines = File.ReadAllLines(reportFile);

            int blockStart = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith(marker))
                {
                    blockStart = i;
                    break;
                }
            }
            if (blockStart < 0)
                return; // Block nicht (mehr) gefunden - nichts zu tun.

            // Einfuegestelle: direkt nach der screenshot-Zeile dieses Blocks.
            // Falls die (aus irgendeinem Grund) fehlt, ersatzweise vor der
            // naechsten Leerzeile/dem naechsten Block/Dateiende.
            int insertAt = -1;
            for (int i = blockStart; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("screenshot="))
                {
                    insertAt = i + 1;
                    break;
                }
                if (i > blockStart && (lines[i].Length == 0 || lines[i].StartsWith("=== BUG ")))
                {
                    insertAt = i;
                    break;
                }
            }
            if (insertAt < 0)
                insertAt = lines.Length;

            // Wie bei last_dialogue an anderer Stelle: keine Escaping-Logik fuer
            // enthaltene Anfuehrungszeichen. Die Datei ist fuer mich zum Lesen
            // gedacht, nicht maschinell geparst - ein seltener Sonderfall darin
            // ist harmlos.
            string descriptionLine = "description=\"" + description + "\"";

            var result = new StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                if (i == insertAt)
                    result.Append(descriptionLine).Append("\n");
                result.Append(lines[i]).Append("\n");
            }
            if (insertAt == lines.Length)
                result.Append(descriptionLine).Append("\n");

            File.WriteAllText(reportFile, result.ToString());
        }
    }
}
