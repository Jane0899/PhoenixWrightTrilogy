using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using AccessibilityMod.Services;
using UnityEngine;

namespace AccessibilityMod.DevBridge
{
    /// <summary>
    /// Die Befehle der DevBridge. Jede Methode hier laeuft im Unity-Hauptthread
    /// (siehe DevBridgeServer.PumpMainThread) und darf deshalb gefahrlos auf
    /// Spielobjekte zugreifen.
    ///
    /// Antworten sind bewusst schlichter Text statt JSON: Sie werden von einem
    /// Menschen bzw. einem Sprachmodell gelesen, nicht von einem Parser, und
    /// Text bleibt auch dann verstaendlich, wenn ein Feld mal fehlt.
    /// </summary>
    public static class DevBridgeCommands
    {
        public static string Execute(string line)
        {
            // Befehl und Rest trennen. Der Rest bleibt ungeteilt, damit
            // Argumente mit Leerzeichen (Pfade, Texte) heil ankommen.
            string cmd = line;
            string arg = string.Empty;
            int sp = line.IndexOf(' ');
            if (sp > 0)
            {
                cmd = line.Substring(0, sp);
                arg = line.Substring(sp + 1).Trim();
            }

            switch (cmd.ToLowerInvariant())
            {
                case "ping":
                    return "pong";
                case "help":
                    return Help();
                case "state":
                    return State();
                case "hotspots":
                    return Hotspots();
                case "hotspot":
                    return GotoHotspot(arg);
                case "dump":
                    return DumpHotspotImages(arg);
                case "shot":
                    return Screenshot(arg);
                case "say":
                    return Say(arg);
                case "mdtpath":
                    return MdtPath(arg);
                case "mes":
                    return MessageText(arg);
                case "scenarios":
                    return Scenarios(arg);
                case "mesraw":
                    return MessageRaw(arg);
                case "mesfile":
                    return MessageFromFile(arg);
                case "loadbg":
                    return LoadBackground(arg);
                case "crop":
                    return CropCurrentBackground(arg);
                case "fast":
                    return FastForward(arg);
                default:
                    return "ERROR unbekannter Befehl: " + cmd + " (help zeigt alle)";
            }
        }

        private static string Help()
        {
            return string.Join(
                "\n",
                new string[]
                {
                    "ping                 - Lebenszeichen",
                    "state                - Spiel, Modus, Szene, aktueller Dialog",
                    "hotspots             - Untersuchungspunkte der aktuellen Szene auflisten",
                    "hotspot <n>          - Cursor auf Punkt n setzen (1-basiert)",
                    "dump [ordner]        - Bildausschnitt je Hotspot als PNG speichern",
                    "shot [datei]         - Bildschirmfoto speichern",
                    "say <text>           - Text ueber den Screenreader ausgeben",
                    "mdtpath <t> <s>      - Nachrichtendatei eines Szenarios anzeigen",
                    "mes <t> <s> <nr> [p] - Text einer Nachricht (Untersuchungstext)",
                }
            );
        }

