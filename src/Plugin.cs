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

        public static ConfigEntry<int> SlotCount;
        public static ConfigEntry<int> PositionX;
        public static ConfigEntry<int> PositionY;
        public static ConfigEntry<int> Scale;
        public static ConfigEntry<bool> SetHotkeyMode;

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

            SlotCount = Config.Bind("Settings", "Slots", 8,
                new ConfigDescription("Number of quickslot buttons displayed",
                    new AcceptableValueRange<int>(1, MAX_SLOTS)));

            PositionX = Config.Bind("Settings", "Position X", 50,
                new ConfigDescription("Horizontal position (0 = left, 100 = right)",
                    new AcceptableValueRange<int>(0, 100),
                new ConfigurationManagerAttributes {
                    CustomDrawer = DrawIntSlider,
                    HideDefaultButton = true
                }));

            PositionY = Config.Bind("Settings", "Position Y", 5,
                new ConfigDescription("Vertical position (0 = bottom, 100 = top)",
                    new AcceptableValueRange<int>(0, 100),
                new ConfigurationManagerAttributes {
                    CustomDrawer = DrawIntSlider,
                    HideDefaultButton = true
                }));

            Scale = Config.Bind("Settings", "Scale", 100,
                new ConfigDescription("Size of the action bar in percent",
                    new AcceptableValueRange<int>(1, 200)));

            SetHotkeyMode = Config.Bind("Settings", "Configure Hotkeys", false,
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

        private static void DrawIntSlider(ConfigEntryBase entry)
        {
            int value = (int)entry.BoxedValue;
            var range = (AcceptableValueRange<int>)entry.Description.AcceptableValues;

            GUILayout.BeginHorizontal();

            float newFloatValue = GUILayout.HorizontalSlider(value, range.MinValue, range.MaxValue, GUILayout.ExpandWidth(true));
            int newValue = Mathf.RoundToInt(newFloatValue);

            string text = GUILayout.TextField(newValue.ToString(), GUILayout.Width(50));
            if (int.TryParse(text, out int parsed))
                newValue = (int)Mathf.Clamp(parsed, range.MinValue, range.MaxValue);

            if (GUILayout.Button("Reset", GUILayout.ExpandWidth(false)))
                newValue = (int)entry.DefaultValue;

            GUILayout.EndHorizontal();

            if (newValue != value)
                entry.BoxedValue = newValue;
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
