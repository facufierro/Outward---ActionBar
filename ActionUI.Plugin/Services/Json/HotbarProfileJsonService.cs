using ModifAmorphic.Outward.ActionUI.Config;
using ModifAmorphic.Outward.ActionUI.DataModels;
using ModifAmorphic.Outward.ActionUI.Extensions;
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
using System.IO;
using System.Linq;

namespace ModifAmorphic.Outward.ActionUI.Services
{

    public class HotbarProfileJsonService : IHotbarProfileService, IDisposable, ISavableProfile
    {
        Func<IModifLogger> _getLogger;
        private IModifLogger Logger => _getLogger.Invoke();

        public string HotbarsConfigFile = "Hotbars.json";

        private ProfileService _profileService;

        private HotbarProfileData _hotbarProfile;
        private bool disposedValue;

        public event Action<IHotbarProfile, HotbarProfileChangeTypes> OnProfileChanged;

        public HotbarProfileJsonService(ProfileService profileService, Func<IModifLogger> getLogger)
        {
            (_profileService, _getLogger) = (profileService, getLogger);
            profileService.OnActiveProfileSwitching += TrySaveCurrentProfile;
            profileService.OnActiveProfileSwitched += TryRefreshCachedProfile;
        }

        private void TrySaveCurrentProfile(IActionUIProfile profile)
        {
            try
            {
                Save();
            }
            catch (Exception ex)
            {
                Logger.LogException($"Failed to save current Hotbar data to profile '{profile?.Name}'.", ex);
            }
        }

        private void TryRefreshCachedProfile(IActionUIProfile profile)
        {
            try
            {
                RefreshCachedProfile(profile, true);
            }
            catch (Exception ex)
            {
                Logger.LogException($"Failed refresh of current Hotbar data for profile '{profile?.Name}'.", ex);
            }
        }

        private void RefreshCachedProfile(IActionUIProfile obj, bool suppressChangedEvent = false)
        {
            _hotbarProfile = GetProfileData();
            if (!suppressChangedEvent)
                OnProfileChanged?.TryInvoke(_hotbarProfile, HotbarProfileChangeTypes.ProfileRefreshed);
        }

        public IHotbarProfile GetProfile()
        {
            if (_hotbarProfile != null)
                return _hotbarProfile;

            _hotbarProfile = GetProfileData();
            return _hotbarProfile;
        }

        public void Save() => Save(GetProfile());

        public void SaveNew(IHotbarProfile hotbarProfile)
        {
            Save(hotbarProfile);
            _hotbarProfile = null;
        }

        private string GetGlobalSettingsPath()
        {
            // Use global settings folder so hotbar settings persist across all characters
            if (!System.IO.Directory.Exists(ActionUISettings.GlobalSettingsPath))
                System.IO.Directory.CreateDirectory(ActionUISettings.GlobalSettingsPath);
            return ActionUISettings.GlobalSettingsPath;
        }

        private void Save(IHotbarProfile hotbarProfile)
        {
            // Sync hotbar profile settings back to BepInEx config (global settings)
            Logger.LogInfo($"Syncing Hotbar profile to BepInEx config.");
            
            if (ActionUIConfig.Rows != null && ActionUIConfig.Rows.Value != hotbarProfile.Rows)
                ActionUIConfig.Rows.Value = hotbarProfile.Rows;
            if (ActionUIConfig.SlotsPerRow != null && ActionUIConfig.SlotsPerRow.Value != hotbarProfile.SlotsPerRow)
                ActionUIConfig.SlotsPerRow.Value = hotbarProfile.SlotsPerRow;
            if (ActionUIConfig.Scale != null && ActionUIConfig.Scale.Value != hotbarProfile.Scale)
                ActionUIConfig.Scale.Value = hotbarProfile.Scale;
            if (ActionUIConfig.HideLeftNav != null && ActionUIConfig.HideLeftNav.Value != hotbarProfile.HideLeftNav)
                ActionUIConfig.HideLeftNav.Value = hotbarProfile.HideLeftNav;
            if (ActionUIConfig.CombatMode != null && ActionUIConfig.CombatMode.Value != hotbarProfile.CombatMode)
                ActionUIConfig.CombatMode.Value = hotbarProfile.CombatMode;
        }

        public void Update(HotbarsContainer hotbar)
        {
            GetProfile().Rows = hotbar.Controller.GetRowCount();
            GetProfile().SlotsPerRow = hotbar.Controller.GetActionSlotsPerRow();
            GetProfile().Hotbars = hotbar.ToHotbarSlotData(GetProfile().Hotbars.Cast<HotbarData>().ToArray());

            Save();
        }

