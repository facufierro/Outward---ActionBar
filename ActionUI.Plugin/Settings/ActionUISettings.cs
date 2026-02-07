using BepInEx.Configuration;
using ModifAmorphic.Outward.Unity.ActionUI.Data;
using ModifAmorphic.Outward.Unity.ActionMenus;
using ModifAmorphic.Outward.Unity.ActionUI;
using System.IO;
using UnityEngine;
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
            EquipmentSetsEnabled = config.Bind("General", "Enable Equipment Sets", true,
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
                new ConfigDescription("Horizontal position of the Hotbar.", null, new ConfigurationManagerAttributes { IsAdvanced = false }));

            HotbarPositionY = config.Bind("UI Positioning", "Hotbar Y", 0f, 
                new ConfigDescription("Vertical position of the Hotbar.", null, new ConfigurationManagerAttributes { IsAdvanced = false }));

            SetHotkeyMode = config.Bind("Input", "Set Hotkey Mode", false,
                new ConfigDescription("Click to enter hotkey assignment mode.", null,
                new ConfigurationManagerAttributes { CustomDrawer = DrawHotkeyModeButton, HideDefaultButton = true, IsAdvanced = false }));

            // Serialized Data
            SerializedPositions = config.Bind("Internal", "SerializedPositions", "", 
                new ConfigDescription("JSON serialized UI positions.", null, new ConfigurationManagerAttributes { Browsable = false, IsAdvanced = true }));
            SerializedHotbars = config.Bind("Internal", "SerializedHotbars", "", 
                 new ConfigDescription("JSON serialized hotbar configuration.", null, new ConfigurationManagerAttributes { Browsable = false, IsAdvanced = true }));

            // Events
            Rows.SettingChanged += (s, e) => ApplyHotbarSettings();
            SlotsPerRow.SettingChanged += (s, e) => ApplyHotbarSettings();
            Scale.SettingChanged += (s, e) => ApplyHotbarSettings();
            HideLeftNav.SettingChanged += (s, e) => ApplyHotbarSettings();
            CombatMode.SettingChanged += (s, e) => ApplyHotbarSettings();
            ShowCooldownTimer.SettingChanged += (s, e) => ApplyHotbarSettings();
            PreciseCooldownTime.SettingChanged += (s, e) => ApplyHotbarSettings();
            EmptySlotOption.SettingChanged += (s, e) => ApplyHotbarSettings();
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
                // We might need to implement an event or manual refresh here if the old system relied on OnProfileChanged
                var menus = Object.FindObjectsOfType<PlayerActionMenus>();
                foreach (var menu in menus)
                {
                     // Previously: menu.ProfileManager.PositionsProfileService.Save();
                     // Now we need to trigger the update manually or through an event
                     // Check if we need to call something on the menu to reset
                }
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

        private static void ApplyHotbarSettings()
        {
            // Logic updated to use Config entries directly where possible, or refactor consumers
            // For now, consumers (like HotbarsStartup or the hotbar UI) should read from these static ConfigEntries directly.
            // This method might be redundant if we update the consumers to subscribe to config changes or check in Update()
            
            // However, to maintain some compatibility during refactor:
            var menus = Object.FindObjectsOfType<PlayerActionMenus>();
            foreach (var menu in menus)
            {
                 // We need to find the Hotbar script and update it.
                 // Previously: if (menu.ProfileManager.HotbarProfileService is ... jsonService)
                 
                 // If the Hotbar UI listens to these events or we can access it directly, fine.
                 // Ideally, the Hotbar UI components should reference ActionUISettings.Rows.Value, etc.
                 
                 // Let's assume for now we need to trigger a refresh on the hotbar.
                 // We will need to locate the hotbar component.
            }
        }
    }
}
