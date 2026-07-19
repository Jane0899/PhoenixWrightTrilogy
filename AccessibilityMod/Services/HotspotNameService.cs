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

            string key = bgNo + "/" + messageId;
            string name;
            if (table.TryGetValue(key, out name) && !Net35Extensions.IsNullOrWhiteSpace(name))
                return name;

            return null;
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
