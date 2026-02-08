using BepInEx.Configuration;
using ModifAmorphic.Outward.Unity.ActionUI.Data;
using ModifAmorphic.Outward.Unity.ActionMenus;
using ModifAmorphic.Outward.Unity.ActionUI;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using ModifAmorphic.Outward.ActionUI.Services;

namespace ModifAmorphic.Outward.ActionUI.Settings
{
    public static class ActionUISettings
    {
        public static readonly string PluginPath = Path.GetDirectoryName(ActionUIPlugin.Instance.Info.Location);
        public static readonly string ConfigPath = Path.GetDirectoryName(ActionUIPlugin.Instance.Config.ConfigFilePath);
        public static readonly string GlobalKeymapsPath = Path.Combine(ConfigPath, "ActionUI_Keymaps");
        public static readonly string CharacterHotbarsPath = Path.Combine(ConfigPath, "ActionUI_CharacterSlots");

        public static class ActionViewer
        {
            public const string SkillsTab = "Skills";
            public const string ConsumablesTab = "Consumables";
            public const string DeployablesTab = "Deployables";
            public const string EquipmentSetsTab = "Equipment Sets";
            public const string WeaponsTab = "Weapons";
            public const string ArmorTab = "Armor";
            public const string CosmeticsTab = "Cosmetics";
            public const string EquippedTab = "Equipped";
        }

        // General
        // public static ConfigEntry<bool> ActionSlotsEnabled; // Removed as per previous config

        // Hotbar Configuration
        public static ConfigEntry<int> Rows;
        public static ConfigEntry<int> SlotsPerRow;
        public static ConfigEntry<int> Scale;
        public static ConfigEntry<bool> HideLeftNav;
        public static ConfigEntry<bool> CombatMode;
        public static ConfigEntry<bool> ShowCooldownTimer;
        public static ConfigEntry<bool> PreciseCooldownTime;
        public static ConfigEntry<string> EmptySlotOption;
        public static ConfigEntry<string> DisabledSlots;

        // UI Positioning
        public static ConfigEntry<bool> OpenPositioningUI;
        public static ConfigEntry<bool> ResetPositions;
        
        // Exact Positioning
        public static ConfigEntry<float> HotbarPositionX;

        public static ConfigEntry<float> HotbarPositionY;


        // Input
        public static ConfigEntry<bool> SetHotkeyMode;
        public static ConfigEntry<bool> EquipmentSetsEnabled;
        
        // Serialized Data (Hidden)
        public static ConfigEntry<string> SerializedPositions;
        public static ConfigEntry<string> SerializedHotbars;

