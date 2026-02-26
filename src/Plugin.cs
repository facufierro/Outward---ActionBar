using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace fierrof.ActionBar
{
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency("com.sinai.SideLoader", BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID    = "fierrof.actionbar";
        public const string NAME    = "ActionBar";
        public const string VERSION = "2.0.21";

        public static ManualLogSource Log;

        public static ConfigEntry<int>   SlotCount;
        public static ConfigEntry<float> PositionX;
        public static ConfigEntry<float> PositionY;
        public static ConfigEntry<float> Scale;
        public static ConfigEntry<bool>  SetHotkeyMode;

        public const int MAX_SLOTS = 20;
        public static ConfigEntry<KeyCode>[] SlotKeys = new ConfigEntry<KeyCode>[MAX_SLOTS];

        private static readonly KeyCode[] DefaultKeys = {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4,
            KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8,
            KeyCode.Alpha9, KeyCode.Alpha0, KeyCode.None, KeyCode.None,
            KeyCode.None, KeyCode.None, KeyCode.None, KeyCode.None,
            KeyCode.None, KeyCode.None, KeyCode.None, KeyCode.None
        };

        void Awake()
        {
            Log = Logger;

            SlotCount = Config.Bind("ActionBar", "SlotCount", 8,
                new ConfigDescription("Number of quickslot buttons displayed",
                    new AcceptableValueRange<int>(1, MAX_SLOTS)));

            PositionX = Config.Bind("ActionBar", "PositionX", 0.5f,
                new ConfigDescription("Horizontal position (0 = left, 1 = right)",
                    new AcceptableValueRange<float>(0f, 1f)));

            PositionY = Config.Bind("ActionBar", "PositionY", 0.05f,
                new ConfigDescription("Vertical position (0 = bottom, 1 = top)",
                    new AcceptableValueRange<float>(0f, 1f)));

            Scale = Config.Bind("ActionBar", "Scale", 1.0f,
                new ConfigDescription("Size multiplier for the action bar",
                    new AcceptableValueRange<float>(0.5f, 3f)));

            SetHotkeyMode = Config.Bind("ActionBar", "Configure Hotkeys", false,
                new ConfigDescription("Click to enter hotkey assignment mode.", null,
                new ConfigurationManagerAttributes {
                    CustomDrawer = DrawHotkeyModeButton,
                    HideDefaultButton = true,
                    Order = -1
                }));

            for (int i = 0; i < MAX_SLOTS; i++)
            {
                SlotKeys[i] = Config.Bind("Keybinds", $"Slot{i + 1}", DefaultKeys[i],
                    new ConfigDescription($"Key for slot {i + 1}",
                    null,
                    new ConfigurationManagerAttributes { Browsable = false }));
            }

            new Harmony(GUID).PatchAll();
            Log.LogMessage($"{NAME} v{VERSION} loaded.");
        }

        private static void DrawHotkeyModeButton(ConfigEntryBase entry)
        {
            var label = SlotDropHandler.IsConfigMode ? "Exit Hotkey Mode" : "Configure Hotkeys";

            if (GUILayout.Button(label, GUILayout.ExpandWidth(true)))
            {
                SlotDropHandler.IsConfigMode = !SlotDropHandler.IsConfigMode;

                if (SlotDropHandler.IsConfigMode)
                    CloseConfigWindow();
            }
        }

        private static void CloseConfigWindow()
        {
            var configManagerType = System.Type.GetType(
                "ConfigurationManager.ConfigurationManager, ConfigurationManager");
            if (configManagerType == null) return;

            var configManager = Object.FindObjectOfType(configManagerType);
            if (configManager == null) return;

            var prop = configManagerType.GetProperty("DisplayingWindow");
            prop?.SetValue(configManager, false, null);
        }
    }
}
