using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace fierrof.ActionBar
{
    /// <summary>
    /// Handles drag-and-drop, keybind assignment, mode cycling, and keybind activation for a slot.
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

        private bool  _isHovered;
        private Image _iconImage;
        private Text  _keyLabel;
        private Text  _modeLabel;
        private Image _bgImage;
        private CanvasGroup _slotCanvasGroup;

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
        }

        // ── Update ─────────────────────────────────────────

        void Update()
        {
            // Right-click behavior
            if (_isHovered && Input.GetMouseButtonDown(1))
            {
                if (IsEditMode)
                {
                    CycleMode();
                    return;
                }
                
                if (AssignedItem != null)
                {
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
                        if (Input.GetKeyDown(key) && key != KeyCode.Mouse0 && key != KeyCode.Mouse1)
                        {
                            SetKeybind(key);
                            return;
                        }
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
                case SlotMode.Hidden:   Mode = SlotMode.Disabled; break;
                case SlotMode.Disabled: Mode = SlotMode.Active; break;
            }

            UpdateModeVisual();
            Plugin.Log.LogMessage($"Bar {BarIndex + 1} Slot {SlotIndex + 1}: mode set to {Mode}.");

            var manager = GetComponentInParent<ActionBarManager>();
            if (manager != null) manager.SaveSlots();
        }

        private void UpdateModeVisual()
        {
            EnsureModeLabel();
            EnsureBgImage();

            if (IsEditMode)
            {
                // Always show all slots in edit mode
                EnsureCanvasGroup();
                _slotCanvasGroup.alpha = 1f;
                _slotCanvasGroup.blocksRaycasts = true;
                _slotCanvasGroup.interactable = true;

                switch (Mode)
                {
                    case SlotMode.Active:
                        _modeLabel.text = "";
                        _bgImage.color = new Color(0.12f, 0.12f, 0.12f, 0.85f);
                        break;
                    case SlotMode.Hidden:
                        _modeLabel.text = "HIDDEN";
                        _bgImage.color = new Color(0.3f, 0.25f, 0.05f, 0.85f); // dim yellow
                        break;
                    case SlotMode.Disabled:
                        _modeLabel.text = "OFF";
                        _bgImage.color = new Color(0.3f, 0.05f, 0.05f, 0.85f); // dim red
                        break;
                }
            }
            else
            {
                _modeLabel.text = "";
                _bgImage.color = new Color(0.12f, 0.12f, 0.12f, 0.85f);
            }
        }

        private void UpdateVisibility()
        {
            if (IsEditMode) return; // edit mode always shows all

            EnsureCanvasGroup();
            EnsureBgImage();

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
                    // Subtle yellow tint so you know it's a hidden slot
                    _bgImage.color = show ? new Color(0.18f, 0.16f, 0.08f, 0.85f) : new Color(0.12f, 0.12f, 0.12f, 0.85f);
                    break;
                case SlotMode.Disabled:
                    _slotCanvasGroup.alpha = 0f;
                    _slotCanvasGroup.blocksRaycasts = false;
                    _slotCanvasGroup.interactable = false;
                    break;
            }
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
            Plugin.Log.LogMessage($"Bar {BarIndex + 1} Slot {SlotIndex + 1}: assigned '{item.Name}'.");

            var manager = GetComponentInParent<ActionBarManager>();
            if (manager != null) manager.SaveSlots();
        }

        public void ClearSlot()
        {
            AssignedItem = null;
            UpdateIcon();

            var manager = GetComponentInParent<ActionBarManager>();
            if (manager != null) manager.SaveSlots();
        }

        // ── Keybind management ─────────────────────────────

        private void SetKeybind(KeyCode key)
        {
            if (BarIndex >= Plugin.MAX_BARS || SlotIndex >= Plugin.MAX_SLOTS) return;

            // Unbind this key from any other slots first
            if (key != KeyCode.None)
            {
                for (int b = 0; b < Plugin.MAX_BARS; b++)
                {
                    for (int s = 0; s < Plugin.MAX_SLOTS; s++)
                    {
                        if ((b != BarIndex || s != SlotIndex) && Plugin.SlotKeys[b][s].Value == key)
                        {
                            Plugin.SlotKeys[b][s].Value = KeyCode.None;
                            Plugin.Log.LogMessage($"Bar {b + 1} Slot {s + 1}: unbound '{key}' because it was assigned to Bar {BarIndex + 1} Slot {SlotIndex + 1}.");
                        }
                    }
                }
            }

            Plugin.SlotKeys[BarIndex][SlotIndex].Value = key;
            
            // Update labels for all active slots
            var allHandlers = FindObjectsOfType<SlotDropHandler>();
            foreach (var handler in allHandlers)
            {
                handler.UpdateKeyLabel();
            }

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

            // Ensure key label stays on top of the icon
            if (_keyLabel != null)
                _keyLabel.transform.SetAsLastSibling();
            if (_modeLabel != null)
                _modeLabel.transform.SetAsLastSibling();
        }

        private void EnsureIconImage()
        {
            if (_iconImage != null) return;

            var iconGO = new GameObject("Icon");
            iconGO.layer = 5;
            iconGO.transform.SetParent(transform, false);

            _iconImage = iconGO.AddComponent<Image>();
            _iconImage.preserveAspect = true;
            _iconImage.raycastTarget  = false;
            _iconImage.enabled        = false;

            var rect = iconGO.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void EnsureKeyLabel()
        {
            if (_keyLabel != null) return;

            var labelGO = new GameObject("KeyLabel");
            labelGO.layer = 5;
            labelGO.transform.SetParent(transform, false);

            _keyLabel = labelGO.AddComponent<Text>();
            _keyLabel.font      = Font.CreateDynamicFontFromOSFont("Arial", 12);
            _keyLabel.fontSize  = 11;
            _keyLabel.alignment = TextAnchor.UpperRight;
            _keyLabel.color     = new Color(1f, 1f, 1f, 0.8f);
            _keyLabel.raycastTarget = false;

            var outline = labelGO.AddComponent<Outline>();
            outline.effectColor    = new Color(0f, 0f, 0f, 0.6f);
            outline.effectDistance = new Vector2(1f, -1f);

            var rect = labelGO.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(2f, 2f);
            rect.offsetMax = new Vector2(-2f, -2f);
        }

        private void EnsureModeLabel()
        {
            if (_modeLabel != null) return;

            var labelGO = new GameObject("ModeLabel");
            labelGO.layer = 5;
            labelGO.transform.SetParent(transform, false);

            _modeLabel = labelGO.AddComponent<Text>();
            _modeLabel.font      = Font.CreateDynamicFontFromOSFont("Arial", 10);
            _modeLabel.fontSize  = 9;
            _modeLabel.alignment = TextAnchor.LowerCenter;
            _modeLabel.color     = new Color(1f, 1f, 1f, 0.9f);
            _modeLabel.raycastTarget = false;

            var outline = labelGO.AddComponent<Outline>();
            outline.effectColor    = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(1f, -1f);

            var rect = labelGO.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(2f, 2f);
            rect.offsetMax = new Vector2(-2f, -2f);
        }

        private void EnsureBgImage()
        {
            if (_bgImage != null) return;
            _bgImage = GetComponent<Image>();
        }

        private void EnsureCanvasGroup()
        {
            if (_slotCanvasGroup != null) return;
            _slotCanvasGroup = GetComponent<CanvasGroup>();
            if (_slotCanvasGroup == null)
                _slotCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // ── Helpers ────────────────────────────────────────

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