        public static void Init(ConfigFile config)
        {
            // General
            EquipmentSetsEnabled = config.Bind("General", "Enable Equipment Sets", false,
                new ConfigDescription("Enable the Equipment Sets tab in the action viewer.", null, new ConfigurationManagerAttributes { IsAdvanced = false }));

            // Hotbar
            Rows = config.Bind("Hotbar", "Rows", 1, new ConfigDescription("Number of action bar rows.", new AcceptableValueRange<int>(1, 4), new ConfigurationManagerAttributes { IsAdvanced = false }));
            SlotsPerRow = config.Bind("Hotbar", "SlotsPerRow", 11, new ConfigDescription("Number of slots per row.", new AcceptableValueRange<int>(1, 20), new ConfigurationManagerAttributes { IsAdvanced = false }));
            Scale = config.Bind("Hotbar", "Scale", 100, new ConfigDescription("Scale of the action bars in percent.", new AcceptableValueRange<int>(50, 200), new ConfigurationManagerAttributes { IsAdvanced = false }));
            
            HideLeftNav = config.Bind("Hotbar", "HideLeftNav", false, new ConfigDescription("Hide the left navigation arrows.", null, new ConfigurationManagerAttributes { IsAdvanced = false }));
            CombatMode = config.Bind("Hotbar", "CombatMode", true, new ConfigDescription("Automatically show action bars when entering combat.", null, new ConfigurationManagerAttributes { IsAdvanced = false }));
            ShowCooldownTimer = config.Bind("Hotbar", "ShowCooldownTimer", true, new ConfigDescription("Show numeric cooldown timer on slots.", null, new ConfigurationManagerAttributes { IsAdvanced = false }));
            PreciseCooldownTime = config.Bind("Hotbar", "PreciseCooldownTime", false, new ConfigDescription("Show decimal precision for cooldowns.", null, new ConfigurationManagerAttributes { IsAdvanced = false }));
            
            EmptySlotOption = config.Bind("Hotbar", "EmptySlotDisplay", "Transparent", 
                new ConfigDescription("How empty slots should look.", 
                new AcceptableValueList<string>("Transparent", "Image", "Hidden"), new ConfigurationManagerAttributes { IsAdvanced = false }));

            DisabledSlots = config.Bind("Hotbar", "DisabledSlots", "",
                new ConfigDescription("Internal list of disabled slot indices.", null, new ConfigurationManagerAttributes { Browsable = false, IsAdvanced = true }));

            // UI Positioning
            OpenPositioningUI = config.Bind("UI Positioning", "Open Visual Editor", false,
                new ConfigDescription("Click to open the visual drag-and-drop editor.", null,
                new ConfigurationManagerAttributes { CustomDrawer = DrawPositionButton, HideDefaultButton = true, IsAdvanced = false }));

            ResetPositions = config.Bind("UI Positioning", "Reset UI Positions", false,
                new ConfigDescription("Reset all UI elements to their default positions.", null,
                new ConfigurationManagerAttributes { CustomDrawer = DrawResetInfo, HideDefaultButton = true, IsAdvanced = false }));

            HotbarPositionX = config.Bind("UI Positioning", "Hotbar X", 0f, 
                new ConfigDescription("Horizontal position of the Hotbar.", new AcceptableValueRange<float>(-3840f, 0f), 
                new ConfigurationManagerAttributes { IsAdvanced = false, CustomDrawer = DrawHotbarX, HideDefaultButton = true }));

            HotbarPositionY = config.Bind("UI Positioning", "Hotbar Y", 0f, 
                new ConfigDescription("Vertical position of the Hotbar.", new AcceptableValueRange<float>(-2160f, 0f), 
                new ConfigurationManagerAttributes { IsAdvanced = false, CustomDrawer = DrawHotbarY, HideDefaultButton = true }));



            SetHotkeyMode = config.Bind("Input", "Set Hotkey Mode", false,
                new ConfigDescription("Click to enter hotkey assignment mode.", null,
                new ConfigurationManagerAttributes { CustomDrawer = DrawHotkeyModeButton, HideDefaultButton = true, IsAdvanced = false }));

            // Serialized Data
            SerializedPositions = config.Bind("Internal", "SerializedPositions", "", 
                new ConfigDescription("JSON serialized UI positions.", null, new ConfigurationManagerAttributes { Browsable = false, IsAdvanced = true }));
            SerializedHotbars = config.Bind("Internal", "SerializedHotbars", "", 
                 new ConfigDescription("JSON serialized hotbar configuration.", null, new ConfigurationManagerAttributes { Browsable = false, IsAdvanced = true }));

            // Events
            // Handled by GlobalHotbarService to prevent race conditions
        }

        private static void CloseConfigWindow()
        {
            try 
            {
                var configManagerType = System.Type.GetType("ConfigurationManager.ConfigurationManager, ConfigurationManager");
                if (configManagerType == null) return;

                var configManager = Object.FindObjectOfType(configManagerType);
                if (configManager != null)
                {
                    var prop = configManagerType.GetProperty("DisplayingWindow");
                    if (prop != null)
                    {
                        prop.SetValue(configManager, false, null);
                    }
                }
            }
            catch (System.Exception)
            {
            }
        }

        private static void DrawPositionButton(ConfigEntryBase entry)
        {
            if (GUILayout.Button("Open Visual Editor", GUILayout.ExpandWidth(true)))
            {
                CloseConfigWindow();
                var menus = Object.FindObjectsOfType<PlayerActionMenus>();
                foreach(var menu in menus)
                {
                    menu.MainSettingsMenu.ShowMenu(ActionSettingsMenus.UIPosition);
                }
            }
        }

        private static void DrawResetInfo(ConfigEntryBase entry)
        {
             if (GUILayout.Button("Reset Positions", GUILayout.ExpandWidth(true)))
            {
                // Logic updated to use GlobalConfigService instead of ProfileManager
                GlobalConfigService.Instance.PositionsProfile.Positions.Clear();
                GlobalConfigService.Instance.SavePositions();
                
                // Force update UI
                var positionables = Object.FindObjectsOfType<PositionableUI>();
                foreach (var pos in positionables)
                {
                    pos.ResetToOrigin();
                }
            }
        }

