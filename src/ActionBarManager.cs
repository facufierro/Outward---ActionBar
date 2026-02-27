using System.Collections.Generic;
using System.Linq;
using SideLoader.UI;
using UnityEngine;
using UnityEngine.UI;

namespace fierrof.ActionBar
{
    /// <summary>
    /// Owns the custom action bar lifecycle.
    /// Embedded inside CharacterUI's Canvas/GameplayPanels so that drag visuals
    /// and our bar share the same GraphicRaycaster. Uses overrideSorting to
    /// control visual layering only.
    /// </summary>
    public class ActionBarManager : MonoBehaviour
    {
        private const int   SLOT_WIDTH  = 54;
        private const int   SLOT_HEIGHT = 81; // 1.5× taller than wide

        private Transform        _vanillaBar;

        private GameObject[] _containers = new GameObject[Plugin.MAX_BARS];
        private List<GameObject>[] _slots = new List<GameObject>[Plugin.MAX_BARS];

        private int[]   _lastSlotCount = new int[Plugin.MAX_BARS];
        private float[] _lastPosX = new float[Plugin.MAX_BARS];
        private float[] _lastPosY = new float[Plugin.MAX_BARS];
        private float[] _lastScale = new float[Plugin.MAX_BARS];
        private int[]   _lastGap = new int[Plugin.MAX_BARS];
        private int[]   _lastRows = new int[Plugin.MAX_BARS];
        private bool[]  _lastEnabled = new bool[Plugin.MAX_BARS];

        private CanvasGroup      _canvasGroup;
        private Canvas _uiCanvas;

        private GameObject _configOverlay;
        private bool _wasConfigMode;
        private string _loadedCharacterUID;
        private int _draggingBarIndex = -1;
        private Vector2 _dragAnchorOffset;

        // Equipment change tracking
        private bool _equipmentChangePending;
        private float _equipmentChangeDelay;

        // ── Setup ──────────────────────────────────────────────

        public void Setup(Transform vanillaBar)
        {
            _vanillaBar = vanillaBar;

            for (int i = 0; i < Plugin.MAX_BARS; i++)
            {
                _slots[i] = new List<GameObject>();
            }

            // Add the HUD mover manager to this same GameObject
            gameObject.AddComponent<HudMoverManager>();

            BuildUI();

            for (int i = 0; i < Plugin.MAX_BARS; i++)
            {
                SyncSlots(i);
            }

            // Listen for equipment changes
            EquipmentPatch.OnEquipmentChanged += OnEquipmentChanged;
        }

        void OnDestroy()
        {
            EquipmentPatch.OnEquipmentChanged -= OnEquipmentChanged;
        }

        private void OnEquipmentChanged(Character character)
        {
            // Delay by one frame to let equipment state settle
            _equipmentChangePending = true;
            _equipmentChangeDelay = 0.1f;
        }

        // ── UI Build ───────────────────────────────────────────

        private void BuildUI()
        {
            gameObject.layer = 5;

            // RectTransform to fill the parent (GameplayPanels)
            var rt = gameObject.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // overrideSorting makes our bar render ABOVE the DropPanel (0)
            // and above the default game elements, but the drag cursor
            // (which renders on the game's main canvas) stays on top.
            _uiCanvas = gameObject.AddComponent<Canvas>();
            _uiCanvas.overrideSorting = true;
            _uiCanvas.sortingOrder = 1;

            gameObject.AddComponent<GraphicRaycaster>();

            // CanvasGroup for visibility toggling
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;

            for (int i = 0; i < Plugin.MAX_BARS; i++)
            {
                _containers[i] = UIFactory.CreateUIObject($"SlotContainer_Bar{i+1}", gameObject);

                var grid = _containers[i].AddComponent<GridLayoutGroup>();
                grid.cellSize        = new Vector2(SLOT_WIDTH, SLOT_HEIGHT);
                grid.spacing         = new Vector2(Plugin.SlotGap[i].Value, Plugin.SlotGap[i].Value);
                grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = Mathf.Clamp(Plugin.SlotCount[i].Value, 1, Plugin.MAX_SLOTS_PER_ROW);
                grid.startCorner     = GridLayoutGroup.Corner.UpperLeft;
                grid.startAxis       = GridLayoutGroup.Axis.Horizontal;
                grid.childAlignment  = TextAnchor.UpperLeft;

                var fitter           = _containers[i].AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

                // Invisible raycast target so the container itself is draggable
                var containerBg = _containers[i].AddComponent<Image>();
                containerBg.color = new Color(0f, 0f, 0f, 0f);
                containerBg.raycastTarget = true;

                var drag = _containers[i].AddComponent<BarDragHandler>();
                drag.BarIndex = i;

                // Set initial visibility from config so disabled bars don't flash
                bool enabled = Plugin.Enabled[i].Value;
                _containers[i].SetActive(enabled);
                _lastEnabled[i] = enabled;
            }

            BuildConfigOverlay();
        }