        public IHotbarProfile AddHotbar()
        {
            int barIndex = GetProfile().Hotbars.Last().HotbarIndex + 1;

            var profileClone = GetProfileData();

            var newBar = new HotbarData()
            {
                HotbarIndex = barIndex,
                RewiredActionId = RewiredConstants.ActionSlots.HotbarNavActions[barIndex].id,
                RewiredActionName = RewiredConstants.ActionSlots.HotbarNavActions[barIndex].name,
            };

            Logger.LogDebug($"Adding new hotbar with index of {newBar.HotbarIndex}");

            foreach (var slot in profileClone.Hotbars.First().Slots)
            {
                var slotData = slot as SlotData;
                slotData.ItemUID = null;
                slotData.ItemID = -1;
                newBar.Slots.Add(slotData);
                Logger.LogDebug($"Added slot to new hotbar {newBar.HotbarIndex}.  Slot config: \n\t" +
                    $"RewiredActionId: {((ActionConfig)slotData.Config).RewiredActionId}\n\t" +
                    $"RewiredActionName: {((ActionConfig)slotData.Config).RewiredActionName}\n\t");
            }

            GetProfile().Hotbars.Add(newBar);
            Save();
            OnProfileChanged?.TryInvoke(GetProfile(), HotbarProfileChangeTypes.HotbarAdded);
            return GetProfile();
        }

        public IHotbarProfile RemoveHotbar()
        {
            if (GetProfile().Hotbars.Count > 1)
            {
                GetProfile().Hotbars.RemoveAt(GetProfile().Hotbars.Count - 1);
                Save();
                OnProfileChanged?.TryInvoke(GetProfile(), HotbarProfileChangeTypes.HotbarRemoved);
            }
            return GetProfile();
        }

        public IHotbarProfile AddRow()
        {
            GetProfile().Rows = GetProfile().Rows + 1;

            for (int b = 0; b < GetProfile().Hotbars.Count; b++)
            {
                for (int s = 0; s < GetProfile().SlotsPerRow; s++)
                {
                    int slotIndex = s + GetProfile().SlotsPerRow * (GetProfile().Rows - 1);

                    GetProfile().Hotbars[b].Slots.Add(
                        CreateSlotDataFrom(GetProfile().Hotbars[b].Slots[s], slotIndex));
                }
            }

            Save();
            OnProfileChanged?.TryInvoke(GetProfile(), HotbarProfileChangeTypes.RowAdded);
            return GetProfile();
        }
        public IHotbarProfile RemoveRow()
        {
            if (GetProfile().Rows <= 1)
                return GetProfile();

            GetProfile().Rows--;

            for (int b = 0; b < GetProfile().Hotbars.Count; b++)
            {
                int removeFrom = GetProfile().SlotsPerRow * GetProfile().Rows;
                int removeAmount = GetProfile().Hotbars[b].Slots.Count - removeFrom;

                Logger.LogDebug($"Reducing hotbar {b}'s rows to {GetProfile().Rows}. Removing {removeAmount} slots starting with slot index {removeFrom}.\n" +
                    $"\tremoveFrom = {GetProfile().SlotsPerRow} * {GetProfile().Rows}\n" +
                    $"\tremoveAmount = {GetProfile().Hotbars[b].Slots.Count} - {removeFrom}");

                GetProfile().Hotbars[b].Slots.RemoveRange(removeFrom, removeAmount);
            }

            Save();
            OnProfileChanged?.TryInvoke(GetProfile(), HotbarProfileChangeTypes.RowRemoved);
            return GetProfile();
        }

        public IHotbarProfile AddSlot()
        {
            for (int b = 0; b < GetProfile().Hotbars.Count; b++)
            {
                int slotIndex = GetProfile().Hotbars[b].Slots.Count;
                GetProfile().Hotbars[b].Slots.Add(
                        CreateSlotDataFrom(GetProfile().Hotbars[b].Slots.First(), slotIndex));

                var lastIndex = slotIndex - GetProfile().SlotsPerRow;

                for (int s = lastIndex; s > 0; s = s - GetProfile().SlotsPerRow)
                {
                    GetProfile().Hotbars[b].Slots.Insert(s,
                        CreateSlotDataFrom(GetProfile().Hotbars[b].Slots.First(), s));
                }
                ReindexSlots(GetProfile().Hotbars[b].Slots);
            }

            GetProfile().SlotsPerRow++;
            Save();
            OnProfileChanged?.TryInvoke(GetProfile(), HotbarProfileChangeTypes.SlotAdded);
            return GetProfile();
        }