        private static void DrawHotbarX(ConfigEntryBase entry)
        {
            float center = -Screen.width / 2f;
            string debugInfo = "";
            
            try 
            {
                var container = Object.FindObjectOfType<HotbarsContainer>();
                if (container != null)
                {
                    // 1. Get Screen Width
                    float pWidth = Screen.width;
                    var canvas = container.GetComponentInParent<Canvas>();
                    if (canvas != null && canvas.rootCanvas != null)
                         pWidth = canvas.rootCanvas.GetComponent<RectTransform>().rect.width;
                    
                    // 2. Safely Read Grid Properties
                    float padL = 0f;
                    float padR = 0f;
                    float slotSize = 50f;
                    float spacing = 5f;
                    
                    bool foundGrid = false;
                    
                    // Search for BaseHotbarGrid (safest template) or any active grid
                    var allGrids = container.GetComponentsInChildren<GridLayoutGroup>(true);
                    var targetGrid = allGrids.FirstOrDefault(g => g.name == "BaseHotbarGrid") ?? allGrids.FirstOrDefault();
                    
                    if (targetGrid != null)
                    {
                        padL = targetGrid.padding.left;
                        padR = targetGrid.padding.right;
                        slotSize = targetGrid.cellSize.x;
                        spacing = targetGrid.spacing.x;
                        foundGrid = true;
                    }

                    // 3. Calculate Grid Width
                    int slots = SlotsPerRow.Value;
                    // Grid Width = (Cell + Spacing) * Slots - Spacing + PadL + PadR
                    // Note: Unity Grid adds padding to the total width
                    float gridContentWidth = (slotSize + spacing) * slots - spacing; 
                    float gridTotalWidth = gridContentWidth + padL + padR;

                    // 4. Calculate Visual Offset
                    // We want Center of "Content" (Slots) at Screen Center.
                    // Content Center relative to Left Edge = PadL + GridContentWidth / 2.
                    // Right Edge relative to Left Edge = GridTotalWidth.
                    // Distance from Right Edge to Content Center:
                    // Dist = GridTotalWidth - (PadL + GridContentWidth / 2)
                    //      = (GridContent + PadL + PadR) - PadL - GridContent/2
                    //      = GridContent/2 + PadR.
                    
                    // We want: ScreenCenter = ContainerRight - Dist.
                    // ContainerRight = ScreenCenter + Dist.
                    
                    float scale = Scale.Value / 100f;
                    float scaledDist = (gridContentWidth / 2f + padR) * scale;
                    
                    // Pivot is Right (1). 
                    // Pos = -ScreenCenter + scaledDist.
                    
                    center = -(pWidth / 2f) + scaledDist;
                    
                    if (foundGrid)
                        debugInfo = "";
                    else
                        debugInfo = "";
                }
            }
            catch (System.Exception ex)
            {
                // Silent catch for release
                // UnityEngine.Debug.LogException(ex);
            }

            DrawHotbarSetting(entry, "Center", center);
        }

        private static void DrawHotbarY(ConfigEntryBase entry)
        {
            float center = -Screen.height / 2f;
            var container = Object.FindObjectOfType<HotbarsContainer>();
            
            if (container != null)
            {
                var rect = container.GetComponent<RectTransform>();
                
                // Safe Rebuild
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

                // Use Root Canvas Height (Screen Height)
                var canvas = container.GetComponentInParent<Canvas>();
                RectTransform canvasRect = null;
                
                if (canvas != null && canvas.rootCanvas != null)
                     canvasRect = canvas.rootCanvas.GetComponent<RectTransform>();
                else if (canvas != null)
                     canvasRect = canvas.GetComponent<RectTransform>();
                else
                     canvasRect = rect.parent as RectTransform;

                float pHeight = canvasRect ? canvasRect.rect.height : Screen.height;
                
                // Use actual Rect height (scaled)
                float barHeight = rect.rect.height * container.transform.localScale.y;

                // Pivot is Bottom (0). Value = -Pos
                center = -(pHeight / 2f) + (barHeight / 2f);
            }

            DrawHotbarSetting(entry, "Center", center);
        }

        private static void DrawHotbarSetting(ConfigEntryBase entry, string centerLabel, float centerValue)
        {
            float value = (float)entry.BoxedValue;
            var range = (AcceptableValueRange<float>)entry.Description.AcceptableValues;
            float min = (float)range.MinValue;
            float max = (float)range.MaxValue;
            
            GUILayout.BeginHorizontal();
            
            float newValue = GUILayout.HorizontalSlider(value, min, max, GUILayout.ExpandWidth(true));
            
            string text = GUILayout.TextField(newValue.ToString("F1"), GUILayout.Width(50));
            if (float.TryParse(text, out float parsed))
            {
                newValue = Mathf.Clamp(parsed, min, max);
            }

            if (GUILayout.Button(centerLabel, GUILayout.ExpandWidth(false)))
            {
                newValue = centerValue;
            }

            if (GUILayout.Button("Reset", GUILayout.ExpandWidth(false)))
            {
                newValue = (float)entry.DefaultValue;
            }
            
            GUILayout.EndHorizontal();
            
            if (Mathf.Abs(newValue - value) > 0.001f)
            {
                entry.BoxedValue = newValue;
            }
        }

        private static void DrawHotkeyModeButton(ConfigEntryBase entry)
        {
            if (GUILayout.Button("Enter Hotkey Mode", GUILayout.ExpandWidth(true)))
            {
                CloseConfigWindow();
                var menus = Object.FindObjectsOfType<PlayerActionMenus>();
                foreach (var menu in menus)
                {
                    if (menu.MainSettingsMenu != null && menu.MainSettingsMenu.HotkeyCaptureMenu != null)
                    {
                        menu.MainSettingsMenu.HotkeyCaptureMenu.Show();
                    }
                }
            }
        }

    }
}