        private void BuildConfigOverlay()
        {
            _configOverlay = new GameObject("ConfigOverlay");
            _configOverlay.transform.SetParent(gameObject.transform, false);
            _configOverlay.transform.SetAsFirstSibling(); // Behind the slots

            var rt = _configOverlay.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var bg = _configOverlay.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.7f); // Dark tint
            bg.raycastTarget = false; // Allow clicks to pass through to HUD elements

            var textGO = new GameObject("InstructionText");
            textGO.transform.SetParent(_configOverlay.transform, false);

            var textRt = textGO.AddComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0.6f, 0.7f); // Top right quadrant
            textRt.anchorMax = new Vector2(0.98f, 0.98f); // 2% padding from edge
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var text = textGO.AddComponent<Text>();
            text.font = Font.CreateDynamicFontFromOSFont("Arial", 40);
            text.fontSize = 40;
            text.alignment = TextAnchor.UpperRight; // Align the actual text to the right
            text.color = Color.white;
            text.text = "INSTRUCTIONS\n" +
                        "<size=26>" +
                        "• Open Config (F5) to add/resize bars\n" +
                        "• Drag skills and items into slots to assign them\n" +
                        "• Middle-Click a slot to toggle it Dynamic\n\n" +
                        "EDITOR MODE\n" +
                        "• Hover a slot & press a key to bind it\n" +
                        "• Drag bars and ui elements to move them\n" +
                        "• Right-Click a slot to Hide/Disable it" +
                        "</size>";

