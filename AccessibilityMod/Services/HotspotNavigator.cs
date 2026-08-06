using System;
using System.Collections.Generic;
using System.Linq;
using AccessibilityMod.Core;
using AccessibilityMod.Utilities;
using UnityAccessibilityLib;
using UnityEngine;

namespace AccessibilityMod.Services
{
    public static class HotspotNavigator
    {
        private static List<HotspotInfo> _hotspots = new List<HotspotInfo>();
        private static int _currentIndex = -1;
        private static bool _wasActive = false;
        private static bool _lastIsSlider = false;
        private static float _lastStableBgPosX = float.NaN;

        // Max X of hotspots BEFORE any half-screen filtering (used to detect wide scenes reliably)
        private static float _lastUnfilteredHotspotMaxX = 1920f;

        public class HotspotInfo
        {
            public uint MessageId;
            public int DataIndex;
            public float CenterX;
            public float CenterY;
            public bool IsExamined;
            public string Description;

            /// <summary>
            /// Die vier Eckpunkte der Trefferflaeche (Spielkoordinaten, wie CenterX/Y).
            /// Gebraucht, um den Cursor gezielt in DIESEN Punkt zu setzen und nicht
            /// in einen ueberlappenden Nachbarn: Das Spiel untersucht bei Enter den
            /// Hotspot unter dem Cursor; liegt der Schwerpunkt eines Punktes zufaellig
            /// in der Trefferflaeche eines anderen, wird sonst der falsche untersucht
            /// (von Jana gefunden am 26./29.07.2026: Punkt 5 -> immer Punkt 6).
            /// </summary>
            public float X0,
                Y0,
                X1,
                Y1,
                X2,
                Y2,
                X3,
                Y3;

            /// <summary>
            /// Rohwert des item-Feldes aus INSPECT_DATA. 0 bedeutet "kein Bezug
            /// zu einem Beweisstueck".
            /// </summary>
            public uint ItemId;

            /// <summary>
            /// Offizieller, lokalisierter Name des zugehoerigen Beweisstuecks —
            /// oder null, wenn der Punkt keinen Bezug hat bzw. nicht aufloesbar
            /// war. Kommt aus den Spieldaten selbst (piceDataCtrl), ist also
            /// spielbegriffstreu und automatisch in der Spielsprache.
            /// </summary>
            public string ItemName;
        }

        /// <summary>
        /// Loest die item-Nummer eines Hotspots in den offiziellen Namen des
        /// Beweisstuecks auf.
        ///
        /// Weg durch die Spieldaten (per Reflection ueber Assembly-CSharp.dll
        /// ermittelt, es gibt keinen Decompiled-Ordner in diesem Checkout):
        ///   piceDataCtrl.instance.note_data -> List&lt;piceData&gt; des laufenden Spiels
        ///   piceData.no                     -> Nummer, passt zu INSPECT_DATA.item
        ///   piceData.name                   -> fertiger, lokalisierter Anzeigename
        /// Die Text-ID-Aufloesung (name_id_j_/u_/g_ je Sprache) macht das Spiel
        /// selbst in der name-Eigenschaft — deshalb hier kein eigenes Mapping.
        /// </summary>
        /// <summary>
        /// Baut die gesprochene Beschreibung eines Punktes. Die Nummer bleibt
        /// immer erhalten — Jana navigiert auch ueber sie ("Punkt 3") und braucht
        /// sie zur Orientierung. Der Beweisstueck-Name kommt nur dazu, wenn er
        /// aufloesbar war.
        /// </summary>
        private static string BuildDescription(int number, HotspotInfo info, string posDesc)
        {
            // Reihenfolge der Namensquellen, beste zuerst:
            // 1. Gepflegter Hotspot-Name (aus dem, was das Spiel beim Untersuchen
            //    sagt — "Ein einfaches Bett." -> "Bett"). Am aussagekraeftigsten.
            // 2. Name des zugehoerigen Beweisstuecks, falls der Punkt einen hat.
            // 3. Nur die Nummer, wie bisher.
            try
            {
                int bgNo = bgCtrl.instance != null ? bgCtrl.instance.bg_no : -1;
                string configured = HotspotNameService.GetName(bgNo, info.MessageId);
                if (!Net35Extensions.IsNullOrWhiteSpace(configured))
                    return L.Get("navigation.point_position_named", number, configured, posDesc);
            }
            catch { }

            if (!Net35Extensions.IsNullOrWhiteSpace(info.ItemName))
                return L.Get("navigation.point_position_named", number, info.ItemName, posDesc);

            return L.Get("navigation.point_position", number, posDesc);
        }

