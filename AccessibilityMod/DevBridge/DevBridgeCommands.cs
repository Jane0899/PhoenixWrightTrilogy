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