            var outline = textGO.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2f, -2f);

            _configOverlay.SetActive(false);
        }

        // ── Slots ──────────────────────────────────────────────

        private void SyncSlots(int barIndex)
        {
            int slotsPerRow = Mathf.Clamp(Plugin.SlotCount[barIndex].Value, 1, Plugin.MAX_SLOTS_PER_ROW);
            int rows = Mathf.Clamp(Plugin.Rows[barIndex].Value, 1, Plugin.MAX_ROWS);
            int target = Mathf.Clamp(slotsPerRow * rows, 1, Plugin.MAX_SLOTS_PER_BAR);

            while (_slots[barIndex].Count < target)
                _slots[barIndex].Add(CreateSlot(barIndex, _slots[barIndex].Count));

            while (_slots[barIndex].Count > target)
            {
                Destroy(_slots[barIndex][_slots[barIndex].Count - 1]);
                _slots[barIndex].RemoveAt(_slots[barIndex].Count - 1);
            }

            var grid = _containers[barIndex]?.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                int gap = Plugin.SlotGap[barIndex].Value;
                grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = slotsPerRow;
                grid.spacing         = new Vector2(gap, gap);
            }

            var rect = _containers[barIndex]?.GetComponent<RectTransform>();
            if (rect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        }

        private GameObject CreateSlot(int barIndex, int slotIndex)
        {
            var slot = UIFactory.CreateUIObject($"Slot_{barIndex}_{slotIndex}", _containers[barIndex],
                           new Vector2(SLOT_WIDTH, SLOT_HEIGHT));

            var bg   = slot.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.12f, 0.12f, 0.85f);

            var outline            = slot.AddComponent<Outline>();
            outline.effectColor    = Color.black;
            outline.effectDistance = new Vector2(2f, -2f);

            UIFactory.SetLayoutElement(slot,
                minWidth: SLOT_WIDTH,       minHeight: SLOT_HEIGHT,
                preferredWidth: SLOT_WIDTH, preferredHeight: SLOT_HEIGHT);

            var dropHandler = slot.AddComponent<SlotDropHandler>();
            dropHandler.BarIndex = barIndex;
            dropHandler.SlotIndex = slotIndex;

            return slot;
        }

        // ── Runtime loop ───────────────────────────────────────

        void Update()
        {
            bool inGameplay = NetworkLevelLoader.Instance != null
                           && !NetworkLevelLoader.Instance.IsGameplayPaused
                           && !NetworkLevelLoader.Instance.IsGameplayLoading
                           && CharacterManager.Instance?.GetFirstLocalCharacter() != null;

            if (!inGameplay)
            {
                if (_canvasGroup.alpha != 0f)
                {
                    _canvasGroup.alpha = 0f;
                    _canvasGroup.blocksRaycasts = false;
                }
                return;
            }

            float targetAlpha = 1f;
            if (_canvasGroup.alpha != targetAlpha)
            {
                _canvasGroup.alpha = targetAlpha;
                _canvasGroup.blocksRaycasts = true;
            }

            TryLoadSlots();
            HandleConfigModeState();
            HandleBarDragging();
            HandleEquipmentChange();

            SuppressVanillaBar();
            ApplyConfig();
        }

        // ── Equipment change handling ────────────────────────

        private void HandleEquipmentChange()
        {
            if (!_equipmentChangePending) return;

            _equipmentChangeDelay -= Time.unscaledDeltaTime;
            if (_equipmentChangeDelay > 0f) return;

            _equipmentChangePending = false;

            var character = CharacterManager.Instance?.GetFirstLocalCharacter();
            if (character == null) return;

            ApplyDynamicPresets(character);
        }

        /// <summary>
        /// Applies dynamic presets for current weapon context.
        /// Called on equipment change and after initial slot load.
        /// </summary>
        public void ApplyDynamicPresets(Character character)
        {
            if (character == null) return;
            if (!DynamicPresetManager.HasContextChanged(character)) return;

            DynamicPresetManager.EnsureLoaded(character.UID);

            var resolveKeys = DynamicPresetManager.GetResolveKeys(character);
            var handlers = GetAllSlotHandlers();
            bool anyChanged = false;

            foreach (var handler in handlers)
            {
                if (!handler.IsDynamic) continue;

                if (DynamicPresetManager.ResolvePreset(resolveKeys, handler.BarIndex, handler.SlotIndex,
                    out var entry))
                {
                    if (entry.ItemID <= 0)
                    {
                        // Preset says empty
                        if (handler.AssignedItem != null)
                        {
                            handler.ClearSlotSilent();
                            anyChanged = true;
                        }
                    }
                    else
                    {
                        // Already has the right item?
                        if (handler.AssignedItem != null && handler.AssignedItem.ItemID == entry.ItemID)
                            continue;

                        var item = SlotSaveManager.FindItemStatic(character, entry.ItemID);
                        if (item != null)
                        {
                            handler.AssignItemSilent(item);
                            anyChanged = true;
                        }
                        else if (handler.AssignedItem != null)
                        {
                            // Resolved entry references an unavailable item; do not keep stale assignment.
                            handler.ClearSlotSilent();
                            anyChanged = true;
                        }
                    }
                }
                else
                {
                    // No preset at all for this context chain: revert to empty.
                    if (handler.AssignedItem != null)
                    {
                        handler.ClearSlotSilent();
                        anyChanged = true;
                    }
                }
            }

            if (anyChanged)
                SaveSlots();
        }

        // ── Slot persistence ──────────────────────────────────

        private float _totalLoadTime;
        private float _retryInterval;
        private bool  _slotsLoaded;

        private void TryLoadSlots()
        {
            var character = CharacterManager.Instance?.GetFirstLocalCharacter();
            if (character == null) return;

            string uid = character.UID;

            // New character — reset load state
            if (uid != _loadedCharacterUID)
            {
                _loadedCharacterUID = uid;
                _slotsLoaded = false;
                _totalLoadTime = 0f;
                _retryInterval = 0f;
                DynamicPresetManager.ResetContextSignature();
            }

            if (_slotsLoaded) return;

            _totalLoadTime += Time.unscaledDeltaTime;
            _retryInterval += Time.unscaledDeltaTime;

            // Wait 0.5s between retries
            if (_retryInterval < 0.5f) return;
            _retryInterval = 0f;

            var allHandlers = GetAllSlotHandlers();
            bool allFound = SlotSaveManager.Load(uid, allHandlers, character);

            if (allFound || _totalLoadTime > 10f)
            {
                _slotsLoaded = true;
                if (!allFound)
                    Plugin.Log.LogWarning("Some slotted items could not be found in inventory.");
                else
                    Plugin.Log.LogMessage($"All slots loaded for {uid}.");

                // Load dynamic presets and apply for current weapon context
                DynamicPresetManager.EnsureLoaded(uid);
                DynamicPresetManager.ResetContextSignature(); // Force re-apply
                ApplyDynamicPresets(character);
            }
        }

        public void SaveSlots()
        {
            if (_loadedCharacterUID == null) return;

            var character = CharacterManager.Instance?.GetFirstLocalCharacter();
            if (character == null) return;

            SlotSaveManager.Save(_loadedCharacterUID, GetAllSlotHandlers());
        }

        public SlotDropHandler[] GetAllSlotHandlers()
        {
            var allHandlers = new List<SlotDropHandler>();
            for (int i = 0; i < Plugin.MAX_BARS; i++)
            {
                allHandlers.AddRange(_slots[i]
                    .Select(s => s.GetComponent<SlotDropHandler>())
                    .Where(h => h != null));
            }
            return allHandlers.ToArray();
        }

        private void HandleConfigModeState()
        {
            if (SlotDropHandler.IsEditMode && !_wasConfigMode)
            {
                _configOverlay.SetActive(true);
                Time.timeScale = 0f; // Pause game
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                _wasConfigMode = true;
                RefreshAllSlotVisuals();

                // Show semi-transparent black grab handles on action bar containers
                for (int i = 0; i < Plugin.MAX_BARS; i++)
                {
                    if (_containers[i] != null)
                    {
                        var img = _containers[i].GetComponent<Image>();
                        img.color = new Color(0.18f, 0.18f, 0.18f, 0.75f);

                        // Add 4px padding so the handle tint extends beyond the slots
                        var layout = _containers[i].GetComponent<GridLayoutGroup>();
                        layout.padding = new RectOffset(4, 4, 4, 4);
                    }
                }
            }
            else if (!SlotDropHandler.IsEditMode && _wasConfigMode)
            {
                _configOverlay.SetActive(false);
                Time.timeScale = 1f; // Resume game
                Cursor.lockState = CursorLockMode.Confined;
                _wasConfigMode = false;
                _draggingBarIndex = -1;
                RefreshAllSlotVisuals();

                // Remove handle tint and restore container size
                for (int i = 0; i < Plugin.MAX_BARS; i++)
                {
                    if (_containers[i] != null)
                    {
                        _containers[i].GetComponent<Image>().color = Color.clear;
                        var layout = _containers[i].GetComponent<GridLayoutGroup>();
                        layout.padding = new RectOffset(0, 0, 0, 0);
                    }
                }
            }

            // Global ESC to exit config mode
            if (SlotDropHandler.IsEditMode && Input.GetKeyDown(KeyCode.Escape))
            {
                SlotDropHandler.IsEditMode = false;
            }
        }

        private void HandleBarDragging()
        {
            if (!SlotDropHandler.IsEditMode)
            {
                _draggingBarIndex = -1;
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                Vector2 mousePosition = Input.mousePosition;
                _draggingBarIndex = GetBarIndexUnderMouse(mousePosition);

                if (_draggingBarIndex >= 0 && _containers[_draggingBarIndex] != null)
                {
                    var barRect = _containers[_draggingBarIndex].GetComponent<RectTransform>();
                    if (barRect != null && TryGetMouseNormalized(mousePosition, out var mouseNorm))
                    {
                        _dragAnchorOffset = barRect.anchorMin - mouseNorm;
                    }
                    else
                    {
                        _draggingBarIndex = -1;
                    }
                }
            }

            if (_draggingBarIndex < 0) return;

            if (Input.GetMouseButton(0))
            {
                Vector2 mousePosition = Input.mousePosition;
                if (!TryGetMouseNormalized(mousePosition, out var mouseNorm)) return;

                var barRect = _containers[_draggingBarIndex].GetComponent<RectTransform>();
                if (barRect == null) return;

                Vector2 anchor = mouseNorm + _dragAnchorOffset;
                anchor.x = Mathf.Clamp01(anchor.x);
                anchor.y = Mathf.Clamp01(anchor.y);

                barRect.anchorMin = anchor;
                barRect.anchorMax = anchor;
                barRect.anchoredPosition = Vector2.zero;
            }

            if (Input.GetMouseButtonUp(0))
            {
                if (_draggingBarIndex >= 0 && _containers[_draggingBarIndex] != null)
                {
                    var barRect = _containers[_draggingBarIndex].GetComponent<RectTransform>();
                    if (barRect != null)
                    {
                        int newX = Mathf.RoundToInt(barRect.anchorMin.x * 100f);
                        int newY = Mathf.RoundToInt(barRect.anchorMin.y * 100f);

                        Plugin.PositionX[_draggingBarIndex].Value = Mathf.Clamp(newX, 0, 100);
                        Plugin.PositionY[_draggingBarIndex].Value = Mathf.Clamp(newY, 0, 100);
                    }
                }

                _draggingBarIndex = -1;
            }
        }

        private int GetBarIndexUnderMouse(Vector2 mousePosition)
        {
            for (int i = Plugin.MAX_BARS - 1; i >= 0; i--)
            {
                if (_containers[i] == null || !_containers[i].activeInHierarchy) continue;

                var rect = _containers[i].GetComponent<RectTransform>();
                if (rect == null) continue;

                var eventCamera = _uiCanvas != null ? _uiCanvas.worldCamera : null;
                if (RectTransformUtility.RectangleContainsScreenPoint(rect, mousePosition, eventCamera))
                    return i;
            }

            return -1;
        }

        private bool TryGetMouseNormalized(Vector2 mousePosition, out Vector2 normalized)
        {
            normalized = Vector2.zero;

            var rootRect = GetComponent<RectTransform>();
            if (rootRect == null) return false;

            var eventCamera = _uiCanvas != null ? _uiCanvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, mousePosition, eventCamera, out var localPoint))
                return false;

            var rect = rootRect.rect;
            if (rect.width <= 0f || rect.height <= 0f) return false;

            normalized.x = Mathf.Clamp01((localPoint.x - rect.xMin) / rect.width);
            normalized.y = Mathf.Clamp01((localPoint.y - rect.yMin) / rect.height);
            return true;
        }

        private void RefreshAllSlotVisuals()
        {
            foreach (var handler in GetAllSlotHandlers())
            {
                handler.RefreshEditModeVisuals();
            }
        }

        private void SuppressVanillaBar()
        {
            if (_vanillaBar == null) return;

            if (_vanillaBar.gameObject.activeSelf)
                _vanillaBar.gameObject.SetActive(false);

            var parent = _vanillaBar.parent;
            if (parent != null && parent.gameObject.activeSelf)
                parent.gameObject.SetActive(false);
        }

        private void ApplyConfig()
        {
            for (int i = 0; i < Plugin.MAX_BARS; i++)
            {
                if (_containers[i] == null) continue;

                bool enabled = Plugin.Enabled[i].Value;
                if (enabled != _lastEnabled[i])
                {
                    _containers[i].SetActive(enabled);
                    _lastEnabled[i] = enabled;
                }

                if (!enabled) continue;

                int slotsPerRow = Mathf.Clamp(Plugin.SlotCount[i].Value, 1, Plugin.MAX_SLOTS_PER_ROW);
                int rows = Mathf.Clamp(Plugin.Rows[i].Value, 1, Plugin.MAX_ROWS);
                int targetSlots = Mathf.Clamp(slotsPerRow * rows, 1, Plugin.MAX_SLOTS_PER_BAR);

                if (_slots[i].Count != targetSlots)
                    SyncSlots(i);

                float x     = Plugin.PositionX[i].Value / 100f;
                float y     = Plugin.PositionY[i].Value / 100f;
                float scale = Plugin.Scale[i].Value / 100f;
                int   gap   = Plugin.SlotGap[i].Value;

                // Update gap/rows if changed
                if (gap != _lastGap[i] || rows != _lastRows[i] || slotsPerRow != _lastSlotCount[i])
                {
                    var grid = _containers[i].GetComponent<GridLayoutGroup>();
                    if (grid != null)
                    {
                        grid.spacing         = new Vector2(gap, gap);
                        grid.constraintCount = slotsPerRow;
                    }
                    _lastGap[i]  = gap;
                    _lastRows[i] = rows;
                    _lastSlotCount[i] = slotsPerRow;
                }

                var rect              = _containers[i].GetComponent<RectTransform>();

                if (i == _draggingBarIndex)
                {
                    _lastPosX[i] = rect.anchorMin.x;
                    _lastPosY[i] = rect.anchorMin.y;
                    _lastScale[i] = scale;
                    continue;
                }

                rect.anchorMin        = new Vector2(x, y);
                rect.anchorMax        = new Vector2(x, y);
                rect.pivot            = new Vector2(0.5f, 0f); // Default to center-bottom alignment to keep slots centered
                rect.anchoredPosition = Vector2.zero;
                rect.localScale       = new Vector3(scale, scale, 1f);

                _lastPosX[i]  = x;
                _lastPosY[i]  = y;
                _lastScale[i] = scale;
            }
        }
    }
}
