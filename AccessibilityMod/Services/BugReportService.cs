using System;
using System.Globalization;
using System.IO;
using System.Text;
using AccessibilityMod.Patches;
using UnityAccessibilityLib;
using UnityEngine;

namespace AccessibilityMod.Services
{
    /// <summary>
    /// Haelt auf Tastendruck einen vollstaendigen Schnappschuss der aktuellen
    /// Szene fest, damit Jana beim Testen einen gefundenen Fehler markieren kann,
    /// ohne die Stelle muehsam beschreiben zu muessen.
    ///
    /// Warum ueberhaupt? Jana testet mit Screenreader. Faellt ihr in einer Szene
    /// etwas auf (falscher Name, fehlende Ansage, kaputte Navigation), hilft mir
    /// spaeter nur eine exakte Fundstelle: welches Spiel, welches Szenario, welcher
    /// Hintergrund, welcher Untersuchungspunkt, welche Dialogzeile lief zuletzt.
    /// Genau das schreibt dieser Service in eine Datei, die ich hinterher lese.
    /// Jana drueckt nur eine Taste und sagt mir im Chat, was falsch war.
    ///
    /// Bewusst defensiv gebaut: JEDES Feld steht in seinem eigenen try/catch (wie
    /// beim Bridge-Befehl "state"). Ein Fehler beim Auslesen eines einzelnen Werts
    /// darf niemals verhindern, dass der Rest des Berichts geschrieben wird — sonst
    /// verliert Jana genau in dem Moment die Fundstelle, in dem sie sie braucht.
    /// </summary>
    public static class BugReportService
    {
        /// <summary>
        /// Erfasst den aktuellen Zustand, haengt ihn an die Berichtsdatei an und
        /// loest ein Bildschirmfoto aus. Bestaetigt per Sprachausgabe, damit Jana
        /// ohne Blick auf den Bildschirm weiss, dass die Erfassung geklappt hat.
        /// </summary>
        public static void Capture()
        {
            try
            {
                // Zielordner: dasselbe UserData-Verzeichnis, das auch die Bridge
                // nutzt (relativ zum Spielordner). Muss existieren, bevor wir
                // hineinschreiben.
                string dir = Path.Combine(
                    Environment.CurrentDirectory,
                    Path.Combine("UserData", Path.Combine("AccessibilityMod", "BugReports"))
                );
                Directory.CreateDirectory(dir);

                string reportFile = Path.Combine(dir, "report.txt");

                // Fortlaufende Nummer aus der Datei ableiten, damit sie einen
                // Spielneustart ueberlebt: einfach die bisherigen Bloecke zaehlen.
                // So kann Jana im Chat "Bug 3" sagen und ich finde ihn wieder.
                int number = CountExistingReports(reportFile) + 1;

                // Zeitstempel dient doppelt: im Bericht als Datum und als
                // eindeutiger Dateiname fuers Bildschirmfoto.
                string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
                string shotFile = Path.Combine(dir, "bug_" + number + "_" + stamp + ".png");

                StringBuilder sb = new StringBuilder();
                sb.Append("=== BUG ").Append(number).Append(" === ")
                    .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
                    .Append("\n");

                AppendCoreState(sb);
                AppendDialogue(sb);
                AppendInvestigation(sb);

                sb.Append("screenshot=").Append(Path.GetFileName(shotFile)).Append("\n");
                sb.Append("\n"); // Leerzeile zwischen Bloecken

                // Anhaengen statt ueberschreiben: alle Funde einer Test-Session
                // sammeln sich in einer Datei.
                File.AppendAllText(reportFile, sb.ToString());

                // Bildschirmfoto zuletzt: Unity schreibt es erst am Frame-Ende.
                // Das Spiel hat beim Testen den Fokus (Jana spielt gerade), also
                // zeigt das Foto genau die Szene, die sie meint.
                try
                {
                    ScreenCapture.CaptureScreenshot(shotFile);
                }
                catch (Exception exShot)
                {
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                        "Bug-Screenshot fehlgeschlagen: " + exShot.Message
                    );
                }

                AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                    "Bug report " + number + " gespeichert in " + reportFile
                );

