using ModifAmorphic.Outward.ActionUI.DataModels;
using ModifAmorphic.Outward.ActionUI.Models;
using ModifAmorphic.Outward.ActionUI.Monobehaviours;
using ModifAmorphic.Outward.ActionUI.Patches;
using ModifAmorphic.Outward.ActionUI.Settings;
using ModifAmorphic.Outward.Coroutines;
using ModifAmorphic.Outward.Extensions;
using ModifAmorphic.Outward.Logging;
using ModifAmorphic.Outward.Unity.ActionMenus;
using ModifAmorphic.Outward.Unity.ActionUI.Data;
using Rewired;
using System;
using System.Linq;
using UnityEngine;

namespace ModifAmorphic.Outward.ActionUI.Services
{
    internal class HotbarService : IDisposable
    {

        private IModifLogger Logger => _getLogger.Invoke();
        private readonly Func<IModifLogger> _getLogger;

        private readonly HotbarsContainer _hotbars;
        private readonly Player _player;
        private readonly Character _character;
        private readonly CharacterUI _characterUI;
        private readonly IHotbarProfileService _hotbarProfileService;
        private readonly IActionUIProfileService _profileService;
        private readonly SlotDataService _slotData;

        private readonly LevelCoroutines _levelCoroutines;
        private bool _saveDisabled;
        private bool _isProfileInit;
        private bool _isStarted = false;

        private bool disposedValue;

        public HotbarService(HotbarsContainer hotbarsContainer, Player player, Character character, IHotbarProfileService hotbarProfileService, IActionUIProfileService profileService, SlotDataService slotData, LevelCoroutines levelCoroutines, Func<IModifLogger> getLogger)
        {
            if (hotbarsContainer == null)
                throw new ArgumentNullException(nameof(hotbarsContainer));
            if (character == null)
                throw new ArgumentNullException(nameof(character));
            if (hotbarProfileService == null)
                throw new ArgumentNullException(nameof(hotbarProfileService));
            if (profileService == null)
                throw new ArgumentNullException(nameof(profileService));
            if (slotData == null)
                throw new ArgumentNullException(nameof(slotData));

            _hotbars = hotbarsContainer;

            _player = player;
            _character = character;
            _characterUI = character.CharacterUI;
            _hotbarProfileService = hotbarProfileService;
            _profileService = profileService;
            _slotData = slotData;
            _levelCoroutines = levelCoroutines;
            _getLogger = getLogger;

            QuickSlotPanelPatches.StartInitAfter += DisableKeyboardQuickslots;
            SkillMenuPatches.AfterOnSectionSelected += SetSkillsMovable;
            ItemDisplayDropGroundPatches.TryGetIsDropValids.Add(_player.id, TryGetIsDropValid);
            _hotbars.OnAwake += StartNextFrame;
            if (_hotbars.IsAwake)
                StartNextFrame();

        }

        private void StartNextFrame()
        {
            try
            {
                _levelCoroutines.DoNextFrame(() => Start());
            }
            catch (Exception ex)
            {
                Logger.LogException($"Failed to start {nameof(HotbarService)} coroutine {nameof(Start)}.", ex);
            }
        }
        public void Start()
        {
            try
            {
                _isStarted = true;
                _saveDisabled = true;

                SwapCanvasGroup();

                var profile = GetOrCreateActiveProfile();
                
                // Load character-specific slot assignments
                if (_hotbarProfileService is GlobalHotbarService globalService)
                {
                    globalService.LoadCharacterSlots(_character.UID);
                }
                
                TryConfigureHotbars(profile, HotbarProfileChangeTypes.ProfileRefreshed);
                _hotbars.ClearChanges();
                _hotbarProfileService.OnProfileChanged += TryConfigureHotbars;
                _hotbars.OnHasChanges.AddListener(Save);
                CharacterUIPatches.AfterRefreshHUDVisibility += ShowHideHotbars;

                AssignSlotActions();
                AssignSlotActions();
                ShowHideHotbars(_characterUI);
                
                // Initialize Position Coupling
                InitializePositionConfig();
            }
            catch (Exception ex)
            {
                Logger.LogException($"Failed to start {nameof(HotbarService)}.", ex);
            }
        }