        /// <summary>
        /// Gesamtzustand in einer kompakten Uebersicht: Welches Spiel laeuft, in
        /// welchem Modus sind wir, welcher Hintergrund ist geladen, was steht
        /// gerade im Dialogfenster.
        /// </summary>
        private static string State()
        {
            StringBuilder sb = new StringBuilder();

            try
            {
                sb.Append("game=");
                sb.Append(GameName());
                sb.Append(" scenario=");
                sb.Append(SafeScenario());
                sb.Append("\n");
            }
            catch { }

            try
            {
                if (bgCtrl.instance != null)
                {
                    sb.Append("bg_no=");
                    sb.Append(bgCtrl.instance.bg_no);
                    sb.Append(" bg_pos_x=");
                    sb.Append(
                        bgCtrl.instance.bg_pos_x.ToString("0.#", CultureInfo.InvariantCulture)
                    );
                    sb.Append("\n");
                }
            }
            catch { }

            try
            {
                sb.Append("investigation=");
                sb.Append(AccessibilityState.IsInInvestigationMode() ? "yes" : "no");
                sb.Append("\n");
            }
            catch { }

            try
            {
                sb.Append("hotspots=");
                sb.Append(HotspotNavigator.GetHotspotCount());
                sb.Append("\n");
            }
            catch { }

            if (sb.Length == 0)
                sb.Append("(kein Zustand lesbar)");

            return sb.ToString().TrimEnd('\n');
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

        /// <summary>
        /// Listet die Untersuchungspunkte der aktuellen Szene mit Mittelpunkt und
        /// Untersucht-Status. Grundlage fuer alles Weitere: Ohne diese Liste
        /// wuesste der Client nicht, wieviele Punkte es zu benennen gibt.
        /// </summary>
        private static string Hotspots()
        {
            try
            {
                HotspotNavigator.RefreshHotspots();
                List<HotspotNavigator.HotspotInfo> list = HotspotNavigator.GetHotspots();

                if (list == null || list.Count == 0)
                    return "keine Hotspots in dieser Szene";

                StringBuilder sb = new StringBuilder();
                sb.Append("game=").Append(GameName());
                sb.Append(" scenario=").Append(SafeScenario());
                sb.Append(" bg_no=").Append(SafeBgNo());
                sb.Append(" count=").Append(list.Count).Append("\n");

                for (int i = 0; i < list.Count; i++)
                {
                    HotspotNavigator.HotspotInfo h = list[i];
                    sb.Append(i + 1);
                    sb.Append(" msg=").Append(h.MessageId);
                    sb.Append(" idx=").Append(h.DataIndex);
                    sb.Append(" x=").Append((int)h.CenterX);
                    sb.Append(" y=").Append((int)h.CenterY);
                    sb.Append(" examined=").Append(h.IsExamined ? "1" : "0");
                    // Beweisbezug mit ausgeben: So ist von aussen sofort sichtbar,
                    // welche Punkte ueber Loesung 1 schon einen echten Spielnamen
                    // haben und welche noch von Hand benannt werden muessen.
                    sb.Append(" item=").Append(h.ItemId);
                    if (!string.IsNullOrEmpty(h.ItemName))
                        sb.Append(" name=\"").Append(h.ItemName).Append("\"");
                    sb.Append("\n");
                }

                return sb.ToString().TrimEnd('\n');
            }
            catch (Exception ex)
            {
                return "ERROR hotspots: " + ex.Message;
            }
        }

        /// <summary>
        /// Aktuelle Szenario-Nummer. Gehoert zwingend in jede Hotspot-Ausgabe:
        /// Nachrichten-IDs wiederholen sich zwischen Episoden mit voellig
        /// anderem Inhalt, erst zusammen mit dem Szenario ist ein Punkt
        /// eindeutig bestimmt.
        /// </summary>
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

        private static int SafeBgNo()
        {
            try
            {
                return bgCtrl.instance != null ? bgCtrl.instance.bg_no : -1;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// Setzt den Untersuchungs-Cursor auf einen Punkt. Nutzt bewusst den
        /// vorhandenen Navigator statt eigener Cursorlogik, damit die Bridge
        /// genau das tut, was auch die Tastatursteuerung tut.
        /// </summary>
        private static string GotoHotspot(string arg)
        {
            int n;
            if (!int.TryParse(arg, out n) || n < 1)
                return "ERROR bitte eine Punktnummer ab 1 angeben";

            try
            {
                if (!HotspotNavigator.NavigateToIndex(n - 1))
                    return "ERROR Punkt " + n + " existiert nicht";

                return "ok Punkt " + n;
            }
            catch (Exception ex)
            {
                return "ERROR hotspot: " + ex.Message;
            }
        }

        /// <summary>
        /// Kernstueck fuer die Benennung: schneidet fuer jeden Hotspot den
        /// zugehoerigen Bildausschnitt aus der Hintergrundtextur und legt ihn als
        /// PNG ab, dazu eine index.txt mit den Metadaten.
        ///
        /// Warum aus der Textur statt per Bildschirmfoto?
        /// Die Textur liegt ohnehin im Speicher, ist unabhaengig von Fenstergroesse
        /// und Fokus, und enthaelt auch die Bildteile, die gerade nicht sichtbar
        /// sind (breite Szenen zum Schwenken). Ausserdem muss dafuer das
        /// Spielfenster nicht in den Vordergrund — es blockiert Janas Rechner also
        /// deutlich weniger.
        /// </summary>
        private static string DumpHotspotImages(string arg)
        {
            try
            {
                if (bgCtrl.instance == null || bgCtrl.instance.sprite_data == null)
                    return "ERROR kein Hintergrund geladen";

                Sprite sprite = bgCtrl.instance.sprite_data;
                Texture2D source = sprite.texture;
                if (source == null)
                    return "ERROR Hintergrund hat keine Textur";

                HotspotNavigator.RefreshHotspots();
                List<HotspotNavigator.HotspotInfo> list = HotspotNavigator.GetHotspots();
                if (list == null || list.Count == 0)
                    return "keine Hotspots zum Speichern";

                string dir = string.IsNullOrEmpty(arg)
                    ? Path.Combine(
                        Environment.CurrentDirectory,
                        Path.Combine("UserData", Path.Combine("AccessibilityMod", "HotspotDump"))
                    )
                    : arg;

                string sceneDir = Path.Combine(dir, GameName() + "_bg" + SafeBgNo());
                Directory.CreateDirectory(sceneDir);

                // Die Textur ist in aller Regel nicht CPU-lesbar (isReadable=false).
                // Deshalb ueber eine RenderTexture umkopieren — das funktioniert
                // unabhaengig von den Importeinstellungen des Assets.
                Texture2D readable = MakeReadable(source);

                StringBuilder index = new StringBuilder();
                index.Append("game=").Append(GameName());
                index.Append(" bg_no=").Append(SafeBgNo());
                index.Append(" texture=").Append(readable.width).Append("x").Append(readable.height);
                index.Append(" count=").Append(list.Count).Append("\n");

                int saved = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    HotspotNavigator.HotspotInfo h = list[i];
                    try
                    {
                        string file = Path.Combine(sceneDir, "hotspot_" + (i + 1) + ".png");
                        SaveCrop(readable, h, file);
                        saved++;

                        index.Append(i + 1);
                        index.Append(" msg=").Append(h.MessageId);
                        index.Append(" x=").Append((int)h.CenterX);
                        index.Append(" y=").Append((int)h.CenterY);
                        index.Append(" file=").Append(Path.GetFileName(file));
                        index.Append("\n");
                    }
                    catch (Exception exOne)
                    {
                        index.Append(i + 1).Append(" FEHLER ").Append(exOne.Message).Append("\n");
                    }
                }

                File.WriteAllText(Path.Combine(sceneDir, "index.txt"), index.ToString());
                UnityEngine.Object.Destroy(readable);

                return "ok " + saved + " Ausschnitte gespeichert in " + sceneDir;
            }
            catch (Exception ex)
            {
                return "ERROR dump: " + ex.Message;
            }
        }

        /// <summary>
        /// Kopiert eine beliebige Textur in eine CPU-lesbare Kopie. Der Umweg ueber
        /// die GPU (Blit in eine RenderTexture, dann ReadPixels) ist noetig, weil
        /// Spiel-Texturen praktisch nie mit "Read/Write enabled" importiert werden
        /// und GetPixels sonst eine Ausnahme wirft.
        /// </summary>
        private static Texture2D MakeReadable(Texture2D source)
        {
            RenderTexture rt = RenderTexture.GetTemporary(
                source.width,
                source.height,
                0,
                RenderTextureFormat.Default,
                RenderTextureReadWrite.Linear
            );

            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;

                Texture2D copy = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
                copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                copy.Apply();
                return copy;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        /// <summary>
        /// Schneidet den Bereich um einen Hotspot aus und speichert ihn als PNG.
        ///
        /// Koordinaten: Das Spiel rechnet in einem 1920x1080-Raster mit Ursprung
        /// oben links, Unity-Texturen haben ihren Ursprung unten links. Deshalb
        /// wird die Y-Achse gespiegelt. Zusaetzlich wird grosszuegig Rand
        /// mitgenommen — ein Hotspot-Rechteck allein zeigt oft nur einen Teil des
        /// Gegenstands, und zum Benennen hilft der Zusammenhang.
        /// </summary>
        private static void SaveCrop(Texture2D tex, HotspotNavigator.HotspotInfo h, string file)
        {
            const float GameWidth = 1920f;
            const float GameHeight = 1080f;
            const int Padding = 90; // Rand in Spielkoordinaten

            float scaleX = tex.width / GameWidth;
            float scaleY = tex.height / GameHeight;

            int cx = Mathf.RoundToInt(h.CenterX * scaleX);
            int cyTop = Mathf.RoundToInt(h.CenterY * scaleY);
            int cy = tex.height - cyTop; // Y spiegeln

            int half = Mathf.RoundToInt(Padding * scaleX);

            int x0 = Mathf.Clamp(cx - half, 0, tex.width - 1);
            int y0 = Mathf.Clamp(cy - half, 0, tex.height - 1);
            int w = Mathf.Clamp(half * 2, 1, tex.width - x0);
            int hgt = Mathf.Clamp(half * 2, 1, tex.height - y0);

            Color[] pixels = tex.GetPixels(x0, y0, w, hgt);

            Texture2D crop = new Texture2D(w, hgt, TextureFormat.RGB24, false);
            crop.SetPixels(pixels);
            crop.Apply();

            File.WriteAllBytes(file, crop.EncodeToPNG());
            UnityEngine.Object.Destroy(crop);
        }

        /// <summary>
        /// Sucht die Textur des aktuell angezeigten Hintergrunds.
        ///
        /// Warum mehrere Quellen? Das Feld sprite_data ist nur ein Zwischenspeicher
        /// und bleibt bei direkt per SetSprite geladenen Hintergruenden leer
        /// (live festgestellt am 19.07.2026). Das tatsaechlich angezeigte Bild
        /// haengt am SpriteRenderer. Deshalb werden die Quellen der Reihe nach
        /// abgeklopft, statt sich auf eine zu verlassen.
        /// </summary>
        private static Texture2D GetBackgroundTexture()
        {
            if (bgCtrl.instance == null)
                return null;

            // 1) Der offizielle Zwischenspeicher — wenn gefuellt, der einfachste Weg.
            try
            {
                if (bgCtrl.instance.sprite_data != null && bgCtrl.instance.sprite_data.texture != null)
                    return bgCtrl.instance.sprite_data.texture;
            }
            catch { }

            // 2) Die Renderer, die das Bild wirklich darstellen.
            string[] rendererFields = new string[] { "sprite_renderer_", "image_", "sub_sprite_" };
            foreach (string name in rendererFields)
            {
                try
                {
                    var f = typeof(bgCtrl).GetField(
                        name,
                        System.Reflection.BindingFlags.NonPublic
                            | System.Reflection.BindingFlags.Public
                            | System.Reflection.BindingFlags.Instance
                    );
                    if (f == null)
                        continue;

                    var sr = f.GetValue(bgCtrl.instance) as SpriteRenderer;
                    if (sr != null && sr.sprite != null && sr.sprite.texture != null)
                        return sr.sprite.texture;
                }
                catch { }
            }

            return null;
        }

        /// <summary>
        /// loadbg &lt;nummer&gt; — laedt einen beliebigen Hintergrund direkt.
        ///
        /// Der Schluessel zur Automatisierung: Damit lassen sich alle Schauplaetze
        /// der Reihe nach anzeigen, ohne die Geschichte zu spielen. Das Spiel
        /// bringt die Methode selbst mit (bgCtrl.SetSprite), sie wird hier nur
        /// von aussen ausloesbar gemacht.
        /// </summary>
        private static string LoadBackground(string arg)
        {
            int no;
            if (!int.TryParse(arg, out no) || no < 0)
                return "ERROR Aufruf: loadbg <nummer>";

            try
            {
                if (bgCtrl.instance == null)
                    return "ERROR bgCtrl noch nicht bereit (Spiel im Titelbildschirm?)";

                bgCtrl.instance.SetSprite(no, true);

                string name = "?";
                try
                {
                    name = bgCtrl.instance.GetBGName(no);
                }
                catch { }

                return "ok bg=" + no + " name=" + name;
            }
            catch (Exception ex)
            {
                return "ERROR loadbg: " + Unwrap(ex).Message;
            }
        }

        /// <summary>
        /// crop &lt;x&gt; &lt;y&gt; &lt;radius&gt; &lt;datei&gt; — schneidet einen Bereich des aktuell
        /// geladenen Hintergrunds aus und speichert ihn als PNG.
        ///
        /// Koordinaten sind Spielkoordinaten (1920x1080, Ursprung oben links),
        /// also genau das, was in den Hotspot-Tabellen steht. So laesst sich zu
        /// jedem Untersuchungspunkt das passende Bild erzeugen, ohne dass die
        /// Szene tatsaechlich im Spielverlauf erreicht werden muss.
        /// </summary>
        private static string CropCurrentBackground(string arg)
        {
            string[] parts = arg.Split(new char[] { ' ' }, 4);
            int cx, cy, radius;

            if (
                parts.Length < 4
                || !int.TryParse(parts[0], out cx)
                || !int.TryParse(parts[1], out cy)
                || !int.TryParse(parts[2], out radius)
            )
                return "ERROR Aufruf: crop <x> <y> <radius> <datei>";

            string file = parts[3];

            try
            {
                Texture2D source = GetBackgroundTexture();
                if (source == null)
                    return "ERROR kein Hintergrund geladen (keine Textur gefunden)";

                string dir = Path.GetDirectoryName(file);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                Texture2D readable = MakeReadable(source);
                try
                {
                    const float GameWidth = 1920f;
                    const float GameHeight = 1080f;

                    float scaleX = readable.width / GameWidth;
                    float scaleY = readable.height / GameHeight;

                    int px = Mathf.RoundToInt(cx * scaleX);
                    // Y spiegeln: Spiel rechnet von oben, Unity-Texturen von unten.
                    int py = readable.height - Mathf.RoundToInt(cy * scaleY);
                    int half = Mathf.RoundToInt(radius * scaleX);

                    int x0 = Mathf.Clamp(px - half, 0, readable.width - 1);
                    int y0 = Mathf.Clamp(py - half, 0, readable.height - 1);
                    int w = Mathf.Clamp(half * 2, 1, readable.width - x0);
                    int h = Mathf.Clamp(half * 2, 1, readable.height - y0);

                    Texture2D crop = new Texture2D(w, h, TextureFormat.RGB24, false);
                    crop.SetPixels(readable.GetPixels(x0, y0, w, h));
                    crop.Apply();
                    File.WriteAllBytes(file, crop.EncodeToPNG());
                    UnityEngine.Object.Destroy(crop);

                    return "ok " + w + "x" + h + " -> " + file;
                }
                finally
                {
                    UnityEngine.Object.Destroy(readable);
                }
            }
            catch (Exception ex)
            {
                return "ERROR crop: " + Unwrap(ex).Message;
            }
        }

        private static string Screenshot(string arg)
        {
            try
            {
                string file = string.IsNullOrEmpty(arg)
                    ? Path.Combine(
                        Environment.CurrentDirectory,
                        Path.Combine("UserData", Path.Combine("AccessibilityMod", "shot.png"))
                    )
                    : arg;

                string dir = Path.GetDirectoryName(file);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                ScreenCapture.CaptureScreenshot(file);
                // Unity schreibt die Datei erst am Ende des Frames — der Client
                // muss also kurz warten, bevor er sie liest.
                return "ok Bildschirmfoto folgt in " + file;
            }
            catch (Exception ex)
            {
                return "ERROR shot: " + ex.Message;
            }
        }

        // --- Zugriff auf den spieleigenen Debug-Nachrichtenleser -------------
        //
        // Das Spiel bringt eine interne Klasse DebugMdtViewer+otherLangMessage
        // mit, die genau das kann, was hier gebraucht wird: die Nachrichtendatei
        // eines Szenarios laden und den Text einer Nachricht liefern. Sie wird
        // per Reflection angesprochen, weil sie nicht Teil der oeffentlichen
        // Spiel-API ist — faellt sie in einer Spielversion weg, scheitert nur
        // dieser Befehl und nicht die ganze Bruecke.
        //
        // Warum das wichtig ist: Damit lassen sich die Untersuchungstexte ALLER
        // Szenen abrufen, ohne sie im Spiel anzusteuern. Das ersetzt das
        // urspruenglich geplante stundenlange Durchspielen.
        private static Type _viewerType;
        private static object _viewerInstance;

        private static object GetViewer()
        {
            if (_viewerInstance != null)
                return _viewerInstance;

            if (_viewerType == null)
            {
                // Verschachtelter Typ: der Laufzeitname nutzt '+' als Trenner.
                _viewerType = typeof(GSStatic).Assembly.GetType("DebugMdtViewer+otherLangMessage");
            }

            if (_viewerType == null)
                return null;

            // Der Konstruktor verlangt eine Sprache — ohne sie wuerde er die
            // Texte gar nicht aufloesen koennen. Es wird die aktuell im Spiel
            // eingestellte Sprache genommen, damit die Untersuchungstexte in
            // derselben Sprache herauskommen, die Jana auch hoert.
            object language = GSStatic.global_work_.language;
            _viewerInstance = Activator.CreateInstance(_viewerType, new object[] { language });
            return _viewerInstance;
        }

        /// <summary>
        /// scenarios &lt;title&gt; — listet die Nachrichtendateien aller Szenarien
        /// eines Spiels. Damit laesst sich zuordnen, welche Szenario-Nummer zu
        /// welchem Kapitel gehoert (noetig, um die Hotspot-Tabellen aus der
        /// scenario-Klasse mit den richtigen Texten zusammenzubringen).
        /// </summary>
        private static string Scenarios(string arg)
        {
            int title;
            if (!int.TryParse(arg, out title) || title < 0 || title > 2)
                return "ERROR Aufruf: scenarios <title 0-2>";

            try
            {
                object viewer = GetViewer();
                if (viewer == null)
                    return "ERROR DebugMdtViewer nicht verfuegbar";

                string fieldName = "GS" + (title + 1) + "_scenario_mdt_path_table";
                var field = _viewerType.GetField(
                    fieldName,
                    System.Reflection.BindingFlags.Public
                        | System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.Static
                );
                if (field == null)
                    return "ERROR Feld " + fieldName + " nicht gefunden";

                string[] paths = field.GetValue(viewer) as string[];
                if (paths == null)
                    return "ERROR Pfadtabelle ist leer";

                StringBuilder sb = new StringBuilder();
                sb.Append("count=").Append(paths.Length).Append("\n");
                for (int i = 0; i < paths.Length; i++)
                {
                    sb.Append(i).Append(" ").Append(paths[i]).Append("\n");
                }
                return sb.ToString().TrimEnd('\n');
            }
            catch (Exception ex)
            {
                return "ERROR scenarios: " + Unwrap(ex).Message;
            }
        }

        /// <summary>
        /// mdtpath &lt;title&gt; &lt;scenario&gt; — zeigt, welche Nachrichtendatei zu einer
        /// Szenario-Nummer gehoert. Dient zum Abgleich, welche Nummer welchem
        /// Kapitel entspricht.
        /// </summary>
        private static string MdtPath(string arg)
        {
            string[] parts = arg.Split(' ');
            int title, scenario;
            if (
                parts.Length < 2
                || !int.TryParse(parts[0], out title)
                || !int.TryParse(parts[1], out scenario)
            )
                return "ERROR Aufruf: mdtpath <title 0-2> <scenario>";

            try
            {
                object viewer = GetViewer();
                if (viewer == null)
                    return "ERROR DebugMdtViewer nicht verfuegbar";

                _viewerType
                    .GetMethod("LoadByTitle_Scenario")
                    .Invoke(viewer, new object[] { title, scenario });

                object path = _viewerType
                    .GetMethod("getMdtPath")
                    .Invoke(viewer, new object[] { scenario });

                return "ok " + (path == null ? "(null)" : path.ToString());
            }
            catch (Exception ex)
            {
                return "ERROR mdtpath: " + Unwrap(ex).Message;
            }
        }

        /// <summary>
        /// mes &lt;title&gt; &lt;scenario&gt; &lt;messageNo&gt; [seite] — liefert den Text einer
        /// Nachricht. Das ist der Untersuchungstext eines Hotspots und damit die
        /// Grundlage, um ihm einen sinnvollen Namen zu geben.
        /// </summary>
        private static string MessageText(string arg)
        {
            string[] parts = arg.Split(' ');
            int title, scenario, mesNo;
            int page = 0;

            if (
                parts.Length < 3
                || !int.TryParse(parts[0], out title)
                || !int.TryParse(parts[1], out scenario)
                || !int.TryParse(parts[2], out mesNo)
            )
                return "ERROR Aufruf: mes <title 0-2> <scenario> <messageNo> [seite]";

            if (parts.Length >= 4)
                int.TryParse(parts[3], out page);

            // Jeder Schritt wird einzeln berichtet. Beim ersten Anlauf schlug der
            // Abruf mit einer nichtssagenden Nullreferenz fehl — ohne
            // Zwischenmeldungen ist nicht erkennbar, ob schon das Laden scheitert
            // oder erst das Auslesen.
            StringBuilder diag = new StringBuilder();

            object viewer;
            try
            {
                viewer = GetViewer();
                if (viewer == null)
                    return "ERROR DebugMdtViewer nicht verfuegbar";
            }
            catch (Exception ex)
            {
                return "ERROR Viewer: " + Unwrap(ex).Message;
            }

            try
            {
                _viewerType
                    .GetMethod("LoadByTitle_Scenario")
                    .Invoke(viewer, new object[] { title, scenario });
                diag.Append("load=ok ");
            }
            catch (Exception ex)
            {
                diag.Append("load=FEHLER(").Append(Unwrap(ex).Message).Append(") ");
            }

            // Wurde die Datei wirklich geladen? Das Feld mdt verraet es.
            try
            {
                var mdtField = _viewerType.GetField(
                    "mdt",
                    System.Reflection.BindingFlags.Public
                        | System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );
                object mdt = mdtField != null ? mdtField.GetValue(viewer) : null;

                if (mdt == null)
                {
                    diag.Append("mdt=null ");
                }
                else
                {
                    diag.Append("mdt=ok ");
                    var cnt = mdt.GetType().GetMethod("get_message_count");
                    if (cnt != null)
                        diag.Append("count=").Append(cnt.Invoke(mdt, null)).Append(" ");
                }
            }
            catch (Exception ex)
            {
                diag.Append("mdt=FEHLER(").Append(Unwrap(ex).Message).Append(") ");
            }

            // Beide Textgetter versuchen — getPageMessage liefert den fertigen
            // Text, getPageString eine rohere Variante. Welcher funktioniert,
            // ist von aussen nicht ersichtlich, also einfach beide probieren.
            foreach (string getter in new string[] { "getPageMessage", "getPageString" })
            {
                try
                {
                    object text = _viewerType
                        .GetMethod(getter)
                        .Invoke(viewer, new object[] { scenario, (ushort)mesNo, page });

                    if (text != null && text.ToString().Length > 0)
                    {
                        return text.ToString().Replace("\r", " ").Replace("\n", " ")
                            + "   [" + diag.ToString().Trim() + " via " + getter + "]";
                    }

                    diag.Append(getter).Append("=leer ");
                }
                catch (Exception ex)
                {
                    diag.Append(getter).Append("=FEHLER(").Append(Unwrap(ex).Message).Append(") ");
                }
            }

            return "(kein Text) " + diag.ToString().Trim();
        }

        /// <summary>
        /// mesraw &lt;title&gt; &lt;scenario&gt; &lt;messageNo&gt; [anzahl] — gibt die Rohwerte
        /// einer Nachricht aus.
        ///
        /// Warum roh? Die fertigen Textgetter des Spiels (getPageMessage /
        /// getPageString) scheitern ausserhalb des normalen Spielablaufs mit
        /// einer Nullreferenz — sie brauchen Datenstrukturen, die erst waehrend
        /// einer echten Szene gefuellt werden. Die Nachrichtendatei selbst ist
        /// aber geladen und liefert ueber GetMessageOffset/GetMessage direkt die
        /// 16-Bit-Werte. Aus denen laesst sich der Text selbst zusammensetzen —
        /// dieser Befehl zeigt sie, damit die Kodierung bestimmt werden kann.
        /// </summary>
        private static string MessageRaw(string arg)
        {
            string[] parts = arg.Split(' ');
            int title, scenario, mesNo;
            int count = 60;

            if (
                parts.Length < 3
                || !int.TryParse(parts[0], out title)
                || !int.TryParse(parts[1], out scenario)
                || !int.TryParse(parts[2], out mesNo)
            )
                return "ERROR Aufruf: mesraw <title 0-2> <scenario> <messageNo> [anzahl]";

            if (parts.Length >= 4)
                int.TryParse(parts[3], out count);

            try
            {
                object viewer = GetViewer();
                if (viewer == null)
                    return "ERROR DebugMdtViewer nicht verfuegbar";

                _viewerType
                    .GetMethod("LoadByTitle_Scenario")
                    .Invoke(viewer, new object[] { title, scenario });

                var mdtField = _viewerType.GetField(
                    "mdt",
                    System.Reflection.BindingFlags.Public
                        | System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );
                object mdt = mdtField.GetValue(viewer);
                if (mdt == null)
                    return "ERROR mdt nicht geladen";

                Type mdtType = mdt.GetType();

                uint offset = (uint)
                    mdtType
                        .GetMethod("GetMessageOffset")
                        .Invoke(mdt, new object[] { (ushort)mesNo });

                var getMsg = mdtType.GetMethod("GetMessage");

                StringBuilder sb = new StringBuilder();
                sb.Append("offset=").Append(offset).Append("\n");

                // Zwei Sichten auf dieselben Werte: einmal als Zahl, einmal als
                // Zeichen (sofern druckbar). So ist auf einen Blick erkennbar,
                // ob es sich um Unicode handelt oder um eine eigene Tabelle.
                StringBuilder nums = new StringBuilder();
                StringBuilder chars = new StringBuilder();

                for (int i = 0; i < count; i++)
                {
                    ushort v = (ushort)getMsg.Invoke(mdt, new object[] { (uint)(offset + i) });
                    nums.Append(v).Append(" ");
                    chars.Append(v >= 32 && v < 0xFFF0 ? (char)v : '.');
                }

                sb.Append("werte: ").Append(nums.ToString().Trim()).Append("\n");
                sb.Append("zeichen: ").Append(chars.ToString());
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return "ERROR mesraw: " + Unwrap(ex).Message;
            }
        }

        // Einmal geladene Nachrichtendateien werden behalten: Beim stapelweisen
        // Auslesen hunderter Punkte wuerde sonst dieselbe Datei immer wieder neu
        // eingelesen und entpackt.
        private static readonly Dictionary<string, object> _mdtCache =
            new Dictionary<string, object>();

        /// <summary>
        /// mesfile &lt;datei&gt; &lt;messageNo&gt; [seiten] — laedt eine Nachrichtendatei
        /// direkt von der Platte und gibt den Text einer Nachricht zurueck.
        ///
        /// Warum nicht ueber den Debug-Viewer des Spiels?
        /// Der laedt nur die japanische Basisdatei (sc..._text.mdt) und seine
        /// fertigen Textgetter scheitern ausserhalb einer echten Szene. Die
        /// Sprachfassungen liegen aber als eigene Dateien daneben
        /// (_g deutsch, _u englisch, _f franzoesisch ...), und MdtData kann aus
        /// rohen Dateibytes gebaut werden. Damit ist jede Sprache und jedes
        /// Szenario frei zugaenglich — unabhaengig vom Spielfortschritt.
        ///
        /// &lt;datei&gt; ist relativ zu StreamingAssets, z. B.
        ///     GS1/scenario/sc1_2_text_g.mdt
        /// </summary>
        private static string MessageFromFile(string arg)
        {
            string[] parts = arg.Split(' ');
            int mesNo;
            int pages = 1;

            if (parts.Length < 2 || !int.TryParse(parts[1], out mesNo))
                return "ERROR Aufruf: mesfile <datei> <messageNo> [seiten]";

            if (parts.Length >= 3)
                int.TryParse(parts[2], out pages);

            string relative = parts[0];

            try
            {
                object mdt = GetMdtFromFile(relative);
                if (mdt == null)
                    return "ERROR Datei nicht ladbar: " + relative;

                string decoded = DecodeMessage(mdt, (ushort)mesNo, pages);

                // Wenn nichts herauskommt, stimmt die Annahme ueber das Format
                // nicht. Statt nur "(leer)" zu melden, werden dann die Rohwerte
                // mitgeliefert — daran laesst sich die Kodierung ablesen, ohne
                // erneut bauen und das Spiel neu starten zu muessen.
                if (decoded == "(leer)")
                    return decoded + "\n" + RawDump(mdt, (ushort)mesNo, 48);

                return decoded;
            }
            catch (Exception ex)
            {
                return "ERROR mesfile: " + Unwrap(ex).Message;
            }
        }

        private static object GetMdtFromFile(string relative)
        {
            object cached;
            if (_mdtCache.TryGetValue(relative, out cached))
                return cached;

            string full = Path.Combine(Application.streamingAssetsPath, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full))
                return null;

            byte[] bytes = File.ReadAllBytes(full);

            Type mdtType = typeof(GSStatic).Assembly.GetType("MdtData");
            if (mdtType == null)
                return null;

            object mdt = Activator.CreateInstance(mdtType, new object[] { bytes });
            _mdtCache[relative] = mdt;
            return mdt;
        }

        /// <summary>
        /// Gibt Offset und die ersten Rohwerte einer Nachricht aus — als
        /// Diagnosehilfe, wenn die Dekodierung nichts liefert.
        /// </summary>
        private static string RawDump(object mdt, ushort mesNo, int count)
        {
            try
            {
                Type mdtType = mdt.GetType();
                uint offset = (uint)
                    mdtType.GetMethod("GetMessageOffset").Invoke(mdt, new object[] { mesNo });
                var getMsg = mdtType.GetMethod("GetMessage");

                object mc = null;
                var cnt = mdtType.GetMethod("get_message_count");
                if (cnt != null)
                    mc = cnt.Invoke(mdt, null);

                StringBuilder nums = new StringBuilder();
                StringBuilder chars = new StringBuilder();
                for (int i = 0; i < count; i++)
                {
                    ushort v = (ushort)getMsg.Invoke(mdt, new object[] { (uint)(offset + i) });
                    nums.Append(v).Append(" ");
                    chars.Append(v >= 32 && v < 0xFFF0 ? (char)v : '.');
                }

                return "count=" + mc + " offset=" + offset
                    + "\nwerte: " + nums.ToString().Trim()
                    + "\nzeichen: " + chars.ToString();
            }
            catch (Exception ex)
            {
                return "RawDump-Fehler: " + Unwrap(ex).Message;
            }
        }

        /// <summary>
        /// Setzt den Text einer Nachricht aus den 16-Bit-Rohwerten zusammen.
        ///
        /// Aufbau (empirisch bestimmt): Werte ab 32 sind direkte Unicode-Zeichen,
        /// kleinere Werte sind Steuercodes (Seitenumbruch, Sprecherwechsel,
        /// Wartezeiten ...). Steuercodes werden uebersprungen; ein Seitenende
        /// beendet die Ausgabe, sofern nicht mehr Seiten angefordert wurden.
        /// Die Laenge ist zusaetzlich hart begrenzt, damit ein unerwartetes
        /// Format nicht in eine Endlosschleife laeuft.
        /// </summary>
        private static string DecodeMessage(object mdt, ushort mesNo, int pages)
        {
            Type mdtType = mdt.GetType();

            uint offset = (uint)
                mdtType.GetMethod("GetMessageOffset").Invoke(mdt, new object[] { mesNo });

            var getMsg = mdtType.GetMethod("GetMessage");

            StringBuilder sb = new StringBuilder();
            int pagesSeen = 0;
            const int MaxChars = 2000;

            for (int i = 0; i < MaxChars; i++)
            {
                ushort v;
                try
                {
                    v = (ushort)getMsg.Invoke(mdt, new object[] { (uint)(offset + i) });
                }
                catch
                {
                    break; // ueber das Dateiende hinaus
                }

                if (v == 0)
                {
                    // Nachrichtenende
                    break;
                }

                if (v == 1 || v == 2 || v == 3)
                {
                    // Seiten-/Zeilenwechsel: als Leerzeichen darstellen und
                    // gegebenenfalls nach der gewuenschten Seitenzahl aufhoeren.
                    pagesSeen++;
                    if (pagesSeen >= pages)
                        break;
                    sb.Append(" ");
                    continue;
                }

                if (v < 32)
                    continue; // sonstige Steuercodes ueberspringen

                sb.Append((char)v);
            }

            string text = sb.ToString().Trim();
            return text.Length == 0 ? "(leer)" : text;
        }

        /// <summary>
        /// Reflection verpackt Fehler aus dem aufgerufenen Code in eine
        /// TargetInvocationException — die eigentliche Ursache steht innen.
        /// </summary>
        private static Exception Unwrap(Exception ex)
        {
            return ex.InnerException != null ? ex.InnerException : ex;
        }

        /// <summary>
        /// fast on|off — schaltet den eingebauten Schnelldurchlauf des
        /// Nachrichtensystems.
        ///
        /// Warum das wichtig ist: Der mit Abstand teuerste Teil beim Erfassen der
        /// Untersuchungspunkte sind die Zwischensequenzen vor jeder Ermittlung —
        /// in Episode 2 reichten 190 Tastendruecke nicht bis zum Ermittlungsteil.
        /// Das Spiel bringt dafuer Entwicklerschalter mit: `debug_skip_` laesst
        /// Text durchlaufen, `debug_no_key_wait_` entfernt das Warten auf einen
        /// Tastendruck. Beide werden per Reflection gesetzt, weil sie nicht Teil
        /// der oeffentlichen Spiel-API sind.
        ///
        /// ACHTUNG: Das veraendert nur die Anzeigegeschwindigkeit, nicht den
        /// Spielstand. Trotzdem gehoert es ausgeschaltet, bevor Jana wieder
        /// selbst spielt — sonst rauscht der Text an ihr vorbei.
        /// </summary>
        private static string FastForward(string arg)
        {
            bool on = !string.Equals(arg.Trim(), "off", StringComparison.OrdinalIgnoreCase);

            try
            {
                Type advType = typeof(GSStatic).Assembly.GetType("advCtrl");
                if (advType == null)
                    return "ERROR advCtrl nicht gefunden";

                // Instanz besorgen: erst ueber die uebliche Singleton-Eigenschaft,
                // sonst ueber ein statisches Feld gleichen Zwecks.
                object adv = null;
                var instProp = advType.GetProperty(
                    "instance",
                    System.Reflection.BindingFlags.Public
                        | System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Static
                );
                if (instProp != null)
                    adv = instProp.GetValue(null, null);

                if (adv == null)
                {
                    var instField = advType.GetField(
                        "instance_",
                        System.Reflection.BindingFlags.Public
                            | System.Reflection.BindingFlags.NonPublic
                            | System.Reflection.BindingFlags.Static
                    );
                    if (instField != null)
                        adv = instField.GetValue(null);
                }

                if (adv == null)
                    return "ERROR advCtrl-Instanz nicht verfuegbar (laeuft eine Szene?)";

                var msField = advType.GetField(
                    "message_system_",
                    System.Reflection.BindingFlags.Public
                        | System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );
                if (msField == null)
                    return "ERROR Feld message_system_ nicht gefunden";

                object ms = msField.GetValue(adv);
                if (ms == null)
                    return "ERROR MessageSystem noch nicht erzeugt";

                Type msType = ms.GetType();
                var flags =
                    System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Instance;

                StringBuilder sb = new StringBuilder();
                foreach (string name in new string[] { "debug_skip_", "debug_no_key_wait_" })
                {
                    var f = msType.GetField(name, flags);
                    if (f == null)
                    {
                        sb.Append(name).Append("=fehlt ");
                        continue;
                    }
                    f.SetValue(ms, on);
                    sb.Append(name).Append("=").Append(on ? "an" : "aus").Append(" ");
                }

                return "ok " + sb.ToString().Trim();
            }
            catch (Exception ex)
            {
                return "ERROR fast: " + Unwrap(ex).Message;
            }
        }

        private static string Say(string arg)
        {
            if (string.IsNullOrEmpty(arg))
                return "ERROR kein Text angegeben";

            try
            {
                UnityAccessibilityLib.SpeechManager.Announce(arg);
                return "ok gesprochen";
            }
            catch (Exception ex)
            {
                return "ERROR say: " + ex.Message;
            }
        }
    }
}
