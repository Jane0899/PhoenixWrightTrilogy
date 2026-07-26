using System;
using System.Collections.Generic;
using AccessibilityMod.Patches;
using AccessibilityMod.Services;
using MelonLoader;
using UnityAccessibilityLib;
using UnityEngine;

namespace AccessibilityMod.Core
{
    public class AccessibilityMod : MelonMod
    {
        public static MelonLogger.Instance Logger { get; private set; }
        public static AccessibilityMod Instance { get; private set; }

        private static bool _isInitialized = false;

        public override void OnInitializeMelon()
        {
            Instance = this;
            Logger = LoggerInstance;
            Logger.Msg("Phoenix Wright Accessibility Mod initializing...");

            // Initialize accessibility library
            InitializeAccessibilityLibrary();
        }

        private void InitializeAccessibilityLibrary()
        {
            // Set up logging
            AccessibilityLog.Logger = new MelonLoggerAdapter(Logger);

            // Set up text type names for logging
            SpeechManager.TextTypeNames = new Dictionary<int, string>
            {
                { GameTextType.Dialogue, "Dialogue" },
                { GameTextType.Narrator, "Narrator" },
                { GameTextType.Menu, "Menu" },
                { GameTextType.MenuChoice, "MenuChoice" },
                { GameTextType.System, "System" },
                { GameTextType.Investigation, "Investigation" },
                { GameTextType.Evidence, "Evidence" },
                { GameTextType.Trial, "Trial" },
                { GameTextType.PsycheLock, "PsycheLock" },
                { GameTextType.Credits, "Credits" },
            };

            // Configure repeat storage for game-specific types
            SpeechManager.ShouldStoreForRepeatPredicate = textType =>
                textType == GameTextType.Dialogue
                || textType == GameTextType.Narrator
                || textType == GameTextType.Credits;

            // Register game-specific text replacements for screen reader compatibility
            // Replace multiplication signs with 'x' (used in resolution strings like "1920☓1080")
            TextCleaner.AddReplacement("\u2613", "x"); // ☓ (ballot x)
            TextCleaner.AddReplacement("\u00D7", "x"); // × (multiplication sign)

            // Initialize speech
            if (SpeechManager.Initialize())
            {
                Logger.Msg("Speech system initialized");
            }
        }

        private void InitializeAccessibility()
        {
            if (_isInitialized)
                return;

            try
            {
                // Initialize localization first (needed by other services)
                LocalizationService.Initialize();

                GameObject managerObject = new GameObject("AccessibilityMod_CoroutineRunner");
                managerObject.AddComponent<CoroutineRunner>();

                // DevBridge erst hier starten (nicht in OnInitializeMelon): Sie
                // greift auf Unity-Objekte zu, die vor dem ersten Szenenladen
                // noch nicht existieren.
                DevBridge.DevBridgeServer.Start();

                Logger.Msg("Accessibility systems initialized successfully");
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to initialize accessibility systems: {ex.Message}");
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            Logger.Msg($"Scene loaded: {sceneName} (Index: {buildIndex})");

            // Initialize on first scene load when Unity is ready
            InitializeAccessibility();
        }

        public override void OnUpdate()
        {
            try
            {
                // Update navigators to detect mode changes
                UpdateNavigators();

                InputManager.ProcessInput();

                // DevBridge-Befehle abarbeiten. MUSS hier passieren: Netzwerk und
                // Einlesen laufen in Hintergrund-Threads, aber Spielobjekte duerfen
                // nur im Unity-Hauptthread angefasst werden.
                DevBridge.DevBridgeServer.PumpMainThread();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in OnUpdate: {ex.Message}");
            }
        }

        private void UpdateNavigators()
        {
            HotspotNavigator.Update();
            PointingNavigator.Update();
            LuminolNavigator.Update();
            VasePuzzleNavigator.Update();
            FingerprintNavigator.Update();
            VideoTapeNavigator.Update();
            VaseShowNavigator.Update();
            DyingMessageNavigator.Update();
            BugSweeperNavigator.Update();
            FirstLaunchDialogPatches.Update();

            // Sammelt beim Spielen die Ground-Truth-Namen von Untersuchungspunkten
            // (s<scenario>/<message> + verbatim Text). Fuer GS2/GS3, wo die Namen
            // offline nicht zuverlaessig ableitbar sind (siehe HotspotNameHarvester).
            HotspotNameHarvester.Update();
        }

        public override void OnDeinitializeMelon()
        {
            // Zuerst die Bridge stoppen, damit der Listener den Port freigibt und
            // ein Neustart des Spiels ihn wiederverwenden kann.
            DevBridge.DevBridgeServer.Stop();

            if (CoroutineRunner.Instance != null)
            {
                UnityEngine.Object.Destroy(CoroutineRunner.Instance.gameObject);
            }

            Logger.Msg("Phoenix Wright Accessibility Mod deinitialized.");
        }
    }
}
