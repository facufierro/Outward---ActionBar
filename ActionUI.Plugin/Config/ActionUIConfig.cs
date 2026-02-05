using BepInEx.Configuration;
using ModifAmorphic.Outward.Unity.ActionUI.Data;
using ModifAmorphic.Outward.Unity.ActionMenus;
using ModifAmorphic.Outward.Unity.ActionUI;
using System.Linq;
using UnityEngine;

namespace ModifAmorphic.Outward.ActionUI.Config
{
    public static class ActionUIConfig
    {
        // General
        public static ConfigEntry<bool> ActionSlotsEnabled;

        // Hotbar Configuration
        public static ConfigEntry<int> Rows;
        public static ConfigEntry<int> SlotsPerRow;
        public static ConfigEntry<int> Scale;
        public static ConfigEntry<bool> HideLeftNav;
        public static ConfigEntry<bool> CombatMode;
        public static ConfigEntry<bool> ShowCooldownTimer;
        public static ConfigEntry<bool> PreciseCooldownTime;
        public static ConfigEntry<string> EmptySlotOption;

        // UI Positioning
        public static ConfigEntry<bool> OpenPositioningUI;
        public static ConfigEntry<bool> ResetPositions;

        // Input
        public static ConfigEntry<bool> SetHotkeyMode;

        public static void Init(ConfigFile config)
        {
            // General
            // ActionSlotsEnabled removed as requested

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

            // UI Positioning
            OpenPositioningUI = config.Bind("UI Positioning", "Open Visual Editor", false,
                new ConfigDescription("Click to open the visual drag-and-drop editor.", null,
                new ConfigurationManagerAttributes { CustomDrawer = DrawPositionButton, HideDefaultButton = true, IsAdvanced = false }));

            ResetPositions = config.Bind("UI Positioning", "Reset UI Positions", false,
                new ConfigDescription("Reset all UI elements to their default positions.", null,
                new ConfigurationManagerAttributes { CustomDrawer = DrawResetInfo, HideDefaultButton = true, IsAdvanced = false }));

             SetHotkeyMode = config.Bind("Input", "Set Hotkey Mode", false,
                new ConfigDescription("Click to enter hotkey assignment mode.", null,
                new ConfigurationManagerAttributes { CustomDrawer = DrawHotkeyModeButton, HideDefaultButton = true, IsAdvanced = false }));

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
            // Use reflection to avoid hard dependency on ConfigurationManager dll which causes build errors if missing
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
                // Ignore errors if config manager is not present or compatible
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
                var menus = Object.FindObjectsOfType<PlayerActionMenus>();
                foreach (var menu in menus)
                {
                    if (menu.ProfileManager != null && menu.ProfileManager.PositionsProfileService != null)
                    {
                        // Clear the profile
                        var profile = menu.ProfileManager.PositionsProfileService.GetProfile();
                        profile.Positions.Clear();
                        menu.ProfileManager.PositionsProfileService.Save();
                        // Reset PositionableUIs
                        // Saving triggers OnProfileChanged which calls ResetToOrigin internally in PositionableUI
                    }
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
                    // Show the HotkeyCaptureMenu properly instead of just toggling edits
                    // This ensures the dialog can be shown when users click hotkey buttons
                    if (menu.MainSettingsMenu != null && menu.MainSettingsMenu.HotkeyCaptureMenu != null)
                    {
                        menu.MainSettingsMenu.HotkeyCaptureMenu.Show();
                    }
                }
            }
        }

        private static void ApplyGlobalSettings()
        {
            // ActionSlotsEnabled removed
        }

        private static void ApplyHotbarSettings()
        {
            var menus = Object.FindObjectsOfType<PlayerActionMenus>();
            foreach (var menu in menus)
            {
                if (menu.ProfileManager != null && menu.ProfileManager.HotbarProfileService is Services.HotbarProfileJsonService jsonService)
                {
                    var profile = jsonService.GetProfile();
                    if (profile != null)
                    {
                        // Safely resize
                        jsonService.UpdateDimensions(Rows.Value, SlotsPerRow.Value);

                        // Apply new settings
                        jsonService.SetCooldownTimer(ShowCooldownTimer.Value, PreciseCooldownTime.Value);
                        jsonService.SetHideLeftNav(HideLeftNav.Value);
                        jsonService.SetCombatMode(CombatMode.Value);
                        jsonService.SetScale(Scale.Value);
                        
                        // Parse EmptySlotOption enum from string
                        if (System.Enum.TryParse<EmptySlotOptions>(EmptySlotOption.Value, out var option))
                        {
                            jsonService.SetEmptySlotView(option);
                        }
                        
                        // Update individual hotbars if needed, or trigger save which usually refreshes UI
                        jsonService.Save();
                    }
                }
            }
        }
    }
}
