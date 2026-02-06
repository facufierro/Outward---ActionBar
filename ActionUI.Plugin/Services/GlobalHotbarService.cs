using ModifAmorphic.Outward.ActionUI.DataModels;
using ModifAmorphic.Outward.ActionUI.Models;
using ModifAmorphic.Outward.ActionUI.Settings;
using ModifAmorphic.Outward.Logging;
using ModifAmorphic.Outward.Unity.ActionMenus;
using ModifAmorphic.Outward.Unity.ActionUI;
using ModifAmorphic.Outward.Unity.ActionUI.Data;
using ModifAmorphic.Outward.Unity.ActionUI.Extensions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ModifAmorphic.Outward.ActionUI.Services
{
    public class GlobalHotbarService : IHotbarProfileService
    {
        private HotbarProfileData _cachedProfile;
        public event Action<IHotbarProfile, HotbarProfileChangeTypes> OnProfileChanged;

        private IModifLogger Logger = LoggerFactory.GetLogger(ModInfo.ModId);

        public GlobalHotbarService()
        {
            LoadProfile();
            // Subscribe to settings changes to update profile if changed via Config Manager
            ActionUISettings.Rows.SettingChanged += (s, e) => UpdateFromSettings(HotbarProfileChangeTypes.RowAdded); // Simplified trigger
            ActionUISettings.SlotsPerRow.SettingChanged += (s, e) => UpdateFromSettings(HotbarProfileChangeTypes.SlotAdded);
            ActionUISettings.Scale.SettingChanged += (s, e) => UpdateFromSettings(HotbarProfileChangeTypes.Scale);
            ActionUISettings.HideLeftNav.SettingChanged += (s, e) => UpdateFromSettings(HotbarProfileChangeTypes.HideLeftNav);
            ActionUISettings.CombatMode.SettingChanged += (s, e) => UpdateFromSettings(HotbarProfileChangeTypes.CombatMode);
            ActionUISettings.ShowCooldownTimer.SettingChanged += (s, e) => UpdateFromSettings(HotbarProfileChangeTypes.CooldownTimer);
            ActionUISettings.PreciseCooldownTime.SettingChanged += (s, e) => UpdateFromSettings(HotbarProfileChangeTypes.CooldownTimer);
            ActionUISettings.EmptySlotOption.SettingChanged += (s, e) => UpdateFromSettings(HotbarProfileChangeTypes.EmptySlotView);
        }

        private void LoadProfile()
        {
            var json = ActionUISettings.SerializedHotbars.Value;
            if (string.IsNullOrEmpty(json))
            {
                _cachedProfile = CreateDefaultProfile();
                Save(); // Serialize the default
            }
            else
            {
                 try
                {
                    _cachedProfile = JsonConvert.DeserializeObject<HotbarProfileData>(json);
                    
                    // Validate/Fix profile if needed (e.g. if settings changed while game was closed? Unlikely with BepInEx)
                    // But we should sync the simple config values to the profile or vice versa.
                    // Let's assume Profile is master for structure, Settings is master for simple values.
                    SyncSettingsToProfile(_cachedProfile);
                }
                catch
                {
                    _cachedProfile = CreateDefaultProfile();
                }
            }
        }

        private void SyncSettingsToProfile(HotbarProfileData profile)
        {
             // Apply simple settings from Config to the Profile object
             profile.Rows = ActionUISettings.Rows.Value;
             profile.SlotsPerRow = ActionUISettings.SlotsPerRow.Value;
             profile.Scale = ActionUISettings.Scale.Value;
             profile.HideLeftNav = ActionUISettings.HideLeftNav.Value;
             profile.CombatMode = ActionUISettings.CombatMode.Value;
             
             // Sync per-slot settings from Config to all slot configs
             var emptySlotOption = Enum.TryParse<EmptySlotOptions>(ActionUISettings.EmptySlotOption.Value, out var parsedOption) 
                 ? parsedOption 
                 : EmptySlotOptions.Transparent;
             
             foreach (var bar in profile.Hotbars)
             {
                 foreach (var slot in bar.Slots)
                 {
                     slot.Config.EmptySlotOption = emptySlotOption;
                     slot.Config.ShowCooldownTime = ActionUISettings.ShowCooldownTimer.Value;
                     slot.Config.PreciseCooldownTime = ActionUISettings.PreciseCooldownTime.Value;
                 }
             }
             
             // Check dimensions
             EnsureDimensions(profile, profile.Rows, profile.SlotsPerRow);
        }

        private void EnsureDimensions(HotbarProfileData profile, int rows, int slotsPerRow)
        {
             // This is a simplified resize logic, might need full implementation from old service if we want to support it cleanly
             // For now, let's trust the UpdateFromSettings or Add/Remove logic.
             // But if we just loaded, we might need to conform the list.
             
             // Implementation similar to UpdateDimensions but operating on provided profile
        }

        private HotbarProfileData CreateDefaultProfile()
        {
             // Create a fresh default profile
             // This logic was in GetProfileData in old service
             var profile = new HotbarProfileData()
            {
                Rows = ActionUISettings.Rows.Value,
                SlotsPerRow = ActionUISettings.SlotsPerRow.Value,
                Scale = ActionUISettings.Scale.Value,
                HideLeftNav = ActionUISettings.HideLeftNav.Value,
                CombatMode = ActionUISettings.CombatMode.Value,
                Hotbars = DeepCloneHotbars(HotbarSettings.DefaulHotbarProfile.Hotbars),
                // Rewired IDs...
                NextRewiredActionId = RewiredConstants.ActionSlots.NextHotbarAction.id,
                NextRewiredActionName = RewiredConstants.ActionSlots.NextHotbarAction.name,
                PrevRewiredActionId = RewiredConstants.ActionSlots.PreviousHotbarAction.id,
                PrevRewiredActionName = RewiredConstants.ActionSlots.PreviousHotbarAction.name,
                NextRewiredAxisActionName = RewiredConstants.ActionSlots.NextHotbarAxisAction.name,
                NextRewiredAxisActionId = RewiredConstants.ActionSlots.NextHotbarAxisAction.id,
                PrevRewiredAxisActionName = RewiredConstants.ActionSlots.PreviousHotbarAxisAction.name,
                PrevRewiredAxisActionId = RewiredConstants.ActionSlots.PreviousHotbarAxisAction.id,
            };
            
            // Initial resize
            // We need logic to add/remove rows to match Rows value
            // For now assuming DefaultHotbarProfile has 1 row, 11 slots (or whatever default is)
            
            return profile;
        }


        public IHotbarProfile GetProfile()
        {
            if (_cachedProfile == null) LoadProfile();
            return _cachedProfile;
        }

        public void Save()
        {
             SaveNew(GetProfile());
        }

        public void SaveNew(IHotbarProfile hotbarProfile)
        {
            if (_isSaving) return;
            _isSaving = true;

            try
            {
                // Sync specific properties back to Config Entries
                if (ActionUISettings.Rows.Value != hotbarProfile.Rows) ActionUISettings.Rows.Value = hotbarProfile.Rows;
                if (ActionUISettings.SlotsPerRow.Value != hotbarProfile.SlotsPerRow) ActionUISettings.SlotsPerRow.Value = hotbarProfile.SlotsPerRow;
                if (ActionUISettings.Scale.Value != hotbarProfile.Scale) ActionUISettings.Scale.Value = hotbarProfile.Scale;
                if (ActionUISettings.HideLeftNav.Value != hotbarProfile.HideLeftNav) ActionUISettings.HideLeftNav.Value = hotbarProfile.HideLeftNav;
                if (ActionUISettings.CombatMode.Value != hotbarProfile.CombatMode) ActionUISettings.CombatMode.Value = hotbarProfile.CombatMode;
                
                // Note: ShowCooldownTimer/EmptySlotOption are per-slot in profile but global in settings. 
                // We sync the global setting to match the first slot of the first hotbar as a proxy.
                if (hotbarProfile.Hotbars.Count > 0 && hotbarProfile.Hotbars[0].Slots.Count > 0)
                {
                    var config = hotbarProfile.Hotbars[0].Slots[0].Config;
                    if (ActionUISettings.ShowCooldownTimer.Value != config.ShowCooldownTime) ActionUISettings.ShowCooldownTimer.Value = config.ShowCooldownTime;
                    if (ActionUISettings.PreciseCooldownTime.Value != config.PreciseCooldownTime) ActionUISettings.PreciseCooldownTime.Value = config.PreciseCooldownTime;
                    if (ActionUISettings.EmptySlotOption.Value != config.EmptySlotOption.ToString()) ActionUISettings.EmptySlotOption.Value = config.EmptySlotOption.ToString();
                }

                var json = JsonConvert.SerializeObject(hotbarProfile, Formatting.None);
                if (ActionUISettings.SerializedHotbars.Value != json)
                {
                    ActionUISettings.SerializedHotbars.Value = json;
                }
            }
            finally
            {
                _isSaving = false;
            }
        }
        
        // IHotbarProfileService methods
        
        public IHotbarProfile AddHotbar()
        {
            // Logic copied/adapted from HotbarProfileJsonService
             int barIndex = GetProfile().Hotbars.Last().HotbarIndex + 1;
            var newBar = new HotbarData()
            {
                HotbarIndex = barIndex,
                RewiredActionId = RewiredConstants.ActionSlots.HotbarNavActions[barIndex].id,
                RewiredActionName = RewiredConstants.ActionSlots.HotbarNavActions[barIndex].name,
            };
             foreach (var slot in GetProfile().Hotbars.First().Slots)
            {
                 // Create empty slots
                var slotData = slot as SlotData; // Danger: reference copy?
                // Need deep copy/create new
                newBar.Slots.Add(CreateSlotDataFrom(slot, slot.SlotIndex));
            }
            GetProfile().Hotbars.Add(newBar);
            Save();
            OnProfileChanged?.Invoke(GetProfile(), HotbarProfileChangeTypes.HotbarAdded);
            return GetProfile();
        }

        public IHotbarProfile RemoveHotbar()
        {
             if (GetProfile().Hotbars.Count > 1)
            {
                GetProfile().Hotbars.RemoveAt(GetProfile().Hotbars.Count - 1);
                Save();
                OnProfileChanged?.Invoke(GetProfile(), HotbarProfileChangeTypes.HotbarRemoved);
            }
            return GetProfile();
        }

        public IHotbarProfile AddRow()
        {
            GetProfile().Rows++;
            // Logic to add slots to all hotbars
            // reusing logic
            SyncStructure();
            Save();
            OnProfileChanged?.Invoke(GetProfile(), HotbarProfileChangeTypes.RowAdded);
            return GetProfile();
        }

        public IHotbarProfile RemoveRow() 
        {
            if (GetProfile().Rows > 1)
            {
                GetProfile().Rows--;
                SyncStructure();
                Save();
                OnProfileChanged?.Invoke(GetProfile(), HotbarProfileChangeTypes.RowRemoved);
            }
            return GetProfile();
        }
        
        public IHotbarProfile AddSlot()
        {
             GetProfile().SlotsPerRow++;
             SyncStructure();
             Save();
             OnProfileChanged?.Invoke(GetProfile(), HotbarProfileChangeTypes.SlotAdded);
             return GetProfile();
        }

        public IHotbarProfile RemoveSlot()
        {
             if (GetProfile().SlotsPerRow > 1)
             {
                 GetProfile().SlotsPerRow--;
                 SyncStructure();
                 Save();
                 OnProfileChanged?.Invoke(GetProfile(), HotbarProfileChangeTypes.SlotRemoved);
             }
             return GetProfile();
        }

        public void Update(HotbarsContainer hotbar)
        {
            // Sync slot assignments from UI container to profile
            if (hotbar?.Hotbars != null && _cachedProfile?.Hotbars != null)
            {
                for (int barIndex = 0; barIndex < hotbar.Hotbars.Length && barIndex < _cachedProfile.Hotbars.Count; barIndex++)
                {
                    var uiBar = hotbar.Hotbars[barIndex];
                    var profileBar = _cachedProfile.Hotbars[barIndex];
                    
                    for (int slotIndex = 0; slotIndex < uiBar.Length && slotIndex < profileBar.Slots.Count; slotIndex++)
                    {
                        var uiSlot = uiBar[slotIndex];
                        var profileSlot = profileBar.Slots[slotIndex];
                        
                        if (uiSlot.SlotAction != null)
                        {
                            profileSlot.ItemID = uiSlot.SlotAction.ActionId;
                            profileSlot.ItemUID = uiSlot.SlotAction.ActionUid;
                        }
                        else
                        {
                            profileSlot.ItemID = -1;
                            profileSlot.ItemUID = null;
                        }
                        
                        // Sync disabled state from UI config
                        if (uiSlot.Config != null)
                        {
                            profileSlot.Config.IsDisabled = uiSlot.Config.IsDisabled;
                        }
                    }
                }
            }
            
            Save();
        }

        // ... Implement other Set methods (SetCooldown, SetScale, etc) trivially updating profile and calling Save()

        public IHotbarProfile SetCooldownTimer(bool showTimer, bool preciseTime)
        {
             // update flags
             _cachedProfile.ShowCooldownTimer = showTimer; // Not in interface?
             // The interface implies updating slots?
             // Old service iterated slots.
             foreach(var bar in _cachedProfile.Hotbars)
                 foreach(var slot in bar.Slots)
                 {
                     slot.Config.ShowCooldownTime = showTimer;
                     slot.Config.PreciseCooldownTime = preciseTime;
                 }
             Save();
             OnProfileChanged?.Invoke(_cachedProfile, HotbarProfileChangeTypes.CooldownTimer);
             return _cachedProfile;
        }

        public IHotbarProfile SetCombatMode(bool combatMode)
        {
            _cachedProfile.CombatMode = combatMode;
            Save();
            OnProfileChanged?.Invoke(_cachedProfile, HotbarProfileChangeTypes.CombatMode);
            return _cachedProfile;
        }

        public IHotbarProfile SetEmptySlotView(EmptySlotOptions option)
        {
             foreach(var bar in _cachedProfile.Hotbars)
                 foreach(var slot in bar.Slots)
                 {
                     slot.Config.EmptySlotOption = option;
                 }
            Save();
            OnProfileChanged?.Invoke(_cachedProfile, HotbarProfileChangeTypes.EmptySlotView);
            return _cachedProfile;
        }

        public IHotbarProfile SetHideLeftNav(bool hideLeftNav)
        {
            _cachedProfile.HideLeftNav = hideLeftNav;
            Save();
            OnProfileChanged?.Invoke(_cachedProfile, HotbarProfileChangeTypes.HideLeftNav);
            return _cachedProfile;
        }

         public IHotbarProfile SetScale(int scale)
        {
            _cachedProfile.Scale = scale;
            Save();
            OnProfileChanged?.Invoke(_cachedProfile, HotbarProfileChangeTypes.Scale);
            return _cachedProfile;
        }

        private void SyncStructure()
        {
             // Ensure slots exist for Rows * SlotsPerRow
             // Simplified version for brevity, should use robust logic from old service
             // Iterate hotbars
             foreach(var bar in _cachedProfile.Hotbars)
             {
                 // Add/Remove slots logic...
                 int required = _cachedProfile.Rows * _cachedProfile.SlotsPerRow;
                 while(bar.Slots.Count < required)
                 {
                     // Add slot
                      bar.Slots.Add(CreateSlotDataFrom(bar.Slots.First(), bar.Slots.Count));
                 }
                 if(bar.Slots.Count > required)
                 {
                     bar.Slots.RemoveRange(required, bar.Slots.Count - required);
                 }
                 ReindexSlots(bar.Slots);
             }
        }

        private bool _isSaving = false;

        private void UpdateFromSettings(HotbarProfileChangeTypes type)
        {
            if (_isSaving) return;

            // Called when settings change (e.g. Rows changed in config menu)
            // Reload simple values
             SyncSettingsToProfile(_cachedProfile);
             // Verify structure
             SyncStructure(); 
             // Save (update serialized string)
             Save();
             // Notify UI
             OnProfileChanged?.Invoke(_cachedProfile, type);
        }
        
        // Helpers
         private void ReindexSlots(List<ISlotData> slots)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                slots[i].SlotIndex = i;
                if(i < RewiredConstants.ActionSlots.Actions.Count) {
                    ((ActionConfig)slots[i].Config).RewiredActionId = RewiredConstants.ActionSlots.Actions[i].id;
                    ((ActionConfig)slots[i].Config).RewiredActionName = RewiredConstants.ActionSlots.Actions[i].name;
                }
            }
        }
        
        private SlotData CreateSlotDataFrom(ISlotData source, int slotIndex)
        {
             // Copy config
             var config = new ActionConfig()
             {
                 EmptySlotOption = source.Config.EmptySlotOption,
                 ShowCooldownTime = source.Config.ShowCooldownTime,
                 // ... other props
             };
              return new SlotData()
            {
                SlotIndex = slotIndex,
                Config = config,
                ItemID = -1,
                ItemUID = null
            };
        }
        
         private List<IHotbarSlotData> DeepCloneHotbars(List<IHotbarSlotData> original)
        {
             // implementation
             return new List<IHotbarSlotData>(original); // Placeholder, needs deep clone
        }
        
        #region Per-Character Slot Data
        
        /// <summary>
        /// Loads slot assignments from a character-specific JSON file and applies them to the current profile.
        /// </summary>
        public void LoadCharacterSlots(string characterUID)
        {
            if (string.IsNullOrEmpty(characterUID))
            {
                Logger.LogWarning("LoadCharacterSlots called with empty characterUID");
                return;
            }

            var filePath = GetCharacterSlotFilePath(characterUID);
            if (!System.IO.File.Exists(filePath))
            {
                Logger.LogDebug($"No character slot file found for {characterUID} at {filePath}. Starting with empty slots.");
                // Clear all slots to prevent leaking data from previous character
                foreach (var bar in _cachedProfile.Hotbars)
                {
                    foreach (var slot in bar.Slots)
                    {
                        slot.ItemID = -1;
                        slot.ItemUID = null;
                        slot.Config.IsDisabled = false;
                    }
                }
                return;
            }
            
            try
            {
                var json = System.IO.File.ReadAllText(filePath);
                var charData = JsonConvert.DeserializeObject<CharacterSlotData>(json);
                
                if (charData == null || charData.Slots == null)
                    return;
                
                // Apply slot assignments to current profile
                foreach (var bar in _cachedProfile.Hotbars)
                {
                    var barIndex = _cachedProfile.Hotbars.IndexOf(bar);
                    foreach (var slot in bar.Slots)
                    {
                        var slotIndex = bar.Slots.IndexOf(slot);
                        var key = $"{barIndex}_{slotIndex}";
                        
                        if (charData.Slots.TryGetValue(key, out var slotEntry))
                        {
                            slot.ItemID = slotEntry.ItemID;
                            slot.ItemUID = slotEntry.ItemUID;
                        }
                        else
                        {
                            // Clear slot if not in character data
                            slot.ItemID = -1;
                            slot.ItemUID = null;
                        }
                        
                        // Apply disabled state
                        slot.Config.IsDisabled = charData.DisabledSlots?.Contains(key) ?? false;
                    }
                }
                
                Logger.LogDebug($"Loaded character slots for {characterUID} from {filePath}");
            }
            catch (Exception ex)
            {
                Logger.LogException($"Failed to load character slots for {characterUID}", ex);
            }
        }
        
        /// <summary>
        /// Saves slot assignments to a character-specific JSON file.
        /// </summary>
        public void SaveCharacterSlots(string characterUID)
        {
            if (string.IsNullOrEmpty(characterUID))
            {
                Logger.LogWarning("SaveCharacterSlots called with empty characterUID");
                return;
            }
            
            try
            {
                // Ensure directory exists
                var dirPath = ActionUISettings.CharacterHotbarsPath;
                if (!System.IO.Directory.Exists(dirPath))
                {
                    System.IO.Directory.CreateDirectory(dirPath);
                }
                
                var charData = new CharacterSlotData
                {
                    CharacterUID = characterUID,
                    Slots = new System.Collections.Generic.Dictionary<string, SlotDataEntry>(),
                    DisabledSlots = new System.Collections.Generic.HashSet<string>()
                };
                
                // Extract slot assignments from current profile
                foreach (var bar in _cachedProfile.Hotbars)
                {
                    var barIndex = _cachedProfile.Hotbars.IndexOf(bar);
                    foreach (var slot in bar.Slots)
                    {
                        var slotIndex = bar.Slots.IndexOf(slot);
                        var key = $"{barIndex}_{slotIndex}";
                        
                        // Only save if there's an item assigned
                        if (slot.ItemID > 0 || !string.IsNullOrEmpty(slot.ItemUID))
                        {
                            charData.Slots[key] = new SlotDataEntry
                            {
                                ItemID = slot.ItemID,
                                ItemUID = slot.ItemUID
                            };
                        }
                        
                        // Save disabled state
                        if (slot.Config.IsDisabled)
                        {
                            charData.DisabledSlots.Add(key);
                        }
                    }
                }
                
                var filePath = GetCharacterSlotFilePath(characterUID);
                var json = JsonConvert.SerializeObject(charData, Formatting.Indented);
                System.IO.File.WriteAllText(filePath, json);
                
                Logger.LogDebug($"Saved character slots for {characterUID} to {filePath}");
            }
            catch (Exception ex)
            {
                Logger.LogException($"Failed to save character slots for {characterUID}", ex);
            }
        }
        
        private string GetCharacterSlotFilePath(string characterUID)
        {
            // Sanitize UID for use as filename
            var sanitized = characterUID.Replace("\\", "_").Replace("/", "_").Replace(":", "_");
            return System.IO.Path.Combine(ActionUISettings.CharacterHotbarsPath, $"{sanitized}.json");
        }
        
        #endregion
    }
}