        private void ShowHideHotbars(CharacterUI characterui)
        {
            if (characterui.TargetCharacter.UID != _character.UID)
                return;
            
            var hudAlpha = OptionManager.Instance.GetHudVisibility(_character.OwnerPlayerSys.PlayerID).ToInt();
            _hotbars.ActionBarsCanvas.GetComponent<CanvasGroup>().alpha = hudAlpha;
        }

        private void SetSkillsMovable(ItemListDisplay itemListDisplay)
        {
            if (!_profileService.GetActiveProfile()?.ActionSlotsEnabled ?? false)
                return;

            var displays = itemListDisplay.GetComponentsInChildren<ItemDisplay>();
            for (int i = 0; i < displays.Length; i++)
            {
                displays[i].Movable = true;
            }
        }

        private bool TryGetIsDropValid(ItemDisplay draggedDisplay, Character character, out bool result)
        {
            result = false;
            if (draggedDisplay?.RefItem == null || !(draggedDisplay.RefItem is Skill skill))
                return false;

            Logger.LogDebug($"TryGetIsDropValid:: Blocking drop of skill {skill.name} to DropPanel.");
            return true;
        }

        private bool _isConfiguring = false;

        public void TryConfigureHotbars(IHotbarProfile profile)
        {
            try
            {
                if (!_hotbars.IsAwake || !_isStarted || _character?.Inventory == null)
                    return;

                _isConfiguring = true; // Block saves
                _saveDisabled = true;

                SetProfileHotkeys(profile);

                _hotbars.Controller.ConfigureHotbars(profile);

                // Add ActionSlotDropper immediately to ensure slots are always interactive, regardless of assignment success
                foreach(var bar in _hotbars.Hotbars)
                {
                    if (bar == null) continue;
                    foreach(var slot in bar)
                    {
                        if (slot != null && slot.ActionButton != null)
                        {
                             slot.ActionButton.gameObject.GetOrAddComponent<ActionSlotDropper>().SetLogger(_getLogger);
                        }
                    }
                }

                if (profile.HideLeftNav)
                    _hotbars.LeftHotbarNav.Hide();
                else
                    _hotbars.LeftHotbarNav.Show();

                if (_isProfileInit)
                {
                    AssignSlotActions(profile);
                }

                ScaleHotbars(profile.Scale);
            }
            catch (Exception ex)
            {
                Logger.LogException($"Failed to configure Hotbars.", ex);
            }
            finally
            {
                _isConfiguring = false;
                _saveDisabled = false;
            }
        }
        private void TryConfigureHotbars(IHotbarProfile profile, HotbarProfileChangeTypes changeType)
        {
            try
            {
                if (changeType == HotbarProfileChangeTypes.Scale)
                    ScaleHotbars(profile.Scale);
                else
                    TryConfigureHotbars(profile);
            }
            catch (Exception ex)
            {
                Logger.LogException($"Failed to configure Hotbars.", ex);
            }
        }

        public Guid InstanceID { get; } = Guid.NewGuid();
        private void Save()
        {
            try
            {
                if (_isConfiguring) return; // STRICTLY BLOCK SAVES DURING CONFIG

                if (_hotbars.HasChanges && !_saveDisabled)
                {
                    var profile = GetOrCreateActiveProfile();
                    Logger.LogDebug($"{nameof(HotbarService)}_{InstanceID}: Hotbar changes detected. Saving.");
                    _hotbarProfileService.Update(_hotbars);
                    
                    // Save character-specific slot assignments
                    if (_hotbarProfileService is GlobalHotbarService globalService)
                    {
                        globalService.SaveCharacterSlots(_character.UID);
                    }
                    
                    _hotbars.ClearChanges();
                }
            }
            catch (Exception ex)
            {
                Logger.LogException($"Failed to Save Hotbar changes.", ex);
            }
        }

        private void ScaleHotbars(int scaleAmount)
        {
            if (_hotbars == null)
                return;

            float scale = (float)scaleAmount / 100f;
            _hotbars.transform.localScale = new Vector3(scale, scale, 1f);
        }