        public IHotbarProfile RemoveSlot()
        {
            if (GetProfile().SlotsPerRow <= 1)
                return GetProfile();

            for (int b = 0; b < GetProfile().Hotbars.Count; b++)
            {
                for (int r = GetProfile().Rows; r > 0; r--)
                {
                    int lastSlotInRow = r * GetProfile().SlotsPerRow - 1;
                    GetProfile().Hotbars[b].Slots.RemoveAt(lastSlotInRow);
                }
                ReindexSlots(GetProfile().Hotbars[b].Slots);
            }

            GetProfile().SlotsPerRow--;
            Save();
            OnProfileChanged?.TryInvoke(GetProfile(), HotbarProfileChangeTypes.SlotRemoved);
            return GetProfile();
        }

        public IHotbarProfile SetCooldownTimer(bool showTimer, bool preciseTime)
        {
            bool profileChanged = false;

            foreach (var bar in GetProfile().Hotbars)
            {
                foreach (var slot in bar.Slots)
                {
                    if (slot.Config.ShowCooldownTime != showTimer || slot.Config.PreciseCooldownTime != preciseTime)
                    {
                        profileChanged = true;
                        slot.Config.ShowCooldownTime = showTimer;
                        slot.Config.PreciseCooldownTime = preciseTime;
                    }
                }
            }

            if (profileChanged)
            {
                Save();
                OnProfileChanged?.TryInvoke(GetProfile(), HotbarProfileChangeTypes.CooldownTimer);
            }

            return GetProfile();
        }

        public IHotbarProfile SetHideLeftNav(bool hideLeftNav)
        {
            if (GetProfile().HideLeftNav != hideLeftNav)
            {
                GetProfile().HideLeftNav = hideLeftNav;
                Save();
                OnProfileChanged?.TryInvoke(GetProfile(), HotbarProfileChangeTypes.HideLeftNav);
            }

            return GetProfile();
        }

        public IHotbarProfile SetCombatMode(bool combatMode)
        {
            if (GetProfile().CombatMode != combatMode)
            {
                GetProfile().CombatMode = combatMode;
                Save();
                OnProfileChanged?.TryInvoke(GetProfile(), HotbarProfileChangeTypes.CombatMode);
            }

            return GetProfile();
        }

        public IHotbarProfile SetEmptySlotView(EmptySlotOptions option)
        {
            bool profileChanged = false;

            foreach (var bar in GetProfile().Hotbars)
            {
                foreach (var slot in bar.Slots)
                {
                    if (slot.Config.EmptySlotOption != option)
                    {
                        profileChanged = true;
                        slot.Config.EmptySlotOption = option;
                    }
                }
            }

            if (profileChanged)
            {
                Save();
                OnProfileChanged?.TryInvoke(GetProfile(), HotbarProfileChangeTypes.EmptySlotView);
            }
            return GetProfile();
        }

        public IHotbarProfile SetScale(int scale)
        {
            if (GetProfile().Scale != scale)
            {
                GetProfile().Scale = scale;
                Save();
                OnProfileChanged?.TryInvoke(GetProfile(), HotbarProfileChangeTypes.Scale);
            }

            return GetProfile();
        }

        public IHotbarProfile UpdateDimensions(int rows, int slotsPerRow)
        {
            var profile = GetProfile();
            if (profile.Rows == rows && profile.SlotsPerRow == slotsPerRow)
                return profile;

            Logger.LogDebug($"UpdateDimensions: Resizing from {profile.Rows}x{profile.SlotsPerRow} to {rows}x{slotsPerRow}");

            while (profile.Rows < rows) AddRow();
            while (profile.Rows > rows) RemoveRow();
            while (profile.SlotsPerRow < slotsPerRow) AddSlot();
            while (profile.SlotsPerRow > slotsPerRow) RemoveSlot();

            return GetProfile();
        }

