using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityAccessibilityLib;

namespace AccessibilityMod.Services
{
    /// <summary>
    /// Liefert sprechende Namen fuer Untersuchungspunkte.
    ///
    /// Warum ueberhaupt eine Datei?
    /// Die Spieldaten kennen fuer Hotspots keine Namen — ein Punkt besteht nur
    /// aus Nachrichten-ID, Ort, Beweisbezug und Koordinaten (siehe todos.md J4).
    /// Sehende erkennen am Bild, was dort liegt; ohne Bild bleibt sonst nur
    /// "Punkt 3". Die Namen hier stammen aus dem, was das Spiel beim Untersuchen
    /// selbst sagt ("Ein einfaches Bett." -> "Bett"), sind also spielbegriffstreu
    /// und nicht erfunden. Erzeugt werden sie mit
    /// AccessibilityMod/DevBridge/dump-hotspot-texts.ps1.
    ///
    /// Dateiformat (flaches JSON, wie die Charakternamen):
    ///     { "32/242": "Gemaelde", "32/241": "Bett" }
    /// Schluessel ist "&lt;Hintergrundnummer&gt;/&lt;Nachrichten-ID&gt;". Die Hintergrund-
    /// nummer muss mit hinein, weil Nachrichten-IDs nur innerhalb einer
    /// Szenariodatei eindeutig sind und sich zwischen Episoden wiederholen.
    ///
    /// Ablage: UserData/AccessibilityMod/&lt;sprache&gt;/GS1_Hotspots.json
    /// (mit Rueckfall auf en/), neu ladbar per F5 wie die uebrigen Konfigdateien.
    /// </summary>
    public static class HotspotNameService
    {
        private static readonly Dictionary<string, string> _gs1 =
            new Dictionary<string, string>();
        private static readonly Dictionary<string, string> _gs2 =
            new Dictionary<string, string>();
        private static readonly Dictionary<string, string> _gs3 =
            new Dictionary<string, string>();

        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized)
                return;
            LoadFromFiles();
            _initialized = true;
        }

        public static void ReloadFromFiles()
        {
            LoadFromFiles();
            AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                "HotspotNameService reloaded from files"
            );
        }

        /// <summary>
        /// Sucht den Namen eines Punktes. Gibt null zurueck, wenn keiner
        /// hinterlegt ist — der Aufrufer faellt dann auf die bisherige
        /// Nummern-Ansage zurueck.
        /// </summary>
        public static string GetName(int bgNo, uint messageId)
        {
            Initialize();

            Dictionary<string, string> table = GetTableForCurrentGame();
            if (table == null || table.Count == 0)
                return null;

            string name;

            // Genauester Schluessel zuerst: <Szenario>/<Hintergrund>/<Nachricht>.
            // Die Szenario-Nummer MUSS mit hinein, weil verschiedene Episoden
            // im selben Raum dieselben Nachrichten-IDs mit anderem Inhalt
            // verwenden — in der Kanzlei ist 134 in Episode 3 ein altes
            // Filmplakat, in Episode 4 ein Steel-Samurai-Poster.
            int scenario = GetCurrentScenario();
            if (scenario >= 0)
            {
                string full = scenario + "/" + bgNo + "/" + messageId;
                if (table.TryGetValue(full, out name) && !Net35Extensions.IsNullOrWhiteSpace(name))
                    return name;
            }

            // Neues, OFFLINE erzeugbares Format: <Szenario>/<Nachricht>.
            // Warum noetig: Die Hintergrundnummer (bgNo) ist nur zur Laufzeit aus
            // bgCtrl bekannt und laesst sich aus den statischen Spieldaten NICHT
            // ableiten. Das Paar (Szenario, Nachricht) dagegen schon: Das Spiel
            // nutzt global_work_.scenario direkt als Index der Szenario-mdt
            // (advCtrl.cs: GSScenario.GetScenarioMdtPath(scenario)), und innerhalb
            // einer Szenariodatei ist die Nachrichten-ID eindeutig. Damit lassen
            // sich alle Untersuchungstexte offline auslesen und benennen, ohne
            // jede Szene im Spiel anzusteuern. Steht VOR dem alten bg-Rueckfall,
            // weil es genauer ist (Szenario statt nur Hintergrund).
            // "s"-Praefix, damit dieses zweiteilige Format NICHT mit dem alten
            // zweiteiligen <bg>/<message> kollidiert (beide waeren sonst z. B.
            // "5/194" — einmal Szenario 5, einmal Hintergrund 5). Mit "s5/194"
            // ist die Bedeutung eindeutig.
            if (scenario >= 0)
            {
                string sceneMsg = "s" + scenario + "/" + messageId;
                if (table.TryGetValue(sceneMsg, out name) && !Net35Extensions.IsNullOrWhiteSpace(name))
                    return name;
            }

            // Rueckfall auf das aeltere Format ohne Szenario. Damit bleiben
            // frueher gepflegte Eintraege gueltig; sie sind ungenauer, aber in
            // aller Regel richtig (ein Bett bleibt ein Bett).
            string shortKey = bgNo + "/" + messageId;
            if (table.TryGetValue(shortKey, out name) && !Net35Extensions.IsNullOrWhiteSpace(name))
                return name;

            return null;
        }

        /// <summary>
        /// Aktuelle Szenario-Nummer (entspricht dem Kapitel/der Episodenhaelfte).
        /// -1, wenn sie nicht lesbar ist.
        /// </summary>
        private static int GetCurrentScenario()
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

        private static Dictionary<string, string> GetTableForCurrentGame()
        {
            try
            {
                int title = (int)GSStatic.global_work_.title;
                if (title == 0)
                    return _gs1;
                if (title == 1)
                    return _gs2;
                if (title == 2)
                    return _gs3;
            }
            catch { }
            return _gs1;
        }

        private static void LoadFromFiles()
        {
            _gs1.Clear();
            _gs2.Clear();
            _gs3.Clear();

            try
            {
                // Gleiche Ablage wie die uebrigen Konfigdateien:
                // <Spielordner>/UserData/AccessibilityMod/<sprache>/
                string baseFolder = Path.Combine(
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData"),
                    "AccessibilityMod"
                );
                string langFolder = Path.Combine(
                    baseFolder,
                    LocalizationService.GetLanguageFolderName()
                );
                string enFolder = Path.Combine(baseFolder, "en");

                LoadWithFallback("GS1_Hotspots.json", langFolder, enFolder, _gs1);
                LoadWithFallback("GS2_Hotspots.json", langFolder, enFolder, _gs2);
                LoadWithFallback("GS3_Hotspots.json", langFolder, enFolder, _gs3);

                int total = _gs1.Count + _gs2.Count + _gs3.Count;
                if (total > 0)
                {
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                        $"Loaded {total} hotspot names from config files"
                    );
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Warning(
                    $"Error loading hotspot names: {ex.Message}"
                );
            }
        }

        private static void LoadWithFallback(
            string fileName,
            string primaryFolder,
            string fallbackFolder,
            Dictionary<string, string> target
        )
        {
            // Sprachordner zuerst, sonst Englisch. Anders als bei Texten ist der
            // Rueckfall hier selten hilfreich (deutsche Namen stehen nicht im
            // englischen Ordner), schadet aber nicht.
            string path = Path.Combine(primaryFolder, fileName);
            if (!File.Exists(path))
                path = Path.Combine(fallbackFolder, fileName);
            if (!File.Exists(path))
                return;

            try
            {
                ParseFlatJson(File.ReadAllText(path, Encoding.UTF8), target);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Warning(
                    $"Error reading {fileName}: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Minimalparser fuer flaches JSON der Form {"a": "b", ...}.
        ///
        /// Eigenbau, weil .NET 3.5 keinen JSON-Parser mitbringt und die Mod
        /// bewusst ohne Zusatzbibliotheken auskommt. Er versteht genau so viel,
        /// wie das Format braucht: Zeichenketten-Paare, doppelte Anfuehrungs-
        /// zeichen, Escape mit Backslash. Zeilen, die mit "_" beginnen, gelten
        /// als Kommentar und werden uebersprungen.
        /// </summary>
        private static void ParseFlatJson(string json, Dictionary<string, string> target)
        {
            if (string.IsNullOrEmpty(json))
                return;

            int i = 0;
            while (i < json.Length)
            {
                // naechste Zeichenkette = Schluessel
                string key = ReadNextString(json, ref i);
                if (key == null)
                    return;

                // Doppelpunkt ueberspringen
                while (i < json.Length && json[i] != ':')
                {
                    // Ein '}' vor dem Doppelpunkt heisst: kein Wert mehr da.
                    if (json[i] == '}')
                        return;
                    i++;
                }
                i++;

                string value = ReadNextString(json, ref i);
                if (value == null)
                    return;

                if (!key.StartsWith("_"))
                    target[key] = value;
            }
        }

        private static string ReadNextString(string s, ref int i)
        {
            while (i < s.Length && s[i] != '"')
                i++;
            if (i >= s.Length)
                return null;

            i++; // oeffnendes Anfuehrungszeichen
            StringBuilder sb = new StringBuilder();

            while (i < s.Length && s[i] != '"')
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    i++;
                    char c = s[i];
                    if (c == 'n')
                        sb.Append('\n');
                    else if (c == 't')
                        sb.Append('\t');
                    else if (c == 'u' && i + 4 < s.Length)
                    {
                        // \uXXXX — kommt bei Umlauten aus manchen Editoren vor
                        string hex = s.Substring(i + 1, 4);
                        int code;
                        if (
                            int.TryParse(
                                hex,
                                System.Globalization.NumberStyles.HexNumber,
                                null,
                                out code
                            )
                        )
                        {
                            sb.Append((char)code);
                            i += 4;
                        }
                    }
                    else
                        sb.Append(c);
                }
                else
                {
                    sb.Append(s[i]);
                }
                i++;
            }

            i++; // schliessendes Anfuehrungszeichen
            return sb.ToString();
        }
    }
}