        private void SwapCanvasGroup()
        {
            try
            {
                var controllerSwitcher = _characterUI.GetComponentsInChildren<QuickSlotControllerSwitcher>().FirstOrDefault(q => q.name == "QuickSlot");
                if (controllerSwitcher == null)
                {
                    Logger.LogWarning("[HotbarService] QuickSlotControllerSwitcher not found!");
                    return;
                }
                
                // Get vanilla bars
                var keyboardGroup = controllerSwitcher.GetPrivateField<QuickSlotControllerSwitcher, CanvasGroup>("m_keyboardQuickSlots");
                var gamepadGroup = controllerSwitcher.GetPrivateField<QuickSlotControllerSwitcher, CanvasGroup>("m_gamepadQuickSlots");

                // Disable vanilla keyboard quickslots (we replace them)
                if (_hotbars.VanillaSuppressionTargets == null)
                    _hotbars.VanillaSuppressionTargets = new System.Collections.Generic.List<GameObject>();
                _hotbars.VanillaSuppressionTargets.Clear();

                if (keyboardGroup != null)
                {
                    keyboardGroup.gameObject.SetActive(false);
                    keyboardGroup.alpha = 0f;
                    _hotbars.VanillaSuppressionTargets.Add(keyboardGroup.gameObject);
                }

                // Create Dummy to satisfy ControllerSwitcher
                var dummyObj = new GameObject("DummyKeyboardSlots");
                dummyObj.transform.SetParent(controllerSwitcher.transform);
                var dummyGroup = dummyObj.AddComponent<CanvasGroup>();
                dummyGroup.alpha = 0f; // Invisible
                // Make sure it doesn't block raycasts
                dummyGroup.blocksRaycasts = false;
                dummyGroup.interactable = false;

                // Swap references so game controls dummy instead of real keyboard slots
                controllerSwitcher.SetPrivateField("m_keyboardQuickSlots", dummyGroup);

                // Setup Dynamic Positioning Targets
                if (_hotbars.VanillaOverlayTargets == null)
                    _hotbars.VanillaOverlayTargets = new System.Collections.Generic.List<CanvasGroup>();
                
                _hotbars.VanillaOverlayTargets.Clear();
                // Only offset for Gamepad slots, not for Keyboard (which is now Dummy/Hidden)
                if (gamepadGroup != null) _hotbars.VanillaOverlayTargets.Add(gamepadGroup);
                
                Logger.LogDebug($"[HotbarService] SwapCanvasGroup Complete. KeyboardGroup found: {keyboardGroup != null}, GamepadGroup found: {gamepadGroup != null}. Swapped to Dummy.");
            }
            catch (Exception ex)
            {
                Logger.LogException("Failed to populate VanillaOverlayTargets or disable QuickSlots.", ex);
            }

        }

        public void DisableKeyboardQuickslots(KeyboardQuickSlotPanel keyboard)
        {
            if (keyboard != null && keyboard.gameObject.activeSelf)
            {
                Logger.LogDebug($"Disabling Keyboard QuickSlots for RewiredID {keyboard?.CharacterUI?.RewiredID}.");
                keyboard.gameObject.SetActive(false);
            }
        }


        private IHotbarProfile GetOrCreateActiveProfile()
        {
            var activeProfile = _profileService.GetActiveProfile();

            var hotbarProfile = _hotbarProfileService.GetProfile();
            //if (hotbarProfile == null)
            //    _hotbarProfileService.SaveNew(HotbarSettings.DefaulHotbarProfile);

            Logger.LogDebug($"Got or Created Active Profile '{activeProfile.Name}'");
            return hotbarProfile;
        }

        public void AssignSlotActions() => AssignSlotActions(GetOrCreateActiveProfile());

