using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace fierrof.ActionBar
{
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency("com.sinai.SideLoader", BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID    = "fierrof.actionbar";
        public const string NAME    = "ActionBar";
        public const string VERSION = "2.2.0";

        public static ManualLogSource Log;

        public const int MAX_BARS = 4;
        public const int MAX_ROWS = 20;
        public const int MAX_SLOTS_PER_ROW = 20;
        public const int MAX_SLOTS_PER_BAR = MAX_ROWS * MAX_SLOTS_PER_ROW;
        public const int MAX_BINDABLE_SLOTS = 20;

        public static ConfigEntry<bool>[] Enabled   = new ConfigEntry<bool>[MAX_BARS];
        public static ConfigEntry<int>[]  SlotCount = new ConfigEntry<int>[MAX_BARS];
        public static ConfigEntry<int>[]  PositionX = new ConfigEntry<int>[MAX_BARS];
        public static ConfigEntry<int>[]  PositionY = new ConfigEntry<int>[MAX_BARS];
        public static ConfigEntry<int>[]  Scale     = new ConfigEntry<int>[MAX_BARS];
        public static ConfigEntry<int>[]  SlotGap   = new ConfigEntry<int>[MAX_BARS];
        public static ConfigEntry<int>[]  Rows      = new ConfigEntry<int>[MAX_BARS];
        
        public static ConfigEntry<bool> SetHotkeyMode;
        public static ConfigEntry<bool> HideBackpack;
        public static ConfigEntry<bool> HideBandage;
        public static ConfigEntry<int> LabelFontSize;
        public static ConfigEntry<int> CooldownFontSize;

        public static Dictionary<string, ConfigEntry<int>> HudElementScale
            = new Dictionary<string, ConfigEntry<int>>();

        public static ConfigEntry<KeyCode>[][] SlotKeys = new ConfigEntry<KeyCode>[MAX_BARS][];
        private static KeyCode[][] RuntimeSlotKeys = new KeyCode[MAX_BARS][];
        private static string ExtraKeybindsPath =>
            Path.Combine(BepInEx.Paths.ConfigPath, "ActionBar_ExtraKeybinds.txt");

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

            LabelFontSize = Config.Bind("Global Settings", "Label Font Size", 11,
                new ConfigDescription("Font size for hotkey and item count labels.",
                    new AcceptableValueRange<int>(6, 24),
                    new ConfigurationManagerAttributes { Order = -5 }));

            CooldownFontSize = Config.Bind("Global Settings", "Cooldown Font Size", 16,
                new ConfigDescription("Font size for the cooldown timer.",
                    new AcceptableValueRange<int>(8, 30),
                    new ConfigurationManagerAttributes { Order = -6 }));

            LabelFontSize.SettingChanged += (sender, args) => {
                foreach (var handler in FindObjectsOfType<SlotDropHandler>())
                    handler.RefreshFontSizes();
            };
            CooldownFontSize.SettingChanged += (sender, args) => {
                foreach (var handler in FindObjectsOfType<SlotDropHandler>())
                    handler.RefreshFontSizes();
            };

            // HUD Element Scaling
            foreach (var kvp in HudMoverManager.KnownElements)
            {
                string friendlyName = kvp.Value;
                var entry = Config.Bind("HUD Element Scaling", $"{friendlyName} Scale", 100,
                    new ConfigDescription($"Scale of {friendlyName} (100 = default)",
                        new AcceptableValueRange<int>(0, 500)));

                HudElementScale[friendlyName] = entry;

                string capturedName = friendlyName;
                entry.SettingChanged += (sender, args) =>
                {
                    if (HudMoverManager.Instance != null)
                        HudMoverManager.Instance.ApplyScale(capturedName, ((ConfigEntry<int>)sender).Value);
                };
            }

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
                        new AcceptableValueRange<int>(1, MAX_SLOTS_PER_ROW), slotsAttr));

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
                        new AcceptableValueRange<int>(0, 500), scaleAttr));

                var gapAttr = new ConfigurationManagerAttributes { HideDefaultButton = true };
                barAttrs.Add(gapAttr);
                SlotGap[b] = Config.Bind(section, "Slot Gap", 8,
                    new ConfigDescription($"Space between slots in pixels",
                        new AcceptableValueRange<int>(0, 20), gapAttr));

                var rowsAttr = new ConfigurationManagerAttributes { HideDefaultButton = true };
                barAttrs.Add(rowsAttr);
                Rows[b] = Config.Bind(section, "Rows", 1,
                    new ConfigDescription($"Number of rows (1 = horizontal bar, more = grid)",
                        new AcceptableValueRange<int>(1, MAX_ROWS), rowsAttr));

                SlotKeys[b] = new ConfigEntry<KeyCode>[MAX_BINDABLE_SLOTS];
                RuntimeSlotKeys[b] = new KeyCode[MAX_SLOTS_PER_BAR];
                for (int s = 0; s < MAX_BINDABLE_SLOTS; s++)
                {
                    KeyCode defaultKey = (b == 0 && s < DefaultKeys.Length)
                        ? DefaultKeys[s]
                        : KeyCode.None;
                    SlotKeys[b][s] = Config.Bind($"Bar {b + 1} Keybinds", $"Slot{s + 1}", defaultKey,
                        new ConfigDescription($"Key for slot {s + 1} on Bar {b + 1}",
                        null,
                        new ConfigurationManagerAttributes { Browsable = false }));

                    int bindBar = b;
                    int bindSlot = s;
                    SlotKeys[b][s].SettingChanged += (sender, args) => {
                        RuntimeSlotKeys[bindBar][bindSlot] = SlotKeys[bindBar][bindSlot].Value;
                    };
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

            InitializeRuntimeKeybinds();

            new Harmony(GUID).PatchAll();
            Log.LogMessage($"{NAME} v{VERSION} loaded. [dynamic-context-save-fix]");
        }

        private static void InitializeRuntimeKeybinds()
        {
            for (int b = 0; b < MAX_BARS; b++)
            {
                if (RuntimeSlotKeys[b] == null)
                    RuntimeSlotKeys[b] = new KeyCode[MAX_SLOTS_PER_BAR];

                for (int s = 0; s < MAX_SLOTS_PER_BAR; s++)
                {
                    RuntimeSlotKeys[b][s] = s < MAX_BINDABLE_SLOTS
                        ? SlotKeys[b][s].Value
                        : KeyCode.None;
                }
            }

            LoadExtraKeybinds();
        }

        private static void LoadExtraKeybinds()
        {
            if (!File.Exists(ExtraKeybindsPath)) return;

            try
            {
                foreach (var rawLine in File.ReadAllLines(ExtraKeybindsPath))
                {
                    var line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    var parts = line.Split('=');
                    if (parts.Length != 2) continue;

                    var indices = parts[0].Split(',');
                    if (indices.Length != 2) continue;

                    if (!int.TryParse(indices[0], out int barIndex)) continue;
                    if (!int.TryParse(indices[1], out int slotIndex)) continue;
                    if (barIndex < 0 || barIndex >= MAX_BARS) continue;
                    if (slotIndex < MAX_BINDABLE_SLOTS || slotIndex >= MAX_SLOTS_PER_BAR) continue;

                    if (System.Enum.TryParse(parts[1], out KeyCode key))
                        RuntimeSlotKeys[barIndex][slotIndex] = key;
                }
            }
            catch (System.Exception ex)
            {
                Log.LogWarning($"Failed to load extra keybinds: {ex.Message}");
            }
        }

        private static void SaveExtraKeybinds()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ExtraKeybindsPath));

                var lines = new System.Collections.Generic.List<string>();
                for (int b = 0; b < MAX_BARS; b++)
                {
                    for (int s = MAX_BINDABLE_SLOTS; s < MAX_SLOTS_PER_BAR; s++)
                    {
                        var key = RuntimeSlotKeys[b][s];
                        if (key == KeyCode.None) continue;
                        lines.Add($"{b},{s}={key}");
                    }
                }

                File.WriteAllLines(ExtraKeybindsPath, lines.ToArray());
            }
            catch (System.Exception ex)
            {
                Log.LogWarning($"Failed to save extra keybinds: {ex.Message}");
            }
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
                Rows[barIndex].Value      = (int)Rows[barIndex].DefaultValue;

                for (int s = 0; s < MAX_BINDABLE_SLOTS; s++)
                    SlotKeys[barIndex][s].Value = (KeyCode)SlotKeys[barIndex][s].DefaultValue;

                for (int s = 0; s < MAX_SLOTS_PER_BAR; s++)
                {
                    RuntimeSlotKeys[barIndex][s] = s < MAX_BINDABLE_SLOTS
                        ? SlotKeys[barIndex][s].Value
                        : KeyCode.None;
                }

                SaveExtraKeybinds();

                // Reset slot runtime state to defaults
                var allHandlers = Object.FindObjectsOfType<SlotDropHandler>();
                foreach (var handler in allHandlers)
                {
                    if (handler.BarIndex == barIndex)
                    {
                        if (handler.AssignedItem != null)
                            handler.ClearSlotSilent();

                        handler.Mode = SlotMode.Active;
                        handler.IsDynamic = false;
                        handler.RefreshEditModeVisuals();
                    }
                }

                SlotSaveManager.ClearBarPresets(barIndex);

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

        public static KeyCode GetBoundKey(int barIndex, int slotIndex)
        {
            if (barIndex < 0 || barIndex >= MAX_BARS) return KeyCode.None;
            if (slotIndex < 0 || slotIndex >= MAX_SLOTS_PER_BAR) return KeyCode.None;

            return RuntimeSlotKeys[barIndex][slotIndex];
        }

        public static void SetBoundKey(int barIndex, int slotIndex, KeyCode key)
        {
            if (barIndex < 0 || barIndex >= MAX_BARS) return;
            if (slotIndex < 0 || slotIndex >= MAX_SLOTS_PER_BAR) return;

            RuntimeSlotKeys[barIndex][slotIndex] = key;

            if (slotIndex < MAX_BINDABLE_SLOTS)
                SlotKeys[barIndex][slotIndex].Value = key;
            else
                SaveExtraKeybinds();
        }
    }
}