        private void ReindexSlots(List<ISlotData> slots)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                slots[i].SlotIndex = i;
                ((ActionConfig)slots[i].Config).RewiredActionId = RewiredConstants.ActionSlots.Actions[i].id;
                ((ActionConfig)slots[i].Config).RewiredActionName = RewiredConstants.ActionSlots.Actions[i].name;
            }
        }

        private SlotData CreateSlotDataFrom(ISlotData source, int slotIndex)
        {
            var config = new ActionConfig()
            {
                EmptySlotOption = source.Config.EmptySlotOption,
                ShowZeroStackAmount = source.Config.ShowZeroStackAmount,
                PreciseCooldownTime = source.Config.PreciseCooldownTime,
                ShowCooldownTime = source.Config.ShowCooldownTime,
            };

            config.RewiredActionName = RewiredConstants.ActionSlots.Actions[slotIndex].name;
            config.RewiredActionId = RewiredConstants.ActionSlots.Actions[slotIndex].id;

            return new SlotData()
            {
                SlotIndex = slotIndex,
                Config = config,
                ItemID = -1,
                ItemUID = null
            };
        }

        private HotbarProfileData GetProfileData()
        {
            // Create profile from BepInEx config values (global settings)
            Logger.LogDebug($"Creating Hotbar profile from BepInEx config values.");
            
            // Start with default profile as base (contains default slot configuration)
            // Use defaults for Rows/SlotsPerRow initially to match the cloned list, then resize.
            var profile = new HotbarProfileData()
            {
                Rows = HotbarSettings.DefaulHotbarProfile.Rows,
                SlotsPerRow = HotbarSettings.DefaulHotbarProfile.SlotsPerRow,
                Scale = ActionUIConfig.Scale?.Value ?? 100,
                HideLeftNav = ActionUIConfig.HideLeftNav?.Value ?? false,
                CombatMode = ActionUIConfig.CombatMode?.Value ?? true,
                Hotbars = DeepCloneHotbars(HotbarSettings.DefaulHotbarProfile.Hotbars),
                NextRewiredActionId = RewiredConstants.ActionSlots.NextHotbarAction.id,
                NextRewiredActionName = RewiredConstants.ActionSlots.NextHotbarAction.name,
                PrevRewiredActionId = RewiredConstants.ActionSlots.PreviousHotbarAction.id,
                PrevRewiredActionName = RewiredConstants.ActionSlots.PreviousHotbarAction.name,
                NextRewiredAxisActionName = RewiredConstants.ActionSlots.NextHotbarAxisAction.name,
                NextRewiredAxisActionId = RewiredConstants.ActionSlots.NextHotbarAxisAction.id,
                PrevRewiredAxisActionName = RewiredConstants.ActionSlots.PreviousHotbarAxisAction.name,
                PrevRewiredAxisActionId = RewiredConstants.ActionSlots.PreviousHotbarAxisAction.id,
            };

            // Temporarily set the internal profile so UpdateDimensions can access it via GetProfile()
            _hotbarProfile = profile;

            // Resize the list to match the saved config
            int targetRows = ActionUIConfig.Rows?.Value ?? 1;
            int targetSlots = ActionUIConfig.SlotsPerRow?.Value ?? 11;
            
            if (targetRows != profile.Rows || targetSlots != profile.SlotsPerRow)
            {
                Logger.LogDebug($"GetProfileData: Resizing new profile from default {profile.Rows}x{profile.SlotsPerRow} to config {targetRows}x{targetSlots}");
                UpdateDimensions(targetRows, targetSlots);
            }
            
            return _hotbarProfile; // return the updated profile
        }

        private List<IHotbarSlotData> DeepCloneHotbars(List<IHotbarSlotData> original)
        {
            var clone = new List<IHotbarSlotData>();
            foreach (var bar in original)
            {
                var barData = bar as HotbarData;
                var newBar = new HotbarData()
                {
                    HotbarIndex = barData.HotbarIndex,
                    RewiredActionId = barData.RewiredActionId,
                    RewiredActionName = barData.RewiredActionName,
                    HotbarHotkey = barData.HotbarHotkey,
                    Slots = new List<ISlotData>()
                };

                foreach (var slot in barData.Slots)
                {
                    // CreateSlotDataFrom clones the slot correctly
                    newBar.Slots.Add(CreateSlotDataFrom(slot, slot.SlotIndex));
                }
                clone.Add(newBar);
            }
            return clone;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (_profileService != null)
                    {
                        _profileService.OnActiveProfileSwitching -= TrySaveCurrentProfile;
                        _profileService.OnActiveProfileSwitched -= TryRefreshCachedProfile;
                    }
                }
                _hotbarProfile = null;
                _profileService = null;
                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~HotbarProfileJsonService()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