        public void AssignSlotActions(IHotbarProfile profile)
        {
            Logger.LogDebug($"{nameof(HotbarService)}_{InstanceID}: Assigning Slot Actions.");
            
            //_characterUI.ShowMenu(CharacterUI.MenuScreens.Inventory);
            //_characterUI.HideMenu(CharacterUI.MenuScreens.Inventory);

            _saveDisabled = true;
            _isConfiguring = true; // Ensure blocked
            try
            {
                for (int hb = 0; hb < profile.Hotbars.Count; hb++)
                {
                    for (int s = 0; s < profile.Hotbars[hb].Slots.Count; s++)
                    {
                        try
                        {
                            var slotData = profile.Hotbars[hb].Slots[s] as SlotData;
                            var actionSlot = _hotbars.Controller.GetActionSlots()[hb][s];
                            
                            bool assigned = false;
                            
                            // Validate slotData and actionSlot
                            if (slotData != null && actionSlot != null)
                            {
                                if (_slotData.TryGetItemSlotAction(slotData, profile.CombatMode, out var slotAction))
                                {
                                    try
                                    {
                                        actionSlot.Controller.AssignSlotAction(slotAction);
                                        assigned = true;
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.LogException($"Failed to assign slot action '{slotAction?.DisplayName}' to Bar {hb}, Slot Index {s}.", ex);
                                    }
                                }
                            }

                            if (!assigned)
                            {
                                // Ensure it's visually empty if we failed to assign something
                                actionSlot.Controller.AssignEmptyAction();
                            }
                            
                            // Dropper is now added in ActionSlot.Start(), so we don't need to do it here
                        }
                        catch (Exception ex)
                        {
                            Logger.LogException($"Failed to assign action to slot {hb}_{s}.", ex);
                        }
                    }
                }
                
                SetProfileHotkeys(profile);
                
                if (!_isProfileInit)
                {
                    _isProfileInit = true;
                }
                
                _hotbars.Controller.Refresh();
            }
            finally
            {
                Logger.LogDebug($"Clearing Hotbar Change Flag and enabling save.");
                _hotbars.ClearChanges();
                _saveDisabled = false;
                _isConfiguring = false;
            }
        }
        private void SetProfileHotkeys(IHotbarProfile profile)
        {
            var keyMap = _player.controllers.maps.GetMap<KeyboardMap>(0, RewiredConstants.ActionSlots.CategoryMapId, 0);
            var mouseMap = _player.controllers.maps.GetMap<MouseMap>(0, RewiredConstants.ActionSlots.CategoryMapId, 0);

            if (keyMap == null || mouseMap == null)
            {
                Logger.LogWarning($"SetProfileHotkeys: Controller maps not found for player {_player.id}. Skipping hotkey configuration. (KeyMap: {keyMap != null}, MouseMap: {mouseMap != null})");
                return;
            }

            var profileData = (HotbarProfileData)profile;
            profileData.NextHotkey = keyMap.ButtonMaps.FirstOrDefault(m => m.actionId == profileData.NextRewiredActionId)?.elementIdentifierName;
            if (string.IsNullOrWhiteSpace(profileData.NextHotkey))
                profileData.NextHotkey = mouseMap.ButtonMaps.FirstOrDefault(m => m.actionId == profileData.NextRewiredActionId)?.elementIdentifierName;
            if (string.IsNullOrWhiteSpace(profileData.NextHotkey) && mouseMap.AllMaps.Any(m => m.actionId == profileData.NextRewiredAxisActionId))
                profileData.NextHotkey = "Wheel+";

            profileData.PrevHotkey = keyMap.ButtonMaps.FirstOrDefault(m => m.actionId == profileData.PrevRewiredActionId)?.elementIdentifierName;
            if (string.IsNullOrWhiteSpace(profileData.PrevHotkey))
                profileData.PrevHotkey = mouseMap.ButtonMaps.FirstOrDefault(m => m.actionId == profileData.PrevRewiredActionId)?.elementIdentifierName;
            if (string.IsNullOrWhiteSpace(profileData.PrevHotkey) && mouseMap.AllMaps.Any(m => m.actionId == profileData.PrevRewiredAxisActionId))
                profileData.PrevHotkey = "Wheel-";

            foreach (HotbarData bar in profileData.Hotbars)
            {
                bar.HotbarHotkey = keyMap.ButtonMaps.FirstOrDefault(m => m.actionId == bar.RewiredActionId)?.elementIdentifierName;
                foreach (var slot in bar.Slots)
                {
                    var config = ((ActionConfig)slot.Config);
                    var eleMap = keyMap.ButtonMaps.FirstOrDefault(m => m.actionId == config.RewiredActionId);

                    if (eleMap != null)
                        slot.Config.HotkeyText = eleMap.elementIdentifierName;
                    else
                        slot.Config.HotkeyText = string.Empty;
                }
            }

            foreach (HotbarData bar in profileData.Hotbars)
            {
                var buttonMap = mouseMap.ButtonMaps.FirstOrDefault(m => m.actionId == bar.RewiredActionId);
                if (buttonMap != null)
                    bar.HotbarHotkey = ControllerMapService.MouseButtonElementIds[buttonMap.elementIdentifierId].DisplayName;
                foreach (var slot in bar.Slots)
                {
                    var config = ((ActionConfig)slot.Config);
                    var eleMap = mouseMap.ButtonMaps.FirstOrDefault(m => m.actionId == config.RewiredActionId);

                    if (eleMap != null)
                    {
                        slot.Config.HotkeyText = ControllerMapService.MouseButtonElementIds[eleMap.elementIdentifierId].DisplayName;
                    }
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Logger.LogDebug($"Disposing of {nameof(HotbarService)} instance '{InstanceID}'. Unsubscribing to events.");

                    QuickSlotPanelPatches.StartInitAfter -= DisableKeyboardQuickslots;
                    SkillMenuPatches.AfterOnSectionSelected -= SetSkillsMovable;

                    if (ItemDisplayDropGroundPatches.TryGetIsDropValids.ContainsKey(_player.id))
                        ItemDisplayDropGroundPatches.TryGetIsDropValids.Remove(_player.id);

                    if (_hotbars != null)
                    {
                        _hotbars.OnHasChanges.RemoveListener(Save);
                        _hotbars.OnAwake -= StartNextFrame;
                    }
                    if (_hotbarProfileService != null)
                        _hotbarProfileService.OnProfileChanged -= TryConfigureHotbars;
                        
                    // Unsubscribe from Position events
                    ActionUISettings.HotbarPositionX.SettingChanged -= OnHotbarPositionConfigChanged;
                    ActionUISettings.HotbarPositionY.SettingChanged -= OnHotbarPositionConfigChanged;
                    
                    if (_hotbars != null)
                    {
                        var posUI = _hotbars.GetComponent<PositionableUI>();
                        if (posUI != null)
                        {
                            posUI.UIElementMoved.RemoveListener(OnHotbarUIMoved);
                        }
                    }
                }

                disposedValue = true;
            }
        }

