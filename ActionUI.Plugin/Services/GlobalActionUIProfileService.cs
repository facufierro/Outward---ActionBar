using ModifAmorphic.Outward.ActionUI.Settings;
using ModifAmorphic.Outward.Unity.ActionUI.Data;
using System;
using System.Collections.Generic;

namespace ModifAmorphic.Outward.ActionUI.Services
{
    public class GlobalActionUIProfileService : IActionUIProfileService
    {
        private GlobalActionUIProfile _globalProfile = new GlobalActionUIProfile();

        public event Action<IActionUIProfile> OnActiveProfileChanged;
        public event Action<IActionUIProfile> OnActiveProfileSwitched;
        public event Action<IActionUIProfile> OnActiveProfileSwitching;

        public IActionUIProfile GetActiveProfile() => _globalProfile;

        public IEnumerable<string> GetProfileNames() => new[] { "Global" };

        public void Save()
        {
            // Auto-saved by BepInEx
        }

        public void SaveNew(IActionUIProfile profile)
        {
            // No-op or update settings
        }

        public void SetActiveProfile(string name)
        {
            // No-op
        }

        public void Rename(string newName)
        {
            // No-op
        }

        private class GlobalActionUIProfile : IActionUIProfile
        {
            public string Name { get; set; } = "Global";
            
            // Assume enabled as the whole point of the mod is this. 
            // If we want a global toggle for the mod features, we'd add it to ActionUISettings.
            public bool ActionSlotsEnabled { get; set; } = true; 
            public bool DurabilityDisplayEnabled { get; set; } = true; // Was disabled in Startup
            
            public bool EquipmentSetsEnabled 
            { 
                get => ActionUISettings.EquipmentSetsEnabled.Value; 
                set => ActionUISettings.EquipmentSetsEnabled.Value = value; 
            }
            
            public bool SkillChainsEnabled { get; set; } = true;

            // These sub-profiles might need their own global settings handling if used
            // For now, returning defaults or empty
            public EquipmentSetsSettingsProfile EquipmentSetsSettingsProfile { get; set; } = new EquipmentSetsSettingsProfile();
            public StashSettingsProfile StashSettingsProfile { get; set; } = new StashSettingsProfile();
            public StorageSettingsProfile StorageSettingsProfile { get; set; } = new StorageSettingsProfile();
        }
    }
}
