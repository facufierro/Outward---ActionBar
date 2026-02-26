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
        public const string VERSION = "2.0.87";

        public static ManualLogSource Log;

        public const int MAX_BARS = 4;
        public const int MAX_SLOTS = 20;

        public static ConfigEntry<bool>[] Enabled   = new ConfigEntry<bool>[MAX_BARS];
        public static ConfigEntry<int>[]  SlotCount = new ConfigEntry<int>[MAX_BARS];
        public static ConfigEntry<int>[]  PositionX = new ConfigEntry<int>[MAX_BARS];
        public static ConfigEntry<int>[]  PositionY = new ConfigEntry<int>[MAX_BARS];
        public static ConfigEntry<int>[]  Scale     = new ConfigEntry<int>[MAX_BARS];
        public static ConfigEntry<int>[]  SlotGap   = new ConfigEntry<int>[MAX_BARS];
        
        public static ConfigEntry<bool> SetHotkeyMode;
        public static ConfigEntry<bool> HideBackpack;
        public static ConfigEntry<bool> HideBandage;

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
            SetHotkeyMode = Config.Bind("Global Settings", "Edit Mode", false,
                new ConfigDescription("Click to enter Edit Mode (drag bars, assign hotkeys).", null,
                new ConfigurationManagerAttributes {
                    CustomDrawer = DrawEditModeButton,
                    HideDefaultButton = true,
                    Order = -1
                }));
                
            Config.Bind("Global Settings", "Reset HUD", false,
                new ConfigDescription("Reset all moved HUD elements to their default positions.", null,
                new ConfigurationManagerAttributes {
                    CustomDrawer = DrawResetHudButton,
                    HideDefaultButton = true,
                    Order = -2
                }));

            HideBackpack = Config.Bind("Global Settings", "Hide Backpack", false,
                new ConfigDescription("Hide the backpack icon from the HUD.", null,
                new ConfigurationManagerAttributes { Order = -3 }));

            HideBackpack.SettingChanged += (sender, args) => {
                if (HudMoverManager.Instance != null) HudMoverManager.Instance.UpdateVisibilityOnConfigChange();
            };

            HideBandage = Config.Bind("Global Settings", "Hide Bandage", false,
                new ConfigDescription("Hide the bandage icon from the HUD.", null,
                new ConfigurationManagerAttributes { Order = -4 }));

            HideBandage.SettingChanged += (sender, args) => {
                if (HudMoverManager.Instance != null) HudMoverManager.Instance.UpdateVisibilityOnConfigChange();
            };

            // Bar Settings
            for (int b = 0; b < MAX_BARS; b++)
            {
                string section = $"Bar {b + 1} Settings";
                bool isEnabled = b == 0; // Bar 1 enabled by default
                int barIdx = b;

                // Collect attributes for all entries in this bar so we can toggle Browsable
                var barAttrs = new System.Collections.Generic.List<ConfigurationManagerAttributes>();

                Enabled[b] = Config.Bind(section, "Enabled", isEnabled,
                    new ConfigDescription($"Enable Action Bar {b + 1}", null,
                    new ConfigurationManagerAttributes { HideDefaultButton = true }));

                var slotsAttr = new ConfigurationManagerAttributes { HideDefaultButton = true };
                barAttrs.Add(slotsAttr);
                SlotCount[b] = Config.Bind(section, "Slots", 8,
                    new ConfigDescription($"Number of quickslot buttons displayed for Bar {b + 1}",
                        new AcceptableValueRange<int>(1, MAX_SLOTS), slotsAttr));

                var posXAttr = new ConfigurationManagerAttributes {
                    CustomDrawer = DrawIntSlider, HideDefaultButton = true };
                barAttrs.Add(posXAttr);
                PositionX[b] = Config.Bind(section, "Position X", 85,
                    new ConfigDescription($"Horizontal position (0 = left, 100 = right)",
                        new AcceptableValueRange<int>(0, 100), posXAttr));

                int defaultY = 5 + (b * 10);
                var posYAttr = new ConfigurationManagerAttributes {
                    CustomDrawer = DrawIntSlider, HideDefaultButton = true };
                barAttrs.Add(posYAttr);
                PositionY[b] = Config.Bind(section, "Position Y", defaultY,
                    new ConfigDescription($"Vertical position (0 = bottom, 100 = top)",
                        new AcceptableValueRange<int>(0, 100), posYAttr));

                var scaleAttr = new ConfigurationManagerAttributes { HideDefaultButton = true };
                barAttrs.Add(scaleAttr);
                Scale[b] = Config.Bind(section, "Scale", 100,
                    new ConfigDescription($"Size of the action bar in percent",
                        new AcceptableValueRange<int>(1, 200), scaleAttr));

                var gapAttr = new ConfigurationManagerAttributes { HideDefaultButton = true };
                barAttrs.Add(gapAttr);
                SlotGap[b] = Config.Bind(section, "Slot Gap", 8,
                    new ConfigDescription($"Space between slots in pixels",
                        new AcceptableValueRange<int>(0, 20), gapAttr));

                SlotKeys[b] = new ConfigEntry<KeyCode>[MAX_SLOTS];
                for (int s = 0; s < MAX_SLOTS; s++)
                {
                    KeyCode defaultKey = (b == 0) ? DefaultKeys[s] : KeyCode.None;
                    SlotKeys[b][s] = Config.Bind($"Bar {b + 1} Keybinds", $"Slot{s + 1}", defaultKey,
                        new ConfigDescription($"Key for slot {s + 1} on Bar {b + 1}",
                        null,
                        new ConfigurationManagerAttributes { Browsable = false }));
                }

                // Reset button
                var resetAttr = new ConfigurationManagerAttributes {
                    CustomDrawer = (ConfigEntryBase _) => DrawResetBarButton(barIdx),
                    HideDefaultButton = true,
                    Order = -100
                };
                barAttrs.Add(resetAttr);
                Config.Bind(section, "Reset Bar", false,
                    new ConfigDescription($"Reset all settings for Bar {b + 1} to defaults (except Enabled)", null,
                    resetAttr));

                // Set initial visibility and listen for changes
                var capturedAttrs = barAttrs.ToArray();
                SetBarConfigVisible(capturedAttrs, isEnabled);
                Enabled[b].SettingChanged += (sender, args) => {
                    bool nowEnabled = ((ConfigEntry<bool>)sender).Value;
                    SetBarConfigVisible(capturedAttrs, nowEnabled);
                };
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

        private static void SetBarConfigVisible(ConfigurationManagerAttributes[] attrs, bool visible)
        {
            foreach (var attr in attrs)
                attr.Browsable = visible;

            // Force the Configuration Manager to rebuild its display
            RefreshConfigManager();
        }

        private static void RefreshConfigManager()
        {
            var configManagerType = System.Type.GetType(
                "ConfigurationManager.ConfigurationManager, ConfigurationManager");
            if (configManagerType == null) return;

            var configManager = Object.FindObjectOfType(configManagerType);
            if (configManager == null) return;

            // Toggle the window quickly to force a UI refresh so the rows actually disappear
            var prop = configManagerType.GetProperty("DisplayingWindow");
            if (prop != null)
            {
                bool isShowing = (bool)prop.GetValue(configManager, null);
                if (isShowing)
                {
                    prop.SetValue(configManager, false, null);
                    prop.SetValue(configManager, true, null);
                }
            }
        }

        private static void DrawResetBarButton(int barIndex)
        {
            if (GUILayout.Button($"Reset Bar {barIndex + 1} to Defaults", GUILayout.ExpandWidth(true)))
            {
                SlotCount[barIndex].Value = (int)SlotCount[barIndex].DefaultValue;
                PositionX[barIndex].Value = (int)PositionX[barIndex].DefaultValue;
                PositionY[barIndex].Value = (int)PositionY[barIndex].DefaultValue;
                Scale[barIndex].Value     = (int)Scale[barIndex].DefaultValue;
                SlotGap[barIndex].Value   = (int)SlotGap[barIndex].DefaultValue;

                for (int s = 0; s < MAX_SLOTS; s++)
                    SlotKeys[barIndex][s].Value = (KeyCode)SlotKeys[barIndex][s].DefaultValue;

                // Reset slot modes to Active
                var allHandlers = Object.FindObjectsOfType<SlotDropHandler>();
                foreach (var handler in allHandlers)
                {
                    if (handler.BarIndex == barIndex)
                        handler.Mode = SlotMode.Active;
                }

                // Save if character is loaded
                var manager = Object.FindObjectOfType<ActionBarManager>();
                if (manager != null) manager.SaveSlots();

                Log.LogMessage($"Bar {barIndex + 1}: reset to defaults.");
            }
        }

        private static void DrawEditModeButton(ConfigEntryBase entry)
        {
            var label = SlotDropHandler.IsEditMode ? "Exit Edit Mode" : "Enter Edit Mode";

            if (GUILayout.Button(label, GUILayout.ExpandWidth(true)))
            {
                SlotDropHandler.IsEditMode = !SlotDropHandler.IsEditMode;

                if (SlotDropHandler.IsEditMode)
                    CloseConfigWindow();
            }
        }
        
        private static void DrawResetHudButton(ConfigEntryBase entry)
        {
            if (GUILayout.Button("Reset All HUD Positions", GUILayout.ExpandWidth(true)))
            {
                if (HudMoverManager.Instance != null)
                {
                    HudMoverManager.Instance.ResetAllPositions();
                }
                else
                {
                    Log.LogWarning("Cannot reset HUD positions: HUD Manager not active (load a character first).");
                }
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
