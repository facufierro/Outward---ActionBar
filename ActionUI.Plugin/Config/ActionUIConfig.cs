using BepInEx.Configuration;
using ModifAmorphic.Outward.Unity.ActionUI.Data;
using ModifAmorphic.Outward.Unity.ActionMenus;
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
            ActionSlotsEnabled = config.Bind("General", "ActionSlotsEnabled", true, 
                new ConfigDescription("Enable or disable custom action slots.", null, new ConfigurationManagerAttributes { IsAdvanced = false }));

            // Hotbar
            Rows = config.Bind("Hotbar", "Rows", 1, new ConfigDescription("Number of action bar rows.", new AcceptableValueRange<int>(1, 4), new ConfigurationManagerAttributes { IsAdvanced = false }));
            SlotsPerRow = config.Bind("Hotbar", "SlotsPerRow", 8, new ConfigDescription("Number of slots per row.", new AcceptableValueRange<int>(1, 20), new ConfigurationManagerAttributes { IsAdvanced = false }));
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
            ActionSlotsEnabled.SettingChanged += (s, e) => ApplyGlobalSettings();
            Rows.SettingChanged += (s, e) => ApplyHotbarSettings();
            SlotsPerRow.SettingChanged += (s, e) => ApplyHotbarSettings();
            Scale.SettingChanged += (s, e) => ApplyHotbarSettings();
            HideLeftNav.SettingChanged += (s, e) => ApplyHotbarSettings();
            CombatMode.SettingChanged += (s, e) => ApplyHotbarSettings();
            ShowCooldownTimer.SettingChanged += (s, e) => ApplyHotbarSettings();
            PreciseCooldownTime.SettingChanged += (s, e) => ApplyHotbarSettings();
            EmptySlotOption.SettingChanged += (s, e) => ApplyHotbarSettings();
        }

        private static void DrawPositionButton(ConfigEntryBase entry)
        {
            if (GUILayout.Button("Open Visual Editor", GUILayout.ExpandWidth(true)))
            {
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
                var menus = Object.FindObjectsOfType<PlayerActionMenus>();
                foreach (var menu in menus)
                {
                     // Toggle via HotbarsController logic if possible, or just open the menu?
                     // Controller has ToggleHotkeyEdits.
                     // Accessing via HotbarsContainer seems hardest part.
                     // PlayerActionMenus has ActionsViewer, but maybe not direct Ref to controller?
                     // Searching for HotbarsContainer in children.
                     var container = menu.GetComponentInChildren<HotbarsContainer>();
                     if (container != null && container.Controller != null)
                     {
                         container.Controller.ToggleHotkeyEdits(true);
                     }
                }
            }
        }

        public static void ApplyToProfile(IHotbarProfile profile)
        {
            if (profile == null) return;

            profile.Rows = Rows.Value;
            profile.SlotsPerRow = SlotsPerRow.Value;
            profile.Scale = Scale.Value;
            profile.HideLeftNav = HideLeftNav.Value;
            profile.CombatMode = CombatMode.Value;
            
            // Note: These settings are per-slot in existing system or spread across other configs
            // We need to verify where ActionConfig lives or if it's cleaner to just update active controllers
        }

        public static void ApplySettingsToActiveProfile()
        {
            ApplyGlobalSettings();
            ApplyHotbarSettings();
        }

        private static void ApplyGlobalSettings()
        {
            // Find active players and update their profiles
            var pspInstance = Psp.Instance;
            if (pspInstance == null) return;
            
            // This part is tricky without direct access to PlayerMenuService or iterating players.
            // We'll try finding PlayerActionMenus in the scene which handles active players.
            var menus = Object.FindObjectsOfType<PlayerActionMenus>();
            foreach (var menu in menus)
            {
                if (menu.ProfileManager != null && menu.ProfileManager.ProfileService != null)
                {
                    var profile = menu.ProfileManager.ProfileService.GetActiveProfile();
                    if (profile != null)
                    {
                        profile.ActionSlotsEnabled = ActionSlotsEnabled.Value;
                        menu.ProfileManager.ProfileService.Save();
                    }
                }
            }
        }

        private static void ApplyHotbarSettings()
        {
            var menus = Object.FindObjectsOfType<PlayerActionMenus>();
            foreach (var menu in menus)
            {
                if (menu.ProfileManager != null && menu.ProfileManager.HotbarProfileService != null)
                {
                    var profile = menu.ProfileManager.HotbarProfileService.GetProfile();
                    if (profile != null)
                    {
                        profile.Rows = Rows.Value;
                        profile.SlotsPerRow = SlotsPerRow.Value;
                        profile.Scale = Scale.Value;
                        profile.HideLeftNav = HideLeftNav.Value;
                        profile.CombatMode = CombatMode.Value;
                        
                        // Update individual hotbars if needed, or trigger save which usually refreshes UI
                        menu.ProfileManager.HotbarProfileService.Save();
                    }
                }
            }
        }
    }
}
