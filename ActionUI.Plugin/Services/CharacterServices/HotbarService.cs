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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        private string _activeContextSignature = string.Empty;

        private const int NoWeaponType = -1;
        private const int OffhandNonWeaponContextOffset = 1000000;

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
            EquipmentPatches.AfterOnEquip += OnEquipmentChanged;
            EquipmentPatches.AfterOnUnequip += OnEquipmentChanged;
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
                EnsureNoWeaponDynamicPresetsInitialized();
                ApplyDynamicPresetsForCurrentWeapon(force: true);
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

                    SyncDynamicPresetsForCurrentWeapon();

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

        private void OnEquipmentChanged(Character character, Equipment equipment)
        {
            try
            {
                if (!_isStarted || character == null || _character == null || character.UID != _character.UID)
                    return;

                _levelCoroutines.DoNextFrame(() => ApplyDynamicPresetsForCurrentWeapon());
            }
            catch (Exception ex)
            {
                Logger.LogException("Failed applying dynamic presets after equipment change.", ex);
            }
        }

        private void ApplyDynamicPresetsForCurrentWeapon(bool force = false)
        {
            if (!(_hotbarProfileService is GlobalHotbarService globalService))
                return;

            var context = GetCurrentWeaponContext();
            if (!force && string.Equals(context.Signature, _activeContextSignature, StringComparison.Ordinal))
                return;

            _activeContextSignature = context.Signature;

            var profile = GetOrCreateActiveProfile();
            var anyChanged = false;

            var actionSlots = _hotbars.Controller.GetActionSlots();
            for (int hotbarIndex = 0; hotbarIndex < actionSlots.Length; hotbarIndex++)
            {
                var barSlots = actionSlots[hotbarIndex];
                for (int slotIndex = 0; slotIndex < barSlots.Length; slotIndex++)
                {
                    var actionSlot = barSlots[slotIndex];
                    if (actionSlot?.Config == null || !actionSlot.Config.IsDynamic)
                        continue;

                    SlotDataEntry slotEntry = null;
                    var hasPreset = false;
                    for (int k = 0; k < context.ResolveKeys.Length; k++)
                    {
                        if (globalService.TryGetDynamicPresetSlot(context.ResolveKeys[k], hotbarIndex, slotIndex, out slotEntry))
                        {
                            hasPreset = true;
                            break;
                        }
                    }

                    if (!hasPreset || slotEntry == null)
                        continue;

                    if (slotEntry.ItemID <= 0 && string.IsNullOrWhiteSpace(slotEntry.ItemUID))
                    {
                        if (actionSlot.SlotAction != null)
                        {
                            actionSlot.Controller.AssignEmptyAction();
                            anyChanged = true;
                        }
                        continue;
                    }

                    var hasSameAssignment = actionSlot.SlotAction != null
                        && actionSlot.SlotAction.ActionId == slotEntry.ItemID
                        && string.Equals(actionSlot.SlotAction.ActionUid ?? string.Empty, slotEntry.ItemUID ?? string.Empty, StringComparison.Ordinal);
                    if (hasSameAssignment)
                        continue;

                    if (_slotData.TryGetItemSlotAction(slotEntry.ItemID, slotEntry.ItemUID, profile.CombatMode, out var slotAction))
                    {
                        actionSlot.Controller.AssignSlotAction(slotAction, true);
                        anyChanged = true;
                    }
                }
            }

            if (!anyChanged)
                return;

            _hotbarProfileService.Update(_hotbars);
            globalService.SaveCharacterSlots(_character.UID);
            _hotbars.ClearChanges();
        }

        private void SyncDynamicPresetsForCurrentWeapon()
        {
            if (!(_hotbarProfileService is GlobalHotbarService globalService))
                return;

            var context = GetCurrentWeaponContext();
            var activeEditKey = context.EditKey;

            var actionSlots = _hotbars.Controller.GetActionSlots();
            for (int hotbarIndex = 0; hotbarIndex < actionSlots.Length; hotbarIndex++)
            {
                var barSlots = actionSlots[hotbarIndex];
                for (int slotIndex = 0; slotIndex < barSlots.Length; slotIndex++)
                {
                    var actionSlot = barSlots[slotIndex];
                    if (actionSlot?.Config == null || !actionSlot.Config.IsDynamic)
                        continue;

                    if (actionSlot.SlotAction == null)
                    {
                        if (string.Equals(activeEditKey, GlobalHotbarService.GetBaselineContextKey(), StringComparison.Ordinal))
                        {
                            globalService.SetDynamicPresetSlot(activeEditKey, hotbarIndex, slotIndex, -1, null);
                        }
                        else
                        {
                            globalService.RemoveDynamicPresetSlot(activeEditKey, hotbarIndex, slotIndex);
                        }
                    }
                    else
                    {
                        globalService.SetDynamicPresetSlot(
                            activeEditKey,
                            hotbarIndex,
                            slotIndex,
                            actionSlot.SlotAction.ActionId,
                            actionSlot.SlotAction.ActionUid);
                    }
                }
            }
        }

        private void EnsureNoWeaponDynamicPresetsInitialized()
        {
            if (!(_hotbarProfileService is GlobalHotbarService globalService))
                return;

            var actionSlots = _hotbars.Controller.GetActionSlots();
            for (int hotbarIndex = 0; hotbarIndex < actionSlots.Length; hotbarIndex++)
            {
                var barSlots = actionSlots[hotbarIndex];
                for (int slotIndex = 0; slotIndex < barSlots.Length; slotIndex++)
                {
                    var actionSlot = barSlots[slotIndex];
                    if (actionSlot?.Config == null || !actionSlot.Config.IsDynamic)
                        continue;

                    var baselineKey = GlobalHotbarService.GetBaselineContextKey();
                    if (globalService.TryGetDynamicPresetSlot(baselineKey, hotbarIndex, slotIndex, out _))
                        continue;

                    if (actionSlot.SlotAction == null)
                    {
                        globalService.SetDynamicPresetSlot(baselineKey, hotbarIndex, slotIndex, -1, null);
                    }
                    else
                    {
                        globalService.SetDynamicPresetSlot(
                            baselineKey,
                            hotbarIndex,
                            slotIndex,
                            actionSlot.SlotAction.ActionId,
                            actionSlot.SlotAction.ActionUid);
                    }
                }
            }

            if (_hotbarProfileService is GlobalHotbarService global && _character != null)
                global.SaveCharacterSlots(_character.UID);
        }

        private WeaponContextSnapshot GetCurrentWeaponContext()
        {
            var mainWeaponType = NoWeaponType;
            var offContextType = NoWeaponType;

            try
            {
                var mainHandEquipment = ReadMainHandEquipment(_character);
                var offHandEquipment = ReadOffhandEquipment(_character);

                if (mainHandEquipment == null)
                {
                    var currentWeapon = ReadCurrentWeapon(_character);
                    if (currentWeapon != null)
                    {
                        if (offHandEquipment == null || !ReferenceEquals(currentWeapon, offHandEquipment))
                            mainHandEquipment = currentWeapon;
                    }
                }

                if (mainHandEquipment != null)
                    mainWeaponType = GetMainContextType(mainHandEquipment);

                if (offHandEquipment != null && !ReferenceEquals(offHandEquipment, mainHandEquipment))
                    offContextType = GetOffhandContextType(offHandEquipment);
            }
            catch
            {
                mainWeaponType = NoWeaponType;
                offContextType = NoWeaponType;
            }

            var resolveKeys = BuildResolveKeys(mainWeaponType, offContextType);
            var editKey = BuildEditKey(mainWeaponType, offContextType);
            return new WeaponContextSnapshot(mainWeaponType, offContextType, resolveKeys, editKey);
        }

        private static Weapon ReadCurrentWeapon(Character character)
        {
            if (character == null)
                return null;

            var characterType = character.GetType();
            var prop = characterType.GetProperty("CurrentWeapon", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop?.GetValue(character, null) is Weapon weapon)
                return weapon;

            var inventory = character.Inventory;
            var equipment = inventory?.Equipment;
            if (equipment == null)
                return null;

            var equipmentProp = equipment.GetType().GetProperty("CurrentWeapon", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return equipmentProp?.GetValue(equipment, null) as Weapon;
        }

        private static Equipment ReadMainHandEquipment(Character character)
        {
            if (character == null)
                return null;

            var fromCharacter = ReadEquipmentFromPropertyNames(character,
                "RightHandWeapon",
                "RightHandEquipment",
                "MainHandWeapon",
                "MainHandEquipment");
            if (fromCharacter != null)
                return fromCharacter;

            var inventory = character.Inventory;
            var equipment = inventory?.Equipment;
            if (equipment == null)
                return null;

            return ReadEquipmentFromPropertyNames(equipment,
                "RightHandWeapon",
                "RightHandEquipment",
                "MainHandWeapon",
                "MainHandEquipment");
        }

        private static Equipment ReadOffhandEquipment(Character character)
        {
            if (character == null)
                return null;

            var fromCharacter = ReadEquipmentFromPropertyNames(character,
                "LeftHandWeapon",
                "LeftHandEquipment",
                "OffHandWeapon",
                "OffHandEquipment");
            if (fromCharacter != null)
                return fromCharacter;

            var inventory = character.Inventory;
            var equipment = inventory?.Equipment;
            if (equipment == null)
                return null;

            return ReadEquipmentFromPropertyNames(equipment,
                "LeftHandWeapon",
                "LeftHandEquipment",
                "OffHandWeapon",
                "OffHandEquipment");
        }

        private static Equipment ReadEquipmentFromPropertyNames(object source, params string[] propertyNames)
        {
            if (source == null || propertyNames == null || propertyNames.Length == 0)
                return null;

            var sourceType = source.GetType();
            for (int i = 0; i < propertyNames.Length; i++)
            {
                var prop = sourceType.GetProperty(propertyNames[i], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop == null)
                    continue;

                if (prop.GetValue(source, null) is Equipment equipment)
                    return equipment;
            }

            return null;
        }

        private static int GetMainContextType(Equipment mainEquipment)
        {
            if (mainEquipment == null)
                return NoWeaponType;

            if (mainEquipment is Weapon mainWeapon)
                return (int)mainWeapon.Type;

            var itemIdProp = mainEquipment.GetType().GetProperty("ItemID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (itemIdProp?.GetValue(mainEquipment, null) is int itemId && itemId > 0)
                return itemId;

            return NoWeaponType;
        }

        private static int GetOffhandContextType(Equipment offhandEquipment)
        {
            if (offhandEquipment == null)
                return NoWeaponType;

            if (offhandEquipment is Weapon offhandWeapon)
                return (int)offhandWeapon.Type;

            var itemIdProp = offhandEquipment.GetType().GetProperty("ItemID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (itemIdProp?.GetValue(offhandEquipment, null) is int itemId && itemId > 0)
                return OffhandNonWeaponContextOffset + itemId;

            return NoWeaponType;
        }

        private static string[] BuildResolveKeys(int mainWeaponType, int offWeaponType)
        {
            var keys = new List<string>(4);

            if (mainWeaponType != NoWeaponType && offWeaponType != NoWeaponType)
                keys.Add(GlobalHotbarService.GetComboContextKey(mainWeaponType, offWeaponType));

            if (mainWeaponType != NoWeaponType)
                keys.Add(GlobalHotbarService.GetMainContextKey(mainWeaponType));

            if (offWeaponType != NoWeaponType)
                keys.Add(GlobalHotbarService.GetOffContextKey(offWeaponType));

            keys.Add(GlobalHotbarService.GetBaselineContextKey());
            return keys.Distinct().ToArray();
        }

        private static string BuildEditKey(int mainWeaponType, int offWeaponType)
        {
            if (mainWeaponType != NoWeaponType && offWeaponType != NoWeaponType)
                return GlobalHotbarService.GetComboContextKey(mainWeaponType, offWeaponType);

            if (mainWeaponType != NoWeaponType)
                return GlobalHotbarService.GetMainContextKey(mainWeaponType);

            if (offWeaponType != NoWeaponType)
                return GlobalHotbarService.GetOffContextKey(offWeaponType);

            return GlobalHotbarService.GetBaselineContextKey();
        }

        private sealed class WeaponContextSnapshot
        {
            public WeaponContextSnapshot(int mainWeaponType, int offWeaponType, string[] resolveKeys, string editKey)
            {
                MainWeaponType = mainWeaponType;
                OffWeaponType = offWeaponType;
                ResolveKeys = resolveKeys ?? Array.Empty<string>();
                EditKey = editKey ?? GlobalHotbarService.GetBaselineContextKey();
                Signature = $"{MainWeaponType}|{OffWeaponType}";
            }

            public int MainWeaponType { get; }
            public int OffWeaponType { get; }
            public string[] ResolveKeys { get; }
            public string EditKey { get; }
            public string Signature { get; }
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
                    EquipmentPatches.AfterOnEquip -= OnEquipmentChanged;
                    EquipmentPatches.AfterOnUnequip -= OnEquipmentChanged;

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
