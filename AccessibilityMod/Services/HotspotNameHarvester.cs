using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using AccessibilityMod.Patches;

namespace AccessibilityMod.Services
{
    /// <summary>
    /// Sammelt beim normalen Spielen die GROUND-TRUTH-Namen von Untersuchungspunkten.
    ///
    /// Warum ueberhaupt?
    /// Fuer GS1 liessen sich die Namen offline aus den Nachrichtendateien ableiten,
    /// weil dort das Laufzeit-Szenario (global_work_.scenario) exakt dem mdt-Index
    /// entspricht. Fuer GS2/GS3 gilt diese Gleichung NICHT gesichert, und der
    /// Debug-Nachrichtenleser des Spiels liefert fuer diese Titel keine
    /// vertrauenswuerdige Szenario->Datei-Zuordnung (nachgewiesen 26.07.2026, siehe
    /// todos.md). Offline benannte Schluessel koennten also am falschen Szenario
    /// haengen -> falsche Namen. Deshalb der umgekehrte, immer korrekte Weg:
    ///
    /// Wenn Jana beim Spielen einen Punkt untersucht, zeigt das Spiel den echten
    /// Untersuchungstext, und global_work_.scenario ist das echte Laufzeit-Szenario.
    /// Genau dieses Paar schreiben wir mit:
    ///     GS&lt;n&gt; s&lt;scenario&gt;/&lt;message&gt; | bg=&lt;bg&gt; | &lt;exakter Text&gt;
    /// Das ist korrekt PER KONSTRUKTION und titel-unabhaengig — es ist derselbe
    /// Schluessel, den HotspotNameService.GetName spaeter nachschlaegt
    /// (s&lt;scenario&gt;/&lt;message&gt; bzw. &lt;scenario&gt;/&lt;bg&gt;/&lt;message&gt;).
    ///
    /// WICHTIG (Janas Regel): Der Harvester BENENNT nicht selbst. Er sammelt nur die
    /// verbatim Texte in eine Log-Datei. Das Ableiten/Zusammenfuehren in
    /// GS&lt;n&gt;_Hotspots.json bleibt Handarbeit mit Gegenpruefung.
    ///
    /// Ablage: UserData/AccessibilityMod/HarvestedNames/harvest.log (angehaengt).
    /// </summary>
    public static class HotspotNameHarvester
    {
        // Schon protokollierte Schluessel (Titel|scenario|message), damit dieselbe
        // Zeile nicht jeden Frame und nicht ueber Sitzungen hinweg doppelt landet.
        private static readonly HashSet<string> _seen = new HashSet<string>();
        private static bool _loaded;

        // Der zuletzt protokollierte Text: nur bei WECHSEL pruefen wir neu, sonst
        // wuerde jeder Frame denselben stehenden Text erneut auswerten.
        private static string _lastHarvestedText = "";

        // Untersuchungstexte erscheinen oft, waehrend der Ermittlungsmodus KURZ
        // aussetzt (das Dialogfenster laeuft). Deshalb ernten wir auch noch ein
        // paar Frames NACH dem letzten aktiven Ermittlungsmodus. ~1,5 s bei 60 fps.
        private const int GraceFrames = 90;
        private static int _framesSinceInvestigation = int.MaxValue;

        private static string _harvestFile;

        public static void Update()
        {
            try
            {
                EnsureLoaded();

                // Naehe zum Ermittlungsmodus verfolgen: 0 waehrend aktiv, danach
                // hochzaehlen. So wissen wir, ob ein gerade erscheinender Text noch
                // zu einer Untersuchung gehoert.
                if (AccessibilityState.IsInInvestigationMode())
                    _framesSinceInvestigation = 0;
                else if (_framesSinceInvestigation < int.MaxValue)
                    _framesSinceInvestigation++;

                // Ausserhalb des Erntefensters gar nicht erst weiter pruefen.
                if (_framesSinceInvestigation > GraceFrames)
                    return;

                string text = DialoguePatches._lastAnnouncedText;
                if (string.IsNullOrEmpty(text) || text == _lastHarvestedText)
                    return;

                _lastHarvestedText = text;

                // Aktuell gewaehlter Punkt. Jana navigiert mit den Mod-Tasten
                // ([ ] / Komma-Punkt), die den Spiel-Cursor mitziehen — der
                // aktuelle Punkt ist damit der gerade untersuchte.
                int idx = HotspotNavigator.GetCurrentIndex();
                var list = HotspotNavigator.GetHotspots();
                if (list == null || idx < 0 || idx >= list.Count)
                    return;

                uint message = list[idx].MessageId;

                int scenario = SafeScenario();
                if (scenario < 0)
                    return;

                string game = GameName();
                string dedupKey = game + "|" + scenario + "|" + message;
                if (_seen.Contains(dedupKey))
                    return;
                _seen.Add(dedupKey);

                int bg = SafeBgNo();

                // Eine Zeile im Log: Schluessel + bg (fuer den genaueren 3er-Key) +
                // verbatim Text. Zeilenumbrueche im Text neutralisieren, damit eine
                // Ernte = eine Zeile bleibt und spaeter zeilenweise parsebar ist.
                string safeText = text.Replace("\r", " ").Replace("\n", " ").Trim();
                string line = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} s{1}/{2} | bg={3} | {4}",
                    game,
                    scenario,
                    message,
                    bg,
                    safeText
                );

                File.AppendAllText(_harvestFile, line + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                // Ernten darf das Spielerlebnis nie stoeren — Fehler nur loggen.
                AccessibilityMod.Core.AccessibilityMod.Logger?.Warning(
                    "HotspotNameHarvester: " + ex.Message
                );
            }
        }

        /// <summary>
        /// Beim ersten Aufruf die schon geernteten Schluessel einlesen, damit ein
        /// Neustart nicht alles erneut anhaengt. Robust gegen fehlende Datei.
        /// </summary>
        private static void EnsureLoaded()
        {
            if (_loaded)
                return;
            _loaded = true;

            string dir = Path.Combine(
                Environment.CurrentDirectory,
                Path.Combine("UserData", Path.Combine("AccessibilityMod", "HarvestedNames"))
            );
            Directory.CreateDirectory(dir);
            _harvestFile = Path.Combine(dir, "harvest.log");

            try
            {
                if (File.Exists(_harvestFile))
                {
                    foreach (string ln in File.ReadAllLines(_harvestFile))
                    {
                        // Format: "GS2 s5/194 | bg=25 | text"
                        // dedupKey rekonstruieren aus "GS<n> s<scenario>/<message>"
                        int sp1 = ln.IndexOf(' ');
                        if (sp1 <= 0)
                            continue;
                        string g = ln.Substring(0, sp1);
                        int sPos = ln.IndexOf(" s", sp1 - 1);
                        int slash = ln.IndexOf('/', sp1);
                        int bar = ln.IndexOf(" |", sp1);
                        if (sPos < 0 || slash < 0 || bar < 0 || slash <= sPos || bar <= slash)
                            continue;
                        string scen = ln.Substring(sPos + 2, slash - (sPos + 2));
                        string msg = ln.Substring(slash + 1, bar - (slash + 1));
                        _seen.Add(g + "|" + scen + "|" + msg);
                    }
                }
            }
            catch { }
        }

        private static int SafeScenario()
        {
            try { return GSStatic.global_work_.scenario; }
            catch { return -1; }
        }

        private static int SafeBgNo()
        {
            try { return bgCtrl.instance != null ? bgCtrl.instance.bg_no : -1; }
            catch { return -1; }
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
            catch { return "GS?"; }
        }
    }
}
