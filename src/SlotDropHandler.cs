using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace fierrof.ActionBar
{
    /// <summary>
    /// Handles drag-and-drop, keybind assignment, and keybind activation for a slot.
    /// </summary>
    public class SlotDropHandler : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public int BarIndex { get; set; }
        public int SlotIndex { get; set; }

        /// <summary>True when the pointer is over any action bar slot.</summary>
        public static bool IsPointerOverSlot { get; private set; }

        /// <summary>Global config mode toggle — set by Plugin's config button.</summary>
        public static bool IsConfigMode { get; set; }

        /// <summary>The item currently assigned to this slot.</summary>
        public Item AssignedItem { get; private set; }

        private bool  _isHovered;
        private Image _iconImage;
        private Text  _keyLabel;

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
            if (IsConfigMode) return; // don't assign items in config mode

            var itemDisplay = GetDraggedItem(eventData);
            if (itemDisplay?.RefItem == null) return;
            if (!itemDisplay.RefItem.IsQuickSlotable) return;

            AssignItem(itemDisplay.RefItem);
        }

        // ── Update ─────────────────────────────────────────


        void Update()
        {
            if (_isHovered && Input.GetMouseButtonDown(1) && AssignedItem != null && !IsConfigMode)
            {
                ClearSlot();
                return;
            }

            // Config mode: hover + press any key to assign keybind
            if (IsConfigMode && _isHovered)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    IsConfigMode = false;
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

            // Gameplay: press bound key to activate item
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