        private void InitializePositionConfig()
        {
            var posUI = _hotbars.GetComponent<PositionableUI>();
            if (posUI != null)
            {
                 // Initial sync from config
                 // Negate Y because Config Range is [-2160, 0] (0=Right=Bottom), but UI expects Positive Y for Up
                 posUI.SetPosition(ActionUISettings.HotbarPositionX.Value, -ActionUISettings.HotbarPositionY.Value);
                 
                 // Listener for Config -> UI
                 ActionUISettings.HotbarPositionX.SettingChanged += OnHotbarPositionConfigChanged;
                 ActionUISettings.HotbarPositionY.SettingChanged += OnHotbarPositionConfigChanged;
                 
                 // Listener for UI -> Config (Dragging)
                 posUI.UIElementMoved.AddListener(OnHotbarUIMoved);
            }
        }

        private void OnHotbarPositionConfigChanged(object sender, EventArgs e)
        {
            if (_hotbars != null)
            {
                var posUI = _hotbars.GetComponent<PositionableUI>();
                if (posUI != null)
                {
                    posUI.SetPosition(ActionUISettings.HotbarPositionX.Value, -ActionUISettings.HotbarPositionY.Value);
                }
            }
        }

        private void OnHotbarUIMoved(PositionableUI p)
        {
             // We must subtract DynamicOffset because SetPosition/Config expects the "Logical" position (without offset),
             // but anchoredPosition includes the offset. Update() adds the offset back.
             float logicalX = p.RectTransform.anchoredPosition.x - p.DynamicOffset.x;
             float logicalY = p.RectTransform.anchoredPosition.y - p.DynamicOffset.y;

             if (Math.Abs(ActionUISettings.HotbarPositionX.Value - logicalX) > 0.1f)
                ActionUISettings.HotbarPositionX.Value = logicalX;
                
             if (Math.Abs(ActionUISettings.HotbarPositionY.Value - (-logicalY)) > 0.1f)
                ActionUISettings.HotbarPositionY.Value = -logicalY;
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
