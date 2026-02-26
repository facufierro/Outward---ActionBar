using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace fierrof.ActionBar
{
    /// <summary>
    /// Handles drag-and-drop, keybind assignment, mode cycling, keybind activation,
    /// dynamic slot behavior, cooldown overlay, and item count display for a slot.
    ///
    /// Mode (Active/Hidden/Disabled) and IsDynamic are independent flags.
    /// A slot can be Hidden+Dynamic but Disabled cannot be Dynamic.
    /// </summary>
    public class SlotDropHandler : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public int BarIndex { get; set; }
        public int SlotIndex { get; set; }

        /// <summary>True when the pointer is over any action bar slot.</summary>
        public static bool IsPointerOverSlot { get; private set; }

        /// <summary>Global edit mode toggle — set by Plugin's config button.</summary>
        public static bool IsEditMode { get; set; }

        /// <summary>The item currently assigned to this slot.</summary>
        public Item AssignedItem { get; private set; }

        /// <summary>Visibility/functionality mode for this slot.</summary>
        public SlotMode Mode { get; set; } = SlotMode.Active;

        /// <summary>Whether this slot uses weapon-context dynamic presets.</summary>
        public bool IsDynamic { get; set; }

        private bool  _isHovered;
        private Image _iconImage;
        private Text  _keyLabel;
        private Image _bgImage;
        private Outline _outline;
        private CanvasGroup _slotCanvasGroup;

        // State indicator icons
        private Image _modeStateIcon;
        private Image _dynamicStateIcon;
        private static Sprite _hiddenSprite;
        private static Sprite _disabledSprite;
        private static Sprite _dynamicSprite;
        private static bool _spritesLoaded;

        // Cooldown UI
        private Image _cooldownOverlay;
        private Coroutine _cooldownCoroutine;

        // Count UI
        private Text _countLabel;
        private Coroutine _countCoroutine;

        // ── Sprite loading ────────────────────────────────

        private static void LoadSprites()
        {
            if (_spritesLoaded) return;
            _spritesLoaded = true;

            _hiddenSprite   = LoadPNG("hidden-icon.png");
            _disabledSprite = LoadPNG("disabled-icon.png");
            _dynamicSprite  = LoadPNG("dynmic-icon.png"); // filename has typo in asset
        }

        private static Sprite LoadPNG(string filename)
        {
            string path = Path.Combine(
                Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
                "assets", filename);

            if (!File.Exists(path))
            {
                // Try alternate path: next to DLL
                path = Path.Combine(
                    Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
                    filename);
            }

            if (!File.Exists(path))
            {
                Plugin.Log.LogWarning($"Icon not found: {filename}");
                return null;
            }

            try
            {
                byte[] data = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(data);
                tex.filterMode = FilterMode.Bilinear;
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), 100f);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Failed to load icon {filename}: {ex.Message}");
                return null;
            }
        }

        // ── Pointer events ─────────────────────────────────

        public void OnPointerEnter(PointerEventData eventData)
        {
            IsPointerOverSlot = true;
            _isHovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            IsPointerOverSlot = false;
            _isHovered = false;
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (IsEditMode) return;
            if (Mode == SlotMode.Disabled) return;

            var itemDisplay = GetDraggedItem(eventData);
            if (itemDisplay?.RefItem == null) return;
            if (!itemDisplay.RefItem.IsQuickSlotable) return;

            AssignItem(itemDisplay.RefItem);

            // If dynamic slot, save preset for current weapon context
            if (IsDynamic)
            {
                var character = CharacterManager.Instance?.GetFirstLocalCharacter();
                if (character != null)
                {
                    string contextKey = DynamicPresetManager.GetContextKey(character);
                    DynamicPresetManager.SetPreset(contextKey, BarIndex, SlotIndex,
                        itemDisplay.RefItem.ItemID, itemDisplay.RefItem.UID);
                    DynamicPresetManager.SavePresets(character.UID);
                }
            }
        }

        // ── Update ─────────────────────────────────────────

        void Update()
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

            // Right-click behavior
            if (_isHovered && Input.GetMouseButtonDown(1))
            {
                if (IsEditMode)
                {
                    // Ctrl+Right-Click toggles Dynamic flag, plain Right-Click cycles mode
                    if (ctrl)
                        ToggleDynamic();
                    else
                        CycleMode();
                    return;
                }

                if (AssignedItem != null)
                {
                    // If dynamic slot, also clear the preset for current context
                    if (IsDynamic)
                    {
                        var character = CharacterManager.Instance?.GetFirstLocalCharacter();
                        if (character != null)
                        {
                            string contextKey = DynamicPresetManager.GetContextKey(character);
                            DynamicPresetManager.RemovePreset(contextKey, BarIndex, SlotIndex);
                            DynamicPresetManager.SavePresets(character.UID);
                        }
                    }
                    ClearSlot();
                    return;
                }
            }

            // Edit mode: hover + press any key to assign keybind
            if (IsEditMode && _isHovered)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    IsEditMode = false;
                    return;
                }

                if (Input.anyKeyDown)
                {
                    foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
                    {
                        if (!Input.GetKeyDown(key)) continue;
                        if (key == KeyCode.Mouse0 || key == KeyCode.Mouse1 || key == KeyCode.Mouse2) continue;
                        if (IsModifierKey(key)) continue;
                        SetKeybind(key);
                        return;
                    }
                }
                return;
            }

            // Gameplay visibility
            UpdateVisibility();

            // Gameplay: press bound key to activate item
            if (Mode == SlotMode.Disabled) return;
            if (AssignedItem == null) return;
            if (BarIndex >= Plugin.MAX_BARS || SlotIndex >= Plugin.MAX_SLOTS) return;

            var boundKey = Plugin.SlotKeys[BarIndex][SlotIndex].Value;
            if (boundKey == KeyCode.None) return;

            if (Input.GetKeyDown(boundKey))
            {
                if (!IsGameplay()) return;
                AssignedItem.TryQuickSlotUse();
            }
        }

        // ── Mode cycling ──────────────────────────────────

        private void CycleMode()
        {
            switch (Mode)
            {
                case SlotMode.Active:   Mode = SlotMode.Hidden; break;
                case SlotMode.Hidden:   Mode = SlotMode.Disabled; IsDynamic = false; break;
                case SlotMode.Disabled: Mode = SlotMode.Active; break;
            }

            UpdateModeVisual();
            Plugin.Log.LogMessage($"Bar {BarIndex + 1} Slot {SlotIndex + 1}: mode={Mode}, dynamic={IsDynamic}.");

            var manager = GetComponentInParent<ActionBarManager>();
            if (manager != null) manager.SaveSlots();
        }

        private void ToggleDynamic()
        {
            if (Mode == SlotMode.Disabled) return; // can't make disabled slot dynamic

            IsDynamic = !IsDynamic;
            UpdateModeVisual();
            Plugin.Log.LogMessage($"Bar {BarIndex + 1} Slot {SlotIndex + 1}: dynamic={IsDynamic}.");

            // When marking as dynamic, initialize baseline preset with current assignment
            if (IsDynamic && AssignedItem != null)
            {
                DynamicPresetManager.SetPreset("baseline", BarIndex, SlotIndex,
                    AssignedItem.ItemID, AssignedItem.UID);
            }

            var manager = GetComponentInParent<ActionBarManager>();
            if (manager != null) manager.SaveSlots();
        }

        private void UpdateModeVisual()
        {
            EnsureBgImage();
            EnsureOutline();
            EnsureModeStateIcon();
            EnsureDynamicStateIcon();

            if (IsEditMode)
            {
                EnsureCanvasGroup();
                _slotCanvasGroup.alpha = 1f;
                _slotCanvasGroup.blocksRaycasts = true;
                _slotCanvasGroup.interactable = true;

                // Keep a neutral slot color in edit mode; state is represented by icons.
                _bgImage.color = new Color(0.12f, 0.12f, 0.12f, 0.85f);
                _outline.effectColor = Color.black;

                // State icon: show the most relevant indicator
                UpdateStateIcon();
            }
            else
            {
                _bgImage.color = new Color(0.12f, 0.12f, 0.12f, 0.85f);
                _outline.effectColor = Color.black;
                UpdateStateIcon();
            }
        }

        private void UpdateStateIcon()
        {
            EnsureModeStateIcon();
            EnsureDynamicStateIcon();
            if (_modeStateIcon == null || _dynamicStateIcon == null) return;

            LoadSprites();

            // Disabled is exclusive and suppresses other indicators.
            if (Mode == SlotMode.Disabled)
            {
                _modeStateIcon.sprite = _disabledSprite;
                _modeStateIcon.color = IsEditMode ? Color.white : new Color(1f, 1f, 1f, 0.55f);
                _modeStateIcon.enabled = _disabledSprite != null;
                _dynamicStateIcon.enabled = false;
                return;
            }

            // Hidden and Dynamic can be active at the same time, so render both.
            if (Mode == SlotMode.Hidden)
            {
                _modeStateIcon.sprite = _hiddenSprite;
                _modeStateIcon.color = IsEditMode ? Color.white : new Color(1f, 1f, 1f, 0.55f);
                _modeStateIcon.enabled = _hiddenSprite != null;
            }
            else
            {
                _modeStateIcon.enabled = false;
            }

            if (IsDynamic)
            {
                _dynamicStateIcon.sprite = _dynamicSprite;
                _dynamicStateIcon.color = IsEditMode ? Color.white : new Color(1f, 1f, 1f, 0.55f);
                _dynamicStateIcon.enabled = _dynamicSprite != null;
            }
            else
            {
                _dynamicStateIcon.enabled = false;
            }
        }

        private void UpdateVisibility()
        {
            if (IsEditMode) return;

            EnsureCanvasGroup();
            EnsureBgImage();
            EnsureOutline();

            switch (Mode)
            {
                case SlotMode.Active:
                    _slotCanvasGroup.alpha = 1f;
                    _slotCanvasGroup.blocksRaycasts = true;
                    _slotCanvasGroup.interactable = true;
                    _bgImage.color = new Color(0.12f, 0.12f, 0.12f, 0.85f);
                    break;
                case SlotMode.Hidden:
                    bool inventoryOpen = IsInventoryOpen();
                    bool hasItem = AssignedItem != null;
                    bool show = inventoryOpen || hasItem;
                    _slotCanvasGroup.alpha = show ? 1f : 0f;
                    _slotCanvasGroup.blocksRaycasts = show;
                    _slotCanvasGroup.interactable = show;
                    _bgImage.color = show ? new Color(0.18f, 0.16f, 0.08f, 0.85f)
                                          : new Color(0.12f, 0.12f, 0.12f, 0.85f);
                    break;
                case SlotMode.Disabled:
                    _slotCanvasGroup.alpha = 0f;
                    _slotCanvasGroup.blocksRaycasts = false;
                    _slotCanvasGroup.interactable = false;
                    break;
            }

            // Keep a neutral border; state is represented by icons.
            _outline.effectColor = Color.black;
        }

        private static bool IsInventoryOpen()
        {
            var character = CharacterManager.Instance?.GetFirstLocalCharacter();
            if (character?.CharacterUI == null) return false;
            return character.CharacterUI.GetIsMenuDisplayed(CharacterUI.MenuScreens.Inventory);
        }

        // ── Item management ────────────────────────────────

        public void AssignItem(Item item)
        {
            AssignedItem = item;
            UpdateIcon();
            StartTracking();
            Plugin.Log.LogMessage($"Bar {BarIndex + 1} Slot {SlotIndex + 1}: assigned '{item.Name}'.");

            var manager = GetComponentInParent<ActionBarManager>();
            if (manager != null) manager.SaveSlots();
        }

        /// <summary>Assigns item without triggering save (used during load/preset apply).</summary>
        public void AssignItemSilent(Item item)
        {
            AssignedItem = item;
            UpdateIcon();
            StartTracking();
        }

        public void ClearSlot()
        {
            AssignedItem = null;
            UpdateIcon();
            StopTracking();

            var manager = GetComponentInParent<ActionBarManager>();
            if (manager != null) manager.SaveSlots();
        }

        /// <summary>Clears without triggering save (used during preset apply).</summary>
        public void ClearSlotSilent()
        {
            AssignedItem = null;
            UpdateIcon();
            StopTracking();
        }

        // ── Cooldown & count tracking ───────────────────────

        private void StartTracking()
        {
            StopTracking();
            if (AssignedItem == null) return;

            if (AssignedItem is Skill skill)
            {
                EnsureCooldownOverlay();
                _cooldownCoroutine = StartCoroutine(TrackCooldown(skill));
            }

            EnsureCountLabel();
            _countCoroutine = StartCoroutine(TrackCount());
        }

        private void StopTracking()
        {
            if (_cooldownCoroutine != null)
            {
                StopCoroutine(_cooldownCoroutine);
                _cooldownCoroutine = null;
            }
            if (_cooldownOverlay != null)
                _cooldownOverlay.fillAmount = 0f;

            if (_countCoroutine != null)
            {
                StopCoroutine(_countCoroutine);
                _countCoroutine = null;
            }
            if (_countLabel != null)
                _countLabel.text = "";
        }

        private IEnumerator TrackCooldown(Skill skill)
        {
            var progressMethod = skill.GetType().GetMethod("GetCooldownProgress",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            var remainingField = skill.GetType().GetField("m_remainingCooldownTime",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cooldownField = skill.GetType().GetField("m_cooldownTime",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            while (true)
            {
                if (skill != null && skill.InCooldown())
                {
                    float progress = 0f;
                    if (progressMethod != null)
                    {
                        progress = (float)progressMethod.Invoke(skill, null);
                    }
                    else if (remainingField != null && cooldownField != null)
                    {
                        float remaining = (float)remainingField.GetValue(skill);
                        float total = (float)cooldownField.GetValue(skill);
                        progress = total > 0f ? remaining / total : 0f;
                    }
                    _cooldownOverlay.fillAmount = Mathf.Clamp01(progress);
                    _cooldownOverlay.color = new Color(0f, 0f, 0f, 0.6f);
                }
                else
                {
                    _cooldownOverlay.fillAmount = 0f;
                }
                yield return new WaitForSeconds(0.05f);
            }
        }

        private IEnumerator TrackCount()
        {
            while (true)
            {
                if (AssignedItem != null)
                {
                    var character = CharacterManager.Instance?.GetFirstLocalCharacter();
                    if (character?.Inventory != null)
                    {
                        int count = 0;
                        if (AssignedItem.GroupItemInDisplay || AssignedItem.IsStackable)
                            count = character.Inventory.ItemCount(AssignedItem.ItemID);
                        else
                            count = AssignedItem.QuickSlotCountDisplay;

                        _countLabel.text = count > 0 ? count.ToString() : "";
                    }
                }
                else
                {
                    _countLabel.text = "";
                }
                yield return new WaitForSeconds(0.5f);
            }
        }

        // ── Keybind management ─────────────────────────────

        private void SetKeybind(KeyCode key)
        {
            if (BarIndex >= Plugin.MAX_BARS || SlotIndex >= Plugin.MAX_SLOTS) return;

            if (key != KeyCode.None)
            {
                for (int b = 0; b < Plugin.MAX_BARS; b++)
                    for (int s = 0; s < Plugin.MAX_SLOTS; s++)
                        if ((b != BarIndex || s != SlotIndex) && Plugin.SlotKeys[b][s].Value == key)
                        {
                            Plugin.SlotKeys[b][s].Value = KeyCode.None;
                            Plugin.Log.LogMessage($"Bar {b + 1} Slot {s + 1}: unbound '{key}'.");
                        }
            }

            Plugin.SlotKeys[BarIndex][SlotIndex].Value = key;

            foreach (var handler in FindObjectsOfType<SlotDropHandler>())
                handler.UpdateKeyLabel();

            Plugin.Log.LogMessage($"Bar {BarIndex + 1} Slot {SlotIndex + 1}: bound to '{key}'.");
        }

        public void UpdateKeyLabel()
        {
            EnsureKeyLabel();
            if (BarIndex >= Plugin.MAX_BARS || SlotIndex >= Plugin.MAX_SLOTS)
            {
                _keyLabel.text = "";
                return;
            }
            var key = Plugin.SlotKeys[BarIndex][SlotIndex].Value;
            _keyLabel.text = key == KeyCode.None ? "" : FormatKeyName(key);
        }

        private static string FormatKeyName(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Alpha0: return "0";
                case KeyCode.Alpha1: return "1";
                case KeyCode.Alpha2: return "2";
                case KeyCode.Alpha3: return "3";
                case KeyCode.Alpha4: return "4";
                case KeyCode.Alpha5: return "5";
                case KeyCode.Alpha6: return "6";
                case KeyCode.Alpha7: return "7";
                case KeyCode.Alpha8: return "8";
                case KeyCode.Alpha9: return "9";
                case KeyCode.Minus:  return "-";
                case KeyCode.Equals: return "=";
                default: return key.ToString();
            }
        }

        // ── Edit mode visual refresh ──────────────────────

        public void RefreshEditModeVisuals()
        {
            UpdateModeVisual();
        }

        // ── UI setup ───────────────────────────────────────

        private void Start()
        {
            UpdateKeyLabel();
        }

        private void UpdateIcon()
        {
            EnsureIconImage();

            if (AssignedItem != null && AssignedItem.ItemIcon != null)
            {
                _iconImage.sprite  = AssignedItem.ItemIcon;
                _iconImage.color   = Color.white;
                _iconImage.enabled = true;
            }
            else
            {
                _iconImage.sprite  = null;
                _iconImage.color   = Color.clear;
                _iconImage.enabled = false;
            }

            // Ensure overlays stay on top
            if (_cooldownOverlay != null)
                _cooldownOverlay.transform.SetAsLastSibling();
            if (_keyLabel != null)
                _keyLabel.transform.SetAsLastSibling();
            if (_countLabel != null)
                _countLabel.transform.SetAsLastSibling();
            if (_modeStateIcon != null)
                _modeStateIcon.transform.SetAsLastSibling();
            if (_dynamicStateIcon != null)
                _dynamicStateIcon.transform.SetAsLastSibling();
        }

        // ── UI element creation ─────────────────────────────

        private void EnsureIconImage()
        {
            if (_iconImage != null) return;

            var go = new GameObject("Icon");
            go.layer = 5;
            go.transform.SetParent(transform, false);

            _iconImage = go.AddComponent<Image>();
            _iconImage.preserveAspect = true;
            _iconImage.raycastTarget  = false;
            _iconImage.enabled        = false;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void EnsureCooldownOverlay()
        {
            if (_cooldownOverlay != null) return;

            var go = new GameObject("CooldownOverlay");
            go.layer = 5;
            go.transform.SetParent(transform, false);

            _cooldownOverlay = go.AddComponent<Image>();
            _cooldownOverlay.type = Image.Type.Filled;
            _cooldownOverlay.fillMethod = Image.FillMethod.Radial360;
            _cooldownOverlay.fillOrigin = (int)Image.Origin360.Top;
            _cooldownOverlay.fillClockwise = true;
            _cooldownOverlay.fillAmount = 0f;
            _cooldownOverlay.color = new Color(0f, 0f, 0f, 0.6f);
            _cooldownOverlay.raycastTarget = false;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void EnsureCountLabel()
        {
            if (_countLabel != null) return;

            var go = new GameObject("CountLabel");
            go.layer = 5;
            go.transform.SetParent(transform, false);

            _countLabel = go.AddComponent<Text>();
            _countLabel.font = Font.CreateDynamicFontFromOSFont("Arial", 12);
            _countLabel.fontSize = 11;
            _countLabel.alignment = TextAnchor.LowerRight;
            _countLabel.color = new Color(1f, 1f, 1f, 0.9f);
            _countLabel.raycastTarget = false;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(1f, -1f);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(2f, 2f);
            rect.offsetMax = new Vector2(-2f, -2f);
        }

        private void EnsureKeyLabel()
        {
            if (_keyLabel != null) return;

            var go = new GameObject("KeyLabel");
            go.layer = 5;
            go.transform.SetParent(transform, false);

            _keyLabel = go.AddComponent<Text>();
            _keyLabel.font      = Font.CreateDynamicFontFromOSFont("Arial", 12);
            _keyLabel.fontSize  = 11;
            _keyLabel.alignment = TextAnchor.UpperRight;
            _keyLabel.color     = new Color(1f, 1f, 1f, 0.8f);
            _keyLabel.raycastTarget = false;

            var outline = go.AddComponent<Outline>();
            outline.effectColor    = new Color(0f, 0f, 0f, 0.6f);
            outline.effectDistance = new Vector2(1f, -1f);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(2f, 2f);
            rect.offsetMax = new Vector2(-2f, -2f);
        }

        private void EnsureModeStateIcon()
        {
            if (_modeStateIcon != null) return;

            LoadSprites();

            var go = new GameObject("ModeStateIcon");
            go.layer = 5;
            go.transform.SetParent(transform, false);

            _modeStateIcon = go.AddComponent<Image>();
            _modeStateIcon.preserveAspect = true;
            _modeStateIcon.raycastTarget = false;
            _modeStateIcon.enabled = false;
            _modeStateIcon.color = Color.white;

            // Mode icon in top-left corner.
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(16f, 16f);
            rect.anchoredPosition = new Vector2(2f, -2f);
        }

        private void EnsureDynamicStateIcon()
        {
            if (_dynamicStateIcon != null) return;

            LoadSprites();

            var go = new GameObject("DynamicStateIcon");
            go.layer = 5;
            go.transform.SetParent(transform, false);

            _dynamicStateIcon = go.AddComponent<Image>();
            _dynamicStateIcon.preserveAspect = true;
            _dynamicStateIcon.raycastTarget = false;
            _dynamicStateIcon.enabled = false;
            _dynamicStateIcon.color = Color.white;

            // Dynamic icon in top-right corner so it can coexist with hidden icon.
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(16f, 16f);
            rect.anchoredPosition = new Vector2(-2f, -2f);
        }

        private void EnsureBgImage()
        {
            if (_bgImage != null) return;
            _bgImage = GetComponent<Image>();
        }

        private void EnsureOutline()
        {
            if (_outline != null) return;
            _outline = GetComponent<Outline>();
        }

        private void EnsureCanvasGroup()
        {
            if (_slotCanvasGroup != null) return;
            _slotCanvasGroup = GetComponent<CanvasGroup>();
            if (_slotCanvasGroup == null)
                _slotCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // ── Helpers ────────────────────────────────────────

        private static bool IsModifierKey(KeyCode key)
        {
            return key == KeyCode.LeftControl  || key == KeyCode.RightControl
                || key == KeyCode.LeftAlt      || key == KeyCode.RightAlt
                || key == KeyCode.LeftShift    || key == KeyCode.RightShift;
        }

        private static bool IsGameplay()
        {
            return NetworkLevelLoader.Instance != null
                && !NetworkLevelLoader.Instance.IsGameplayPaused
                && !NetworkLevelLoader.Instance.IsGameplayLoading
                && CharacterManager.Instance?.GetFirstLocalCharacter() != null;
        }

        private ItemDisplay GetDraggedItem(PointerEventData eventData)
        {
            if (eventData?.pointerDrag == null) return null;
            return eventData.pointerDrag.GetComponent<ItemDisplay>();
        }
    }
}
