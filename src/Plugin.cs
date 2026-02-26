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

        public const int MAX_BARS = 4;
        public const int MAX_SLOTS = 20;

        public static ConfigEntry<bool>[] Enabled   = new ConfigEntry<bool>[MAX_BARS];
        public static ConfigEntry<int>[]  SlotCount = new ConfigEntry<int>[MAX_BARS];
        public static ConfigEntry<int>[]  PositionX = new ConfigEntry<int>[MAX_BARS];
        public static ConfigEntry<int>[]  PositionY = new ConfigEntry<int>[MAX_BARS];
        public static ConfigEntry<int>[]  Scale     = new ConfigEntry<int>[MAX_BARS];
        
        public static ConfigEntry<bool> SetHotkeyMode;

        public static ConfigEntry<KeyCode>[][] SlotKeys = new ConfigEntry<KeyCode>[MAX_BARS][];

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

            // Global Settings
            SetHotkeyMode = Config.Bind("Global Settings", "Configure Hotkeys", false,
                new ConfigDescription("Click to enter hotkey assignment mode.", null,
                new ConfigurationManagerAttributes {
                    CustomDrawer = DrawHotkeyModeButton,
                    HideDefaultButton = true,
                    Order = -1
                }));

            // Bar Settings
            for (int b = 0; b < MAX_BARS; b++)
            {
                string section = $"Bar {b + 1} Settings";
                
                Enabled[b] = Config.Bind(section, "Enabled", b == 0,
                    new ConfigDescription($"Enable Action Bar {b + 1}"));

                SlotCount[b] = Config.Bind(section, "Slots", 8,
                    new ConfigDescription($"Number of quickslot buttons displayed for Bar {b + 1}",
                        new AcceptableValueRange<int>(1, MAX_SLOTS)));

                PositionX[b] = Config.Bind(section, "Position X", 85,
                    new ConfigDescription($"Horizontal position (0 = left, 100 = right)",
                        new AcceptableValueRange<int>(0, 100),
                    new ConfigurationManagerAttributes {
                        CustomDrawer = DrawIntSlider,
                        HideDefaultButton = true
                    }));

                // Stack default Y positions slightly so they don't exactly overlap if all enabled
                int defaultY = 5 + (b * 10);
                PositionY[b] = Config.Bind(section, "Position Y", defaultY,
                    new ConfigDescription($"Vertical position (0 = bottom, 100 = top)",
                        new AcceptableValueRange<int>(0, 100),
                    new ConfigurationManagerAttributes {
                        CustomDrawer = DrawIntSlider,
                        HideDefaultButton = true
                    }));

                Scale[b] = Config.Bind(section, "Scale", 100,
                    new ConfigDescription($"Size of the action bar in percent",
                        new AcceptableValueRange<int>(1, 200)));

                SlotKeys[b] = new ConfigEntry<KeyCode>[MAX_SLOTS];
                for (int s = 0; s < MAX_SLOTS; s++)
                {
                    KeyCode defaultKey = (b == 0) ? DefaultKeys[s] : KeyCode.None;
                    SlotKeys[b][s] = Config.Bind($"Bar {b + 1} Keybinds", $"Slot{s + 1}", defaultKey,
                        new ConfigDescription($"Key for slot {s + 1} on Bar {b + 1}",
                        null,
                        new ConfigurationManagerAttributes { Browsable = false }));
                }
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