        private static string ResolveItemName(uint itemId)
        {
            // Fuellwerte, die "kein Beweisbezug" bedeuten. 255 (0xFF) ist der in
            // den Spieldaten tatsaechlich verwendete Marker — beim Auslesen der
            // Szenentabellen am 19.07.2026 trugen praktisch alle Punkte ohne
            // Beweisbezug genau diesen Wert. 0 und sehr grosse Werte werden
            // vorsichtshalber ebenfalls abgewiesen.
            if (itemId == 0 || itemId == 255 || itemId >= 0xFFFF)
                return null;

            try
            {
                var ctrl = piceDataCtrl.instance;
                if (ctrl == null)
                    return null;

                var notes = ctrl.note_data;
                if (notes == null)
                    return null;

                for (int i = 0; i < notes.Count; i++)
                {
                    var pice = notes[i];
                    if (pice == null)
                        continue;

                    if (pice.no == (int)itemId)
                    {
                        string name = pice.name;
                        return Net35Extensions.IsNullOrWhiteSpace(name) ? null : name;
                    }
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Warning(
                    $"[Hotspot] Item {itemId} nicht aufloesbar: {ex.Message}"
                );
            }

            return null;
        }

        /// <summary>
        /// Called each frame to detect when investigation mode starts/ends.
        /// </summary>
        public static void Update()
        {
            bool isActive = AccessibilityState.IsInInvestigationMode();

            if (isActive && !_wasActive)
            {
                // Investigation mode just started
                OnInvestigationStart();
            }
            else if (!isActive && _wasActive)
            {
                // Investigation mode just ended
                OnInvestigationEnd();
            }

            if (isActive)
            {
                try
                {
                    var bg = bgCtrl.instance;
                    if (bg != null)
                    {
                        float x = bg.bg_pos_x;
                        // When slider finishes (half-screen pan completes), refresh hotspot list so it
                        // only contains the current visible half.
                        bool isSlider = bg.is_slider;
                        if (float.IsNaN(_lastStableBgPosX))
                            _lastStableBgPosX = x;
                        if (_lastIsSlider && !isSlider)
                        {
                            if (Math.Abs(x - _lastStableBgPosX) > 0.5f)
                            {
                                _lastStableBgPosX = x;
                                RefreshHotspots();
                                // Announce side, hotspot count, and unexamined count after switching
                                string side =
                                    x < 960f
                                        ? L.Get("investigation.side_left")
                                        : L.Get("investigation.side_right");
                                int unexamined = _hotspots.Count(h => !h.IsExamined);
                                string message = L.Get(
                                    "investigation.scene_switched_info",
                                    side,
                                    _hotspots.Count,
                                    unexamined
                                );
                                SpeechManager.Announce(message, GameTextType.Investigation);
                            }
                        }
                        if (!isSlider)
                        {
                            _lastStableBgPosX = x;
                        }
                        _lastIsSlider = isSlider;
                    }
                }
                catch { }
            }

            _wasActive = isActive;
        }

        private static void OnInvestigationEnd()
        {
            // Don't clear _hotspots or _currentIndex here.
            // RefreshHotspots() preserves the current position by reading
            // _hotspots[_currentIndex] before rebuilding the list.
            // Clearing here would lose the position when investigation mode
            // temporarily ends (e.g., during hotspot examination dialogue).
        }

        public static void RefreshHotspots()
        {
            // Remember the current hotspot's identity before clearing
            uint? previousMessageId = null;
            int? previousDataIndex = null;
            if (_hotspots.Count > 0 && _currentIndex >= 0 && _currentIndex < _hotspots.Count)
            {
                previousMessageId = _hotspots[_currentIndex].MessageId;
                previousDataIndex = _hotspots[_currentIndex].DataIndex;
            }

            _hotspots.Clear();

            try
            {
                if (GSStatic.inspect_data_ == null)
                {
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                        "No inspection data available"
                    );
                    return;
                }

                for (int i = 0; i < GSStatic.inspect_data_.Length; i++)
                {
                    var data = GSStatic.inspect_data_[i];

                    // End of list marker - place is max value
                    if (data == null || data.place == uint.MaxValue)
                        break;

                    // Skip disabled hotspots (place 254)
                    if (data.place == 254)
                        continue;

                    // Calculate center of quadrilateral
                    float centerX = (data.x0 + data.x1 + data.x2 + data.x3) / 4f;
                    float centerY = (data.y0 + data.y1 + data.y2 + data.y3) / 4f;

                    // Check if already examined
                    bool examined = false;
                    try
                    {
                        ushort inspectNo = inspectCtrl.GetNextInspectNumber((uint)data.message);
                        examined = GSStatic.global_work_.inspect_readed_[0, inspectNo] == 1;
                    }
                    catch { }

                    // Generate position description
                    string posDesc = GetPositionDescription(centerX, centerY);

                    // Beweisbezug aufloesen, solange wir die Rohdaten hier haben.
                    uint itemId = data.item;
                    string itemName = ResolveItemName(itemId);

                    // Jede gefundene Zuordnung protokollieren: So laesst sich im
                    // Log nachvollziehen, ob die Namen sinnvoll sind — und ob sie
                    // womoeglich verraten, was man beim Untersuchen erst finden
                    // soll (dann muesste die Ansage auf "erst nach Untersuchen"
                    // umgestellt werden).
                    if (itemName != null)
                    {
                        AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                            $"[Hotspot] msg={data.message} item={itemId} -> \"{itemName}\""
                        );
                    }

                    var info = new HotspotInfo
                    {
                        MessageId = data.message,
                        DataIndex = i,
                        CenterX = centerX,
                        CenterY = centerY,
                        IsExamined = examined,
                        ItemId = itemId,
                        ItemName = itemName,
                        // Eckpunkte mitnehmen, damit MoveCursorToCurrentHotspot den
                        // Cursor in eine Stelle setzen kann, die NUR zu diesem Punkt
                        // gehoert (siehe Ueberlappungs-Bug).
                        X0 = data.x0,
                        Y0 = data.y0,
                        X1 = data.x1,
                        Y1 = data.y1,
                        X2 = data.x2,
                        Y2 = data.y2,
                        X3 = data.x3,
                        Y3 = data.y3,
                    };
                    // Duplikate mit derselben Nachricht IMMER zusammenfassen —
                    // unabhaengig von der Position. Ursprünglich (Bug 4, Van am
                    // Haupttor) hatte ich das an einen 15px-Abstand gekoppelt, in der
                    // Annahme, dieselbe Nachricht an weit entfernten Positionen sei
                    // dasselbe Objekt aus zwei Blickwinkeln und solle getrennt bleiben.
                    // Das war falsch: Jana meldete (Bug 5/6, 04.08.2026, Szenario 11),
                    // dass bei zwei weiter auseinanderliegenden Punkten mit gleicher
                    // Nachricht (s11/247 "nackte Laubbaeume", s11/248 "Reihe Plastik-
                    // baenke" — beides Objekte, die sich ueber die Szene erstrecken)
                    // immer nur EINER untersuchbar war; der andere reagierte auf
                    // Enter nicht mehr. Grund: inspectCtrl.GetNextInspectNumber()
                    // (Decompiled/inspectCtrl.cs) ermittelt den "untersucht"-Status
                    // ausschliesslich ueber die Nachrichten-ID (target_num), nicht
                    // ueber die Position — das Spiel selbst kennt pro Nachricht nur
                    // EIN inspect_readed_-Flag. Zwei Punkte mit gleicher Nachricht
                    // sind fuer das Spiel also immer dasselbe Ziel, egal wie weit sie
                    // auseinanderliegen; einen zweiten, "toten" Navigationspunkt dafuer
                    // anzubieten, ist fuer Screenreader-Nutzer nur eine Sackgasse.
                    bool isDuplicate = false;
                    for (int k = 0; k < _hotspots.Count; k++)
                    {
                        if (_hotspots[k].MessageId == info.MessageId)
                        {
                            isDuplicate = true;
                            break;
                        }
                    }
                    if (isDuplicate)
                        continue;

                    info.Description = BuildDescription(i + 1, info, posDesc);
                    _hotspots.Add(info);
                }

                // Capture max X BEFORE filtering (so OnInvestigationStart can know this is a wide scene)
                try
                {
                    _lastUnfilteredHotspotMaxX =
                        _hotspots.Count > 0 ? _hotspots.Max(h => h.CenterX) : 1920f;
                    if (_lastUnfilteredHotspotMaxX < 1920f)
                        _lastUnfilteredHotspotMaxX = 1920f;
                }
                catch
                {
                    _lastUnfilteredHotspotMaxX = 1920f;
                }

                // Filter to the currently visible half when the background supports sliding/panning.
                // This MUST be based on game state (bg_pos_x), not mod-derived cursor coordinates.
                {
                    float bgPosX = 0f;
                    float bgWidth = 1920f;
                    int bgNo = -1;
                    bool canSlide = false;
                    try
                    {
                        if (bgCtrl.instance != null)
                        {
                            bgPosX = bgCtrl.instance.bg_pos_x;
                            bgNo = bgCtrl.instance.bg_no;
                            if (bgCtrl.instance.sprite_data != null)
                            {
                                bgWidth = bgCtrl.instance.sprite_data.rect.width;
                            }
                        }
                    }
                    catch { }
                    try
                    {
                        canSlide = GSMain_TanteiPart.IsBGSlide(bgNo);
                    }
                    catch { }

                    // Some versions/scenes don't populate bgCtrl.sprite_data reliably.
                    // Derive an effective width from the inspection data coordinates (game data),
                    // so half-screen filtering still works on large scenes.
                    try
                    {
                        if (_hotspots.Count > 0)
                        {
                            float dataMaxX = _hotspots.Max(h => h.CenterX);
                            if (dataMaxX > bgWidth)
                                bgWidth = dataMaxX;
                        }
                    }
                    catch { }

                    if (canSlide && bgWidth > 1920f)
                    {
                        float minX = bgPosX;
                        float maxX = bgPosX + 1920f;
                        _hotspots = _hotspots
                            .Where(h => h.CenterX >= minX && h.CenterX <= maxX)
                            .ToList();
                    }
                }

                // Sort by position: top-to-bottom, then left-to-right
                _hotspots = _hotspots.OrderBy(h => h.CenterY).ThenBy(h => h.CenterX).ToList();

                // Reassign descriptions after sorting
                for (int i = 0; i < _hotspots.Count; i++)
                {
                    var h = _hotspots[i];
                    string posDesc = GetPositionDescription(h.CenterX, h.CenterY);
                    h.Description = BuildDescription(i + 1, h, posDesc);
                }

                // Restore position to previously selected hotspot if it still exists
                _currentIndex = -1;
                if (previousMessageId.HasValue && _hotspots.Count > 0)
                {
                    for (int i = 0; i < _hotspots.Count; i++)
                    {
                        if (_hotspots[i].MessageId == previousMessageId.Value)
                        {
                            _currentIndex = i;
                            break;
                        }
                    }
                }

                AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                    $"Found {_hotspots.Count} hotspots, position restored to {_currentIndex + 1}"
                );
            }
            catch (Exception ex)
            {
                _currentIndex = -1;
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error refreshing hotspots: {ex.Message}"
                );
            }
        }

        private static string GetPositionDescription(float x, float y)
        {
            // Assuming 1920x1080 resolution
            string horizontal =
                x < 640 ? L.Get("position.left")
                : x > 1280 ? L.Get("position.right")
                : L.Get("position.center");
            string vertical =
                y < 360 ? L.Get("position.top")
                : y > 720 ? L.Get("position.bottom")
                : L.Get("position.middle");
            return $"{vertical} {horizontal}";
        }

        public static void NavigateNext()
        {
            if (_hotspots.Count == 0)
            {
                RefreshHotspots();
            }

            if (_hotspots.Count == 0)
            {
                SpeechManager.Announce(L.Get("navigation.no_points"), GameTextType.Investigation);
                return;
            }

            _currentIndex = (_currentIndex + 1) % _hotspots.Count;
            AnnounceCurrentHotspot();
            MoveCursorToCurrentHotspot();
        }

        public static void NavigatePrevious()
        {
            if (_hotspots.Count == 0)
            {
                RefreshHotspots();
            }

            if (_hotspots.Count == 0)
            {
                SpeechManager.Announce(L.Get("navigation.no_points"), GameTextType.Investigation);
                return;
            }

            // When starting from -1 (no selection), go to the last item
            if (_currentIndex < 0)
            {
                _currentIndex = _hotspots.Count - 1;
            }
            else
            {
                _currentIndex = (_currentIndex - 1 + _hotspots.Count) % _hotspots.Count;
            }
            AnnounceCurrentHotspot();
            MoveCursorToCurrentHotspot();
        }

        public static void NavigateToNextUnexamined()
        {
            if (_hotspots.Count == 0)
            {
                RefreshHotspots();
            }

            // Refresh examined status
            RefreshExaminedStatus();

            var unexamined = _hotspots.Where(h => !h.IsExamined).ToList();

            if (unexamined.Count == 0)
            {
                SpeechManager.Announce(
                    L.Get("navigation.all_examined"),
                    GameTextType.Investigation
                );
                return;
            }

            // Find next unexamined after current index
            int startIndex = _currentIndex;
            for (int i = 1; i <= _hotspots.Count; i++)
            {
                int checkIndex = (startIndex + i) % _hotspots.Count;
                if (!_hotspots[checkIndex].IsExamined)
                {
                    _currentIndex = checkIndex;
                    AnnounceCurrentHotspot();
                    MoveCursorToCurrentHotspot();
                    return;
                }
            }

            SpeechManager.Announce(L.Get("navigation.all_examined"), GameTextType.Investigation);
        }

        private static void RefreshExaminedStatus()
        {
            try
            {
                foreach (var hotspot in _hotspots)
                {
                    ushort inspectNo = inspectCtrl.GetNextInspectNumber(hotspot.MessageId);
                    hotspot.IsExamined = GSStatic.global_work_.inspect_readed_[0, inspectNo] == 1;
                }
            }
            catch { }
        }

        public static void AnnounceCurrentHotspot()
        {
            if (_hotspots.Count == 0 || _currentIndex >= _hotspots.Count)
            {
                SpeechManager.Announce(
                    L.Get("navigation.no_point_selected"),
                    GameTextType.Investigation
                );
                return;
            }

            var hotspot = _hotspots[_currentIndex];
            string status = hotspot.IsExamined ? " " + L.Get("navigation.examined_suffix") : "";
            string message = $"{hotspot.Description}{status}";

            SpeechManager.Announce(message, GameTextType.Investigation);
        }

        public static void AnnounceAllHotspots()
        {
            if (_hotspots.Count == 0)
            {
                RefreshHotspots();
            }

            RefreshExaminedStatus();

            if (_hotspots.Count == 0)
            {
                SpeechManager.Announce(L.Get("navigation.no_points"), GameTextType.Investigation);
                return;
            }

            int examined = _hotspots.Count(h => h.IsExamined);
            int unexamined = _hotspots.Count - examined;

            string summary =
                L.Get("navigation.x_points", _hotspots.Count)
                + ". "
                + L.Get("navigation.examined_remaining", examined, unexamined);

            // List unexamined ones
            var unexaminedList = _hotspots.Where(h => !h.IsExamined).ToList();
            if (unexaminedList.Count > 0 && unexaminedList.Count <= 5)
            {
                summary += " " + L.Get("navigation.unexamined_list") + " ";
                summary += string.Join(", ", unexaminedList.Select(h => h.Description).ToArray());
            }

            SpeechManager.Announce(summary, GameTextType.Investigation);
        }

        private static void MoveCursorToCurrentHotspot()
        {
            if (_hotspots.Count == 0 || _currentIndex >= _hotspots.Count)
                return;

            try
            {
                var hotspot = _hotspots[_currentIndex];

                // Convert from background coordinates to screen coordinates
                // Account for background scroll position
                float bgOffsetX = 0;
                try
                {
                    if (bgCtrl.instance != null)
                    {
                        bgOffsetX = bgCtrl.instance.bg_pos_x;
                    }
                }
                catch { }

                // Zielpunkt in Spielkoordinaten bestimmen. Normalerweise der
                // Schwerpunkt — aber wenn der in der Trefferflaeche eines anderen
                // Punktes liegt, wuerde das Spiel bei Enter den falschen untersuchen.
                // Deshalb suchen wir einen Punkt, der NUR zu diesem Hotspot gehoert.
                float targetX, targetY;
                GetSafeCursorPoint(hotspot, out targetX, out targetY);

                // Calculate screen position
                // Game uses 1920x1080 coordinate system, cursor is centered
                float screenX = targetX - bgOffsetX - 960f;
                float screenY = 540f - targetY;

                // Update cursor position in inspectCtrl
                if (inspectCtrl.instance != null)
                {
                    inspectCtrl.instance.pos_x_ = screenX;
                    inspectCtrl.instance.pos_y_ = screenY;

                    // Update the cursor visual position using reflection (cursor_ is private)
                    var cursorField = typeof(inspectCtrl).GetField(
                        "cursor_",
                        System.Reflection.BindingFlags.NonPublic
                            | System.Reflection.BindingFlags.Instance
                    );
                    if (cursorField != null)
                    {
                        var cursor =
                            cursorField.GetValue(inspectCtrl.instance) as AssetBundleSprite;
                        if (cursor != null)
                        {
                            cursor.transform.localPosition = new Vector3(screenX, screenY, -1f);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error moving cursor: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Liefert einen Cursorpunkt (Spielkoordinaten), der eindeutig zum Ziel-
        /// Hotspot gehoert. Hintergrund: Das Spiel untersucht bei Enter den Hotspot
        /// unter dem Cursor. Trefferflaechen ueberlappen sich manchmal; liegt der
        /// Schwerpunkt des Ziels in der Flaeche eines anderen Punktes, wuerde der
        /// falsche untersucht (Bug: Punkt 5 -> immer Punkt 6, weil dessen Flaeche
        /// den Schwerpunkt von Punkt 5 enthaelt).
        ///
        /// Strategie: Ist der Schwerpunkt frei (in keinem fremden Viereck), nimm ihn.
        /// Sonst taste vom Schwerpunkt in Richtung der vier Ecken ab, bis ein Punkt
        /// gefunden ist, der im Ziel-Viereck liegt und in keinem fremden. Findet sich
        /// keiner (Ziel vollstaendig ueberdeckt), bleibt es beim Schwerpunkt — dann
        /// ist es nicht schlechter als bisher.
        /// </summary>
        private static void GetSafeCursorPoint(HotspotInfo target, out float x, out float y)
        {
            x = target.CenterX;
            y = target.CenterY;

            // Schwerpunkt frei? Dann fertig — haeufigster Fall, keine Aenderung.
            if (!IsInsideAnyOther(x, y, target))
                return;

            // Ecken des Ziel-Vierecks, in die wir uns vom Schwerpunkt bewegen.
            float[] vx = { target.X0, target.X1, target.X2, target.X3 };
            float[] vy = { target.Y0, target.Y1, target.Y2, target.Y3 };

            // Je weiter Richtung Ecke, desto eher raus aus der Ueberlappung. Mehrere
            // Bruchteile probieren; naeher an der Mitte bevorzugt (kleinstes t zuerst).
            float[] fractions = { 0.5f, 0.65f, 0.8f, 0.9f, 0.97f };
            foreach (float t in fractions)
            {
                for (int v = 0; v < 4; v++)
                {
                    float px = target.CenterX + t * (vx[v] - target.CenterX);
                    float py = target.CenterY + t * (vy[v] - target.CenterY);
                    // Muss im Ziel liegen (Ecken koennen konkav sein) UND frei sein.
                    if (IsPointInQuad(px, py, target) && !IsInsideAnyOther(px, py, target))
                    {
                        x = px;
                        y = py;
                        return;
                    }
                }
            }

            // Kein eindeutiger Punkt gefunden: Schwerpunkt beibehalten (Rueckfall).
            AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                $"[Hotspot] Punkt msg={target.MessageId} vollstaendig ueberdeckt - "
                    + "Cursor bleibt auf Schwerpunkt"
            );
        }

        /// <summary>
        /// Liegt (px,py) in der Trefferflaeche IRGENDEINES anderen sichtbaren Punktes?
        /// Vergleich ueber DataIndex, damit derselbe Punkt nicht sich selbst zaehlt.
        /// </summary>
        private static bool IsInsideAnyOther(float px, float py, HotspotInfo target)
        {
            for (int i = 0; i < _hotspots.Count; i++)
            {
                var h = _hotspots[i];
                if (h.DataIndex == target.DataIndex)
                    continue;
                if (IsPointInQuad(px, py, h))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Punkt-im-Viereck-Test (Even-Odd-Regel ueber die vier Ecken in Reihenfolge).
        /// Bewusst nicht auf konvexe Vierecke beschraenkt — die Spieldaten enthalten
        /// auch konkave Trefferflaechen.
        /// </summary>
        private static bool IsPointInQuad(float px, float py, HotspotInfo h)
        {
            float[] xs = { h.X0, h.X1, h.X2, h.X3 };
            float[] ys = { h.Y0, h.Y1, h.Y2, h.Y3 };

            bool inside = false;
            int j = 3;
            for (int i = 0; i < 4; i++)
            {
                if (
                    ((ys[i] > py) != (ys[j] > py))
                    && (px < (xs[j] - xs[i]) * (py - ys[i]) / (ys[j] - ys[i]) + xs[i])
                )
                {
                    inside = !inside;
                }
                j = i;
            }
            return inside;
        }

        public static int GetHotspotCount()
        {
            if (_hotspots.Count == 0)
            {
                RefreshHotspots();
            }
            return _hotspots.Count;
        }

        /// <summary>
        /// Der aktuell ausgewaehlte Punkt (0-basiert), oder -1 wenn keiner steht.
        /// Gebraucht vom BugReportService: Wenn Jana beim Testen die Bug-Taste
        /// drueckt, soll der Bericht festhalten, auf welchem Untersuchungspunkt
        /// sie gerade stand — sonst muesste sie die Stelle nachtraeglich
        /// beschreiben, was mit Screenreader muehsam ist.
        /// </summary>
        public static int GetCurrentIndex()
        {
            return _currentIndex;
        }

        /// <summary>
        /// Lesezugriff auf die geparste Hotspot-Liste. Fuer die DevBridge, damit
        /// die Automatisierung die Punkte aufzaehlen und ihre Bildausschnitte
        /// schneiden kann, ohne die Parselogik ein zweites Mal zu bauen.
        /// </summary>
        public static List<HotspotInfo> GetHotspots()
        {
            if (_hotspots.Count == 0)
                RefreshHotspots();
            return _hotspots;
        }

        /// <summary>
        /// Springt direkt auf einen Punkt (0-basiert) statt schrittweise zu
        /// navigieren — gedacht fuer die DevBridge-Automatisierung. Gibt false
        /// zurueck, wenn es den Punkt nicht gibt.
        /// </summary>
        public static bool NavigateToIndex(int index)
        {
            if (_hotspots.Count == 0)
                RefreshHotspots();

            if (index < 0 || index >= _hotspots.Count)
                return false;

            _currentIndex = index;
            MoveCursorToCurrentHotspot();
            return true;
        }

        public static int GetUnexaminedCount()
        {
            RefreshExaminedStatus();
            return _hotspots.Count(h => !h.IsExamined);
        }

        public static void OnInvestigationStart()
        {
            RefreshHotspots();

            if (_hotspots.Count > 0)
            {
                int unexamined = _hotspots.Count(h => !h.IsExamined);

                // Start with mode name
                string message = L.Get("investigation.mode_start");

                // Check if we need Q-switch hint and determine current side
                bool shouldHintQ = false;
                string sideHint = "";
                try
                {
                    int bgNo = -1;
                    float bgPosX = 0f;
                    bool canSlide = false;
                    float effectiveWidth = 1920f;
                    try
                    {
                        if (bgCtrl.instance != null)
                        {
                            bgNo = bgCtrl.instance.bg_no;
                            bgPosX = bgCtrl.instance.bg_pos_x;
                        }
                    }
                    catch { }
                    try
                    {
                        canSlide = GSMain_TanteiPart.IsBGSlide(bgNo);
                    }
                    catch { }
                    // Use the unfiltered max X captured in RefreshHotspots() (do not use filtered list)
                    effectiveWidth = _lastUnfilteredHotspotMaxX;

                    shouldHintQ = canSlide && effectiveWidth > 1920f;

                    if (shouldHintQ)
                    {
                        // Determine which side we're currently on
                        // bg_pos_x < 960 means left side (showing X coordinates 0-1920)
                        // bg_pos_x >= 960 means right side (showing X coordinates 1920+)
                        sideHint =
                            bgPosX < 960f
                                ? L.Get("investigation.current_side_left")
                                : L.Get("investigation.current_side_right");
                        message += " " + sideHint;
                    }
                }
                catch
                {
                    // ignore
                }

                // Add points count
                message += " " + L.Get("investigation.points_count", _hotspots.Count);

                // Add unexamined count if any
                if (unexamined > 0)
                {
                    message += " " + L.Get("investigation.unexamined_count", unexamined);
                }

                // Add controls hint
                message += " " + L.Get("investigation.controls_hint");

                // Add Q-switch hint if needed
                if (shouldHintQ)
                {
                    message += " " + L.Get("investigation.press_q_switch_half");
                }

                SpeechManager.Announce(message, GameTextType.Investigation);
            }
            else
            {
                SpeechManager.Announce(
                    L.Get("investigation.mode_start_no_points"),
                    GameTextType.Investigation
                );
            }
        }
    }
}
