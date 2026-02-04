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

        public static void Init(ConfigFile config)
        {
            // General
            ActionSlotsEnabled = config.Bind("General", "ActionSlotsEnabled", true, "Enable or disable custom action slots.");

            // Hotbar
            Rows = config.Bind("Hotbar", "Rows", 1, new ConfigDescription("Number of action bar rows.", new AcceptableValueRange<int>(1, 4)));
            SlotsPerRow = config.Bind("Hotbar", "SlotsPerRow", 8, new ConfigDescription("Number of slots per row.", new AcceptableValueRange<int>(1, 20)));
            Scale = config.Bind("Hotbar", "Scale", 100, new ConfigDescription("Scale of the action bars in percent.", new AcceptableValueRange<int>(50, 200)));
            
            HideLeftNav = config.Bind("Hotbar", "HideLeftNav", false, "Hide the left navigation arrows.");
            CombatMode = config.Bind("Hotbar", "CombatMode", true, "Automatically show action bars when entering combat.");
            ShowCooldownTimer = config.Bind("Hotbar", "ShowCooldownTimer", true, "Show numeric cooldown timer on slots.");
            PreciseCooldownTime = config.Bind("Hotbar", "PreciseCooldownTime", false, "Show decimal precision for cooldowns.");
            
            EmptySlotOption = config.Bind("Hotbar", "EmptySlotDisplay", "Transparent", 
                new ConfigDescription("How empty slots should look.", 
                new AcceptableValueList<string>("Transparent", "Image", "Hidden")));

            // UI Positioning
            OpenPositioningUI = config.Bind("UI Positioning", "Open Visual Editor", false,
                new ConfigDescription("Click to open the visual drag-and-drop editor.", null,
                new ConfigurationManagerAttributes { CustomDrawer = DrawPositionButton, HideDefaultButton = true }));

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
                var menus = Object.FindObjectOfType<PlayerActionMenus>();
                if (menus != null)
                {
                    menus.MainSettingsMenu.ShowMenu(ActionSettingsMenus.UIPosition);
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
