using System;
using AccessibilityMod.Patches;
using AccessibilityMod.Services;
using UnityAccessibilityLib;
using UnityEngine;

namespace AccessibilityMod.Core
{
    /// <summary>
    /// Handles all keyboard input for the accessibility mod.
    /// </summary>
    public static class InputManager
    {
        /// <summary>
        /// True when the "navigate to previous item" key was pressed this frame.
        /// Historically this was only [ (LeftBracket), but bracket keys do not exist
        /// as direct keys on many non-US layouts - e.g. German QWERTZ only produces
        /// [ via AltGr+8, and Unity's legacy Input never reports AltGr combinations
        /// as a single key press, so bracket navigation is unreachable there.
        /// Comma is a direct key on virtually every layout, so both are accepted.
        /// </summary>
        private static bool NavigatePreviousPressed()
        {
            return Input.GetKeyDown(KeyCode.LeftBracket) || Input.GetKeyDown(KeyCode.Comma);
        }

        /// <summary>
        /// True when the "navigate to next item" key was pressed this frame.
        /// ] (RightBracket) plus period as the layout-independent alternative;
        /// see NavigatePreviousPressed for why the alternative is needed.
        /// </summary>
        private static bool NavigateNextPressed()
        {
            return Input.GetKeyDown(KeyCode.RightBracket) || Input.GetKeyDown(KeyCode.Period);
        }

        /// <summary>
        /// Process input each frame. Called from AccessibilityMod.OnUpdate().
        /// </summary>
        public static void ProcessInput()
        {
            // F5 - Hot-reload configuration files
            if (Input.GetKeyDown(KeyCode.F5))
            {
                ReloadConfigFiles();
            }

            // R - Repeat last output (disabled in vase puzzle, vase show, and court record modes since R has other functions)
            if (
                Input.GetKeyDown(KeyCode.R)
                && !AccessibilityState.IsInVasePuzzleMode()
                && !AccessibilityState.IsInVaseShowMode()
                && !AccessibilityState.IsInCourtRecordMode()
            )
            {
                SpeechManager.RepeatLast();
            }

            // I - Announce current context/state
            if (Input.GetKeyDown(KeyCode.I))
            {
                AccessibilityState.AnnounceCurrentState();
            }

            ProcessModeSpecificInput();
        }

        private static void ProcessModeSpecificInput()
        {
            // 3D evidence examination mode (GS1 Episode 5+)
            if (AccessibilityState.IsIn3DEvidenceMode())
            {
                Handle3DEvidenceInput();
            }
            // Luminol spray mode (GS1 Episode 5)
            else if (AccessibilityState.IsInLuminolMode())
            {
                HandleLuminolInput();
            }
            // Vase puzzle mode (GS1 Episode 5)
            else if (AccessibilityState.IsInVasePuzzleMode())
            {
                HandleVasePuzzleInput();
            }
            // Fingerprint mode (GS1 Episode 5)
            else if (AccessibilityState.IsInFingerprintMode())
            {
                HandleFingerprintInput();
            }
            // Video tape examination mode (GS1 Episode 5)
            else if (AccessibilityState.IsInVideoTapeMode())
            {
                HandleVideoTapeInput();
            }
            // Orchestra music player mode
            else if (AccessibilityState.IsInOrchestraMode())
            {
                HandleOrchestraInput();
            }
            // Dying message mode (GS1 Episode 5 - connect the dots)
            else if (AccessibilityState.IsInDyingMessageMode())
            {
                HandleDyingMessageInput();
            }
            // Bug sweeper mode (GS2/GS3 - scan for listening devices)
            else if (AccessibilityState.IsInBugSweeperMode())
            {
                HandleBugSweeperInput();
            }
            // Vase show rotation mode (GS1 Episode 5 - unstable jar)
            else if (AccessibilityState.IsInVaseShowMode())
            {
                HandleVaseShowInput();
            }
            // Pointing mode navigation (court maps, etc.)
            else if (AccessibilityState.IsInPointingMode())
            {
                HandlePointingInput();
            }
            // Investigation mode hotspot navigation
            else if (AccessibilityState.IsInInvestigationMode())
            {
                HandleInvestigationInput();
            }
            // H - Announce life gauge (in trial, but not in pointing mode)
            else if (Input.GetKeyDown(KeyCode.H) && AccessibilityState.IsInTrialMode())
            {
                AccessibilityState.AnnounceLifeGauge();
            }
        }

        private static void Handle3DEvidenceInput()
        {
            if (NavigatePreviousPressed())
            {
                Evidence3DNavigator.NavigatePrevious();
            }

            if (NavigateNextPressed())
            {
                Evidence3DNavigator.NavigateNext();
            }
        }