                // Bestaetigung fuer Jana. Lokalisiert, weil sie es hoert; die
                // Nummer nennt ihr, worauf sie sich im Chat beziehen kann.
                SpeechManager.Announce(L.Get("bug_report.saved", number));
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    "Bug-Erfassung fehlgeschlagen: " + ex.Message
                );
                SpeechManager.Announce(L.Get("bug_report.error"));
            }
        }

        /// <summary>
        /// Spiel, Szenario, Hintergrund und aktiver Modus — die grobe Verortung.
        /// </summary>
        private static void AppendCoreState(StringBuilder sb)
        {
            try
            {
                sb.Append("game=").Append(GameName());
                sb.Append(" scenario=").Append(SafeScenario());
                sb.Append("\n");
            }
            catch { }

            try
            {
                if (bgCtrl.instance != null)
                {
                    sb.Append("bg_no=").Append(bgCtrl.instance.bg_no);
                    sb.Append(" bg_pos_x=").Append(
                        bgCtrl.instance.bg_pos_x.ToString("0.#", CultureInfo.InvariantCulture)
                    );
                    sb.Append("\n");
                }
            }
            catch { }

            try
            {
                sb.Append("mode=").Append(CurrentModeName()).Append("\n");
            }
            catch { }
        }

        /// <summary>
        /// Die zuletzt vom Mod ausgegebene Dialogzeile. Oft der wichtigste Hinweis:
        /// "an dieser Zeile stimmte etwas nicht". _lastAnnouncedText ist genau der
        /// bereinigte Text, den Jana zuletzt gehoert hat.
        /// </summary>
        private static void AppendDialogue(StringBuilder sb)
        {
            try
            {
                string last = DialoguePatches._lastAnnouncedText;
                if (!string.IsNullOrEmpty(last))
                    sb.Append("last_dialogue=\"").Append(last).Append("\"\n");
            }
            catch { }
        }

        /// <summary>
        /// Wenn Jana im Ermittlungsmodus war: die komplette Punkteliste mit dem
        /// exakten Namensschluessel s&lt;scenario&gt;/&lt;message&gt; und eine Markierung,
        /// auf welchem Punkt sie stand. Damit kann ich einen Namensfehler direkt
        /// der richtigen Zeile in GS?_Hotspots.json zuordnen.
        /// </summary>
        private static void AppendInvestigation(StringBuilder sb)
        {
            try
            {
                if (!AccessibilityState.IsInInvestigationMode())
                    return;

                HotspotNavigator.RefreshHotspots();
                var list = HotspotNavigator.GetHotspots();
                if (list == null || list.Count == 0)
                {
                    sb.Append("hotspots=0\n");
                    return;
                }

                int scenario = SafeScenario();
                int current = HotspotNavigator.GetCurrentIndex();

                sb.Append("hotspots=").Append(list.Count);
                if (current >= 0 && current < list.Count)
                    sb.Append(" current_point=").Append(current + 1);
                sb.Append("\n");

                for (int i = 0; i < list.Count; i++)
                {
                    var h = list[i];
                    // Der Pfeil markiert den Punkt, auf dem der Cursor stand —
                    // so sehe ich sofort, welchen Jana gemeint hat.
                    sb.Append(i == current ? "  -> " : "     ");
                    sb.Append(i + 1);
                    sb.Append(" key=s").Append(scenario).Append("/").Append(h.MessageId);
                    sb.Append(" msg=").Append(h.MessageId);
                    sb.Append(" examined=").Append(h.IsExamined ? "1" : "0");
                    sb.Append(" item=").Append(h.ItemId);
                    if (!string.IsNullOrEmpty(h.ItemName))
                        sb.Append(" name=\"").Append(h.ItemName).Append("\"");
                    sb.Append("\n");
                }
            }
            catch (Exception ex)
            {
                sb.Append("hotspots=FEHLER(").Append(ex.Message).Append(")\n");
            }
        }

        /// <summary>
        /// Zaehlt die bereits vorhandenen Bug-Bloecke, um die naechste laufende
        /// Nummer zu bestimmen. Ueber die Marker "=== BUG " am Zeilenanfang.
        /// </summary>
        private static int CountExistingReports(string reportFile)
        {
            try
            {
                if (!File.Exists(reportFile))
                    return 0;

                int count = 0;
                foreach (string line in File.ReadAllLines(reportFile))
                {
                    if (line.StartsWith("=== BUG "))
                        count++;
                }
                return count;
            }
            catch
            {
                // Im Zweifel bei 0 anfangen — eine doppelte Nummer ist harmlos,
                // ein Absturz beim Zaehlen waere schlimmer.
                return 0;
            }
        }

        /// <summary>
        /// Ermittelt einen lesbaren Namen fuer den aktiven Modus. Reihenfolge wie
        /// im InputManager (erster Treffer gewinnt), damit der Bericht denselben
        /// Modus nennt, den auch die Tastensteuerung gerade bedient.
        /// </summary>
        private static string CurrentModeName()
        {
            try { if (AccessibilityState.IsIn3DEvidenceMode()) return "3D-Beweis"; } catch { }
            try { if (AccessibilityState.IsInLuminolMode()) return "Luminol"; } catch { }
            try { if (AccessibilityState.IsInVasePuzzleMode()) return "Vasen-Raetsel"; } catch { }
            try { if (AccessibilityState.IsInFingerprintMode()) return "Fingerabdruck"; } catch { }
            try { if (AccessibilityState.IsInVideoTapeMode()) return "Videoband"; } catch { }
            try { if (AccessibilityState.IsInOrchestraMode()) return "Orchester"; } catch { }
            try { if (AccessibilityState.IsInDyingMessageMode()) return "Todesnachricht"; } catch { }
            try { if (AccessibilityState.IsInBugSweeperMode()) return "Wanzensucher"; } catch { }
            try { if (AccessibilityState.IsInVaseShowMode()) return "Vasen-Drehung"; } catch { }
            try { if (AccessibilityState.IsInCourtRecordMode()) return "Gerichtsakte"; } catch { }
            try { if (AccessibilityState.IsInPointingMode()) return "Zeigen"; } catch { }
            try { if (AccessibilityState.IsInInvestigationMode()) return "Ermittlung"; } catch { }
            try { if (AccessibilityState.IsInTrialMode()) return "Verhandlung"; } catch { }
            return "Dialog/unbekannt";
        }

        private static string GameName()
        {
            try
            {
                int t = (int)GSStatic.global_work_.title;
                if (t == 0) return "GS1";
                if (t == 1) return "GS2";
                if (t == 2) return "GS3";
                return "GS?" + t;
            }
            catch
            {
                return "?";
            }
        }

        private static int SafeScenario()
        {
            try
            {
                return GSStatic.global_work_.scenario;
            }
            catch
            {
                return -1;
            }
        }
    }
}
