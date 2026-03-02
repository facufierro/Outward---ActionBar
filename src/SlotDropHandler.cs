using System;
using System.Collections;
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
    /// A slot can be Active/Hidden/Disabled and also Dynamic.
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

        /// <summary>Stable item id cache for the assigned slot item, resilient to transient Unity object nulls.</summary>
        public int AssignedItemID { get; private set; } = -1;

        /// <summary>Base saved slot state used when no dynamic override matches current equipment context.</summary>
        public int BaseItemID { get; private set; } = -1;

        /// <summary>Visibility/functionality mode for this slot.</summary>
        public SlotMode Mode { get; set; } = SlotMode.Active;

        /// <summary>Whether this slot uses weapon-context dynamic presets.</summary>
        public bool IsDynamic { get; set; }

        private bool  _isHovered;
        private static readonly Color DynamicBorderColor = new Color(0.08f, 0.30f, 0.08f, 1f);
        private Image _iconImage;
        private Text  _keyLabel;
        private Image _bgImage;
        private Outline _outline;
        private Image[] _dynamicBorderEdges;
        private CanvasGroup _slotCanvasGroup;

        // Cooldown UI
        private Text _cooldownLabel;
        private Coroutine _cooldownCoroutine;

        // Count UI
        private Text _countLabel;
        private Coroutine _countCoroutine;

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

            int droppedItemId = itemDisplay.RefItem.ItemID;
            var character = CharacterManager.Instance?.GetFirstLocalCharacter();

            // Update model BEFORE AssignItem (which triggers Save)
            if (IsDynamic)
            {
                if (character != null)
                {
                    string contextKey = SlotSaveManager.GetContextKey(character);
                    if (contextKey == "baseline")
                    {
                        SetBaseItemIdOnly(droppedItemId);
                    }
                    else
                    {
                        SlotSaveManager.SetPreset(BarIndex, SlotIndex, contextKey, droppedItemId);
                    }
                }
                else
                {
                    SetBaseItemIdOnly(droppedItemId);
                }
            }
            else
            {
                SetBaseItemIdOnly(droppedItemId);
            }

            AssignItem(itemDisplay.RefItem); // This triggers SaveSlots → Save (writes everything)
        }

        // ── Update ─────────────────────────────────────────

        void Update()
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

            if (AssignedItem == null && AssignedItemID > 0)
                TryRehydrateAssignedItem();

            // Outside edit mode: middle-click toggles dynamic on hovered slot.
            if (!IsEditMode && _isHovered && Input.GetMouseButtonDown(2))
            {
                ToggleDynamic();
                return;
            }

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

                if (AssignedItemID > 0)
                {
                    // Dynamic clear: clear context override when equipped, clear base when unequipped.
                    if (IsDynamic)
                    {
                        var character = CharacterManager.Instance?.GetFirstLocalCharacter();
                        if (character != null)
                        {
                            string contextKey = SlotSaveManager.GetContextKey(character);
                            if (contextKey == "baseline")
                            {
                                SetBaseItemIdOnly(-1);
                                ClearSlot();
                                return;
                            }

                            SlotSaveManager.RemovePreset(BarIndex, SlotIndex, contextKey);

                            if (BaseItemID > 0)
                            {
                                var baseItem = SlotSaveManager.FindItem(character, BaseItemID);
                                if (baseItem != null)
                                    AssignItem(baseItem);
                                else
                                    ClearSlot();
                            }
                            else
                            {
                                ClearSlot();
                            }
                            return;
                        }
                    }

                    SetBaseItemIdOnly(-1);
                    ClearSlot(); // triggers SaveSlots → Save (writes everything)
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
                        if (key == KeyCode.Mouse0 || key == KeyCode.Mouse1) continue;
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
            if (BarIndex >= Plugin.MAX_BARS || SlotIndex >= Plugin.MAX_SLOTS_PER_BAR) return;

            var boundKey = Plugin.GetBoundKey(BarIndex, SlotIndex);
            if (boundKey == KeyCode.None) return;

            if (Input.GetKeyDown(boundKey))
            {
                if (!IsGameplay()) return;
                if (IsMenuOpen()) return;
                AssignedItem.TryQuickSlotUse();
                StartCoroutine(RefreshCountDelayed());
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
            Plugin.Log.LogMessage($"Bar {BarIndex + 1} Slot {SlotIndex + 1}: mode={Mode}, dynamic={IsDynamic}.");

            var manager = GetComponentInParent<ActionBarManager>();
            if (manager != null) manager.SaveSlots();
        }

        private void ToggleDynamic()
        {
            IsDynamic = !IsDynamic;

            var character = CharacterManager.Instance?.GetFirstLocalCharacter();

            if (IsDynamic)
            {
                // Preserve current slot state as base when enabling dynamic.
                SetBaseItemIdOnly(AssignedItemID);

                // Enabling: persist current item to current weapon context only
                int itemID = AssignedItemID > 0 ? AssignedItemID : -1;
                string contextKey = character != null
                    ? SlotSaveManager.GetContextKey(character)
                    : "baseline";
                SlotSaveManager.SetPreset(BarIndex, SlotIndex, contextKey, itemID);
            }
            else
            {
                // Disabling: clean up all presets for this slot
                SlotSaveManager.RemoveAllPresets(BarIndex, SlotIndex);
            }

            UpdateModeVisual();
            Plugin.Log.LogMessage($"Bar {BarIndex + 1} Slot {SlotIndex + 1}: dynamic={IsDynamic}.");

            // TXT save: non-dynamic slots now save their item, dynamic slots save -1
            var manager = GetComponentInParent<ActionBarManager>();
            if (manager != null) manager.SaveSlots();
        }

        private void UpdateModeVisual()
        {
            EnsureBgImage();
            EnsureOutline();
            EnsureDynamicBorder();

            if (IsEditMode)
            {
                EnsureCanvasGroup();
                _slotCanvasGroup.alpha = 1f;
                _slotCanvasGroup.blocksRaycasts = true;
                _slotCanvasGroup.interactable = true;

                switch (Mode)
                {
                    case SlotMode.Active:
                        _bgImage.color = new Color(0.12f, 0.12f, 0.12f, 0.85f);
                        break;
                    case SlotMode.Hidden:
                        _bgImage.color = new Color(0.3f, 0.25f, 0.05f, 0.85f); // yellow-ish
                        break;
                    case SlotMode.Disabled:
                        _bgImage.color = new Color(0.3f, 0.05f, 0.05f, 0.85f); // red-ish
                        break;
                }

                _outline.effectColor = Color.black;
                SetDynamicBorderVisible(IsDynamic);
            }
            else
            {
                switch (Mode)
                {
                    case SlotMode.Active:
                        _bgImage.color = new Color(0.12f, 0.12f, 0.12f, 0.85f);
                        break;
                    case SlotMode.Hidden:
                        _bgImage.color = new Color(0.3f, 0.25f, 0.05f, 0.85f);
                        break;
                    case SlotMode.Disabled:
                        _bgImage.color = new Color(0.3f, 0.05f, 0.05f, 0.85f);
                        break;
                }

                _outline.effectColor = Color.black;
                SetDynamicBorderVisible(IsDynamic);
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
                    bool hasItem = AssignedItemID > 0;
                    bool show = inventoryOpen || hasItem;
                    _slotCanvasGroup.alpha = show ? 1f : 0f;
                    _slotCanvasGroup.blocksRaycasts = show;
                    _slotCanvasGroup.interactable = show;
                    _bgImage.color = new Color(0.3f, 0.25f, 0.05f, 0.85f);
                    break;
                case SlotMode.Disabled:
                    _slotCanvasGroup.alpha = 0f;
                    _slotCanvasGroup.blocksRaycasts = false;
                    _slotCanvasGroup.interactable = false;
                    break;
            }

            // Dynamic should stack with hidden/active/disabled as border-only.
            _outline.effectColor = Color.black;
            SetDynamicBorderVisible(IsDynamic);
        }

        private static bool IsInventoryOpen()
        {
            var character = CharacterManager.Instance?.GetFirstLocalCharacter();
            if (character?.CharacterUI == null) return false;
            return character.CharacterUI.GetIsMenuDisplayed(CharacterUI.MenuScreens.Inventory);
        }

        private void TryRehydrateAssignedItem()
        {
            var character = CharacterManager.Instance?.GetFirstLocalCharacter();
            if (character == null) return;

            var item = SlotSaveManager.FindItem(character, AssignedItemID);
            if (item != null)
                AssignItemSilent(item);
        }

        // ── Item management ────────────────────────────────

        public void AssignItem(Item item)
        {
            AssignedItem = item;
            AssignedItemID = item != null ? item.ItemID : -1;
            if (!IsDynamic)
                SetBaseItemIdOnly(AssignedItemID);
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
            AssignedItemID = item != null ? item.ItemID : -1;
            UpdateIcon();
            StartTracking();
        }

        public void ClearSlot()
        {
            AssignedItem = null;
            AssignedItemID = -1;
            if (!IsDynamic)
                SetBaseItemIdOnly(-1);
            UpdateIcon();
            StopTracking();

            var manager = GetComponentInParent<ActionBarManager>();
            if (manager != null) manager.SaveSlots();
        }

        /// <summary>Clears without triggering save (used during preset apply).</summary>
        public void ClearSlotSilent()
        {
            AssignedItem = null;
            AssignedItemID = -1;
            UpdateIcon();
            StopTracking();
        }

        public void SetAssignedItemIdOnly(int itemID)
        {
            AssignedItemID = itemID > 0 ? itemID : -1;
        }

        public void SetBaseItemIdOnly(int itemID)
        {
            BaseItemID = itemID > 0 ? itemID : -1;
        }

        // ── Cooldown & count tracking ───────────────────────

        private void StartTracking()
        {
            StopTracking();
            if (AssignedItem == null) return;

            if (AssignedItem is Skill skill)
            {
                EnsureCooldownLabel();
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
            if (_cooldownLabel != null)
                _cooldownLabel.text = "";
            if (_iconImage != null)
                _iconImage.color = AssignedItem != null ? Color.white : Color.clear;

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
            while (true)
            {
                if (skill != null && skill.InCooldown())
                {
                    float remaining = skill.RealCooldown * (1f - skill.CoolDownProgress);
                    int seconds = Mathf.CeilToInt(remaining);
                    _cooldownLabel.text = seconds > 0 ? seconds.ToString() : "";
                }
                else
                {
                    _cooldownLabel.text = "";
                }
                yield return new WaitForSeconds(0.1f);
            }
        }

        private int GetItemCount(Character character)
        {
            if (AssignedItem == null) return 0;

            if (AssignedItem is Skill skill && skill.RequiredItems != null && skill.RequiredItems.Length > 0)
                return character.Inventory.ItemCount(skill.RequiredItems[0].Item.ItemID);

            if (AssignedItem.GroupItemInDisplay || AssignedItem.IsStackable)
                return character.Inventory.ItemCount(AssignedItem.ItemID);

            return AssignedItem.QuickSlotCountDisplay;
        }

        private void UpdateCountLabel()
        {
            if (_countLabel == null) return;
            var character = CharacterManager.Instance?.GetFirstLocalCharacter();
            if (character?.Inventory == null) { _countLabel.text = ""; return; }

            int count = GetItemCount(character);
            bool hasRequiredItems = AssignedItem is Skill skill
                && skill.RequiredItems != null && skill.RequiredItems.Length > 0;
            _countLabel.text = count > 0 ? count.ToString() : (hasRequiredItems ? "0" : "");

            if (_iconImage != null)
            {
                bool onCooldown = AssignedItem is Skill s && s.InCooldown();
                bool noAmmo = hasRequiredItems && count <= 0;
                bool equipped = AssignedItem is Equipment eq && eq.IsEquipped;
                _iconImage.color = (onCooldown || noAmmo || equipped) ? new Color(0.3f, 0.3f, 0.3f, 1f) : Color.white;
            }
        }

        private IEnumerator TrackCount()
        {
            while (true)
            {
                UpdateCountLabel();
                yield return new WaitForSeconds(0.5f);
            }
        }

        private IEnumerator RefreshCountDelayed()
        {
            yield return new WaitForSeconds(0.3f);
            UpdateCountLabel();
        }

        // ── Keybind management ─────────────────────────────

        private void SetKeybind(KeyCode key)
        {
            if (BarIndex >= Plugin.MAX_BARS || SlotIndex >= Plugin.MAX_SLOTS_PER_BAR) return;

            if (key != KeyCode.None)
            {
                for (int b = 0; b < Plugin.MAX_BARS; b++)
                    for (int s = 0; s < Plugin.MAX_SLOTS_PER_BAR; s++)
                        if ((b != BarIndex || s != SlotIndex) && Plugin.GetBoundKey(b, s) == key)
                        {
                            Plugin.SetBoundKey(b, s, KeyCode.None);
                            Plugin.Log.LogMessage($"Bar {b + 1} Slot {s + 1}: unbound '{key}'.");
                        }
            }

            Plugin.SetBoundKey(BarIndex, SlotIndex, key);

            foreach (var handler in FindObjectsOfType<SlotDropHandler>())
                handler.UpdateKeyLabel();

            Plugin.Log.LogMessage($"Bar {BarIndex + 1} Slot {SlotIndex + 1}: bound to '{key}'.");
        }

        public void UpdateKeyLabel()
        {
            EnsureKeyLabel();
            if (BarIndex >= Plugin.MAX_BARS || SlotIndex >= Plugin.MAX_SLOTS_PER_BAR)
            {
                _keyLabel.text = "";
                return;
            }
            var key = Plugin.GetBoundKey(BarIndex, SlotIndex);
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
                case KeyCode.BackQuote: return "`";
                case KeyCode.LeftBracket: return "[";
                case KeyCode.RightBracket: return "]";
                case KeyCode.Backslash: return "\\";
                case KeyCode.Semicolon: return ";";
                case KeyCode.Quote: return "'";
                case KeyCode.Comma: return ",";
                case KeyCode.Period: return ".";
                case KeyCode.Slash: return "/";
                case KeyCode.LeftShift: return "LShift";
                case KeyCode.RightShift: return "RShift";
                case KeyCode.LeftControl: return "LCtrl";
                case KeyCode.RightControl: return "RCtrl";
                case KeyCode.LeftAlt: return "LAlt";
                case KeyCode.RightAlt: return "RAlt";
                case KeyCode.Space: return "Space";
                case KeyCode.Return: return "Enter";
                case KeyCode.KeypadEnter: return "NumEnt";
                case KeyCode.Mouse0: return "M1";
                case KeyCode.Mouse1: return "M2";
                case KeyCode.Mouse2: return "M3";
                case KeyCode.Mouse3: return "M4";
                case KeyCode.Mouse4: return "M5";
                case KeyCode.Mouse5: return "M6";
                case KeyCode.Mouse6: return "M7";
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
            if (_cooldownLabel != null)
                _cooldownLabel.transform.SetAsLastSibling();
            if (_keyLabel != null)
                _keyLabel.transform.SetAsLastSibling();
            if (_countLabel != null)
                _countLabel.transform.SetAsLastSibling();
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
            rect.offsetMin = new Vector2(2f, 2f);
            rect.offsetMax = new Vector2(-2f, -2f);
        }

        public void RefreshFontSizes()
        {
            if (_cooldownLabel != null)
            {
                int cdSize = Plugin.CooldownFontSize.Value;
                _cooldownLabel.fontSize = cdSize;
            }
            if (_countLabel != null)
            {
                _countLabel.fontSize = Plugin.LabelFontSize.Value;
            }
            if (_keyLabel != null)
            {
                _keyLabel.fontSize = Plugin.LabelFontSize.Value;
            }
        }

        private void EnsureCooldownLabel()
        {
            if (_cooldownLabel != null) return;

            var go = new GameObject("CooldownLabel");
            go.layer = 5;
            go.transform.SetParent(transform, false);

            int cdSize = Plugin.CooldownFontSize.Value;
            _cooldownLabel = go.AddComponent<Text>();
            _cooldownLabel.font = Font.CreateDynamicFontFromOSFont("Arial", cdSize);
            _cooldownLabel.fontSize = cdSize;
            _cooldownLabel.fontStyle = FontStyle.Bold;
            _cooldownLabel.alignment = TextAnchor.MiddleCenter;
            _cooldownLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            _cooldownLabel.verticalOverflow = VerticalWrapMode.Overflow;
            _cooldownLabel.color = Color.white;
            _cooldownLabel.raycastTarget = false;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);

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

            int lblSize = Plugin.LabelFontSize.Value;
            _countLabel = go.AddComponent<Text>();
            _countLabel.font = Font.CreateDynamicFontFromOSFont("Arial", lblSize);
            _countLabel.fontSize = lblSize;
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

            int keySize = Plugin.LabelFontSize.Value;
            _keyLabel = go.AddComponent<Text>();
            _keyLabel.font      = Font.CreateDynamicFontFromOSFont("Arial", keySize);
            _keyLabel.fontSize  = keySize;
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

        private void EnsureDynamicBorder()
        {
            if (_dynamicBorderEdges != null) return;

            _dynamicBorderEdges = new Image[4];
            float thickness = 2f;

            _dynamicBorderEdges[0] = CreateBorderEdge("DynamicBorderTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(0f, thickness));
            _dynamicBorderEdges[1] = CreateBorderEdge("DynamicBorderBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(0f, thickness));
            _dynamicBorderEdges[2] = CreateBorderEdge("DynamicBorderLeft", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(thickness, 0f));
            _dynamicBorderEdges[3] = CreateBorderEdge("DynamicBorderRight", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(thickness, 0f));

            SetDynamicBorderVisible(false);
        }

        private Image CreateBorderEdge(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var go = new GameObject(name);
            go.layer = 5;
            go.transform.SetParent(transform, false);

            var image = go.AddComponent<Image>();
            image.color = DynamicBorderColor;
            image.raycastTarget = false;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            return image;
        }

        private void SetDynamicBorderVisible(bool visible)
        {
            EnsureDynamicBorder();
            if (_dynamicBorderEdges == null) return;

            for (int i = 0; i < _dynamicBorderEdges.Length; i++)
            {
                if (_dynamicBorderEdges[i] != null)
                    _dynamicBorderEdges[i].enabled = visible;
            }
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

        private static bool IsMenuOpen()
        {
            var character = CharacterManager.Instance?.GetFirstLocalCharacter();
            return character?.CharacterUI != null && character.CharacterUI.IsMenuFocused;
        }

        private ItemDisplay GetDraggedItem(PointerEventData eventData)
        {
            if (eventData?.pointerDrag == null) return null;
            return eventData.pointerDrag.GetComponent<ItemDisplay>();
        }
    }
}