        private static void HandleLuminolInput()
        {
            if (NavigatePreviousPressed())
            {
                LuminolNavigator.NavigatePrevious();
            }

            if (NavigateNextPressed())
            {
                LuminolNavigator.NavigateNext();
            }
        }

        private static void HandleVasePuzzleInput()
        {
            // F1 - Get hint for current step
            if (Input.GetKeyDown(KeyCode.F1))
            {
                VasePuzzleNavigator.AnnounceHint();
            }
        }

        private static void HandleFingerprintInput()
        {
            // [ and ] - Navigate fingerprint locations during selection phase
            if (NavigatePreviousPressed())
            {
                FingerprintNavigator.NavigatePrevious();
            }

            if (NavigateNextPressed())
            {
                FingerprintNavigator.NavigateNext();
            }

            // F1 - Get hint for current phase
            if (Input.GetKeyDown(KeyCode.F1))
            {
                FingerprintNavigator.AnnounceHint();
            }
        }

        private static void HandleVideoTapeInput()
        {
            // [ and ] - Navigate to targets when paused
            if (NavigatePreviousPressed())
            {
                VideoTapeNavigator.NavigateToPreviousTarget();
            }

            if (NavigateNextPressed())
            {
                VideoTapeNavigator.NavigateToNextTarget();
            }

            // F1 - Get hint for current viewing
            if (Input.GetKeyDown(KeyCode.F1))
            {
                VideoTapeNavigator.AnnounceHint();
            }
        }

        private static void HandleOrchestraInput()
        {
            // F1 - Announce controls help
            if (Input.GetKeyDown(KeyCode.F1))
            {
                GalleryOrchestraNavigator.AnnounceHelp();
            }
        }

        private static void HandleDyingMessageInput()
        {
            // [ and ] - Navigate between dots
            if (NavigatePreviousPressed())
            {
                DyingMessageNavigator.NavigatePrevious();
            }

            if (NavigateNextPressed())
            {
                DyingMessageNavigator.NavigateNext();
            }

            // F1 - Get hint for spelling EMA
            if (Input.GetKeyDown(KeyCode.F1))
            {
                DyingMessageNavigator.AnnounceHint();
            }
        }

        private static void HandleBugSweeperInput()
        {
            // F1 - Announce current state/hint
            if (Input.GetKeyDown(KeyCode.F1))
            {
                BugSweeperNavigator.AnnounceState();
            }
        }

        private static void HandleVaseShowInput()
        {
            // F1 - Get hint for rotation
            if (Input.GetKeyDown(KeyCode.F1))
            {
                VaseShowNavigator.AnnounceHint();
            }
        }

        private static void HandlePointingInput()
        {
            if (NavigatePreviousPressed())
            {
                PointingNavigator.NavigatePrevious();
            }

            if (NavigateNextPressed())
            {
                PointingNavigator.NavigateNext();
            }

            // F1 - List all target areas
            if (Input.GetKeyDown(KeyCode.F1))
            {
                PointingNavigator.AnnounceAllPoints();
            }
        }

        private static void HandleInvestigationInput()
        {
            if (NavigatePreviousPressed())
            {
                HotspotNavigator.NavigatePrevious();
            }

            if (NavigateNextPressed())
            {
                HotspotNavigator.NavigateNext();
            }

            // U - Next unexamined hotspot
            if (Input.GetKeyDown(KeyCode.U))
            {
                HotspotNavigator.NavigateToNextUnexamined();
            }

            // F1 - List all hotspots
            if (Input.GetKeyDown(KeyCode.F1))
            {
                HotspotNavigator.AnnounceAllHotspots();
            }
        }

        private static void ReloadConfigFiles()
        {
            try
            {
                // Reload localization first (may affect other services' paths)
                LocalizationService.ReloadFromFiles();
                CharacterNameService.ReloadFromFiles();
                EvidenceDetailService.ReloadFromFiles();
                HotspotNameService.ReloadFromFiles();

                // Die Beschreibungen der Punkte werden beim Einlesen der Szene
                // einmal gebaut und gespeichert. Ohne diesen Aufbau bliebe nach
                // dem Neuladen die ALTE Ansage stehen — man aendert einen Namen,
                // drueckt F5 und hoert trotzdem weiter "Punkt 1". Deshalb die
                // Hotspot-Liste gleich mit neu aufbauen.
                HotspotNavigator.RefreshHotspots();
                StaffRollPatches.ReloadData();
                SpeechManager.Announce(L.Get("system.config_reloaded"));
                AccessibilityMod.Logger.Msg("Configuration files reloaded via F5");
            }
            catch (Exception ex)
            {
                AccessibilityMod.Logger.Error($"Error reloading config files: {ex.Message}");
                SpeechManager.Announce(L.Get("system.config_reload_error"));
            }
        }
    }
}
