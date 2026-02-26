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
        private bool[]  _lastEnabled = new bool[Plugin.MAX_BARS];

        private CanvasGroup      _canvasGroup;
        
        private GameObject _configOverlay;
        private bool _wasConfigMode;
        private string _loadedCharacterUID;

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
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1;

            gameObject.AddComponent<GraphicRaycaster>();

            // CanvasGroup for visibility toggling
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;

            for (int i = 0; i < Plugin.MAX_BARS; i++)
            {
                _containers[i] = UIFactory.CreateUIObject($"SlotContainer_Bar{i+1}", gameObject);

                UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(
                    _containers[i],
                    forceWidth: false, forceHeight: false,
                    childControlWidth: false, childControlHeight: false,
                    spacing: Plugin.SlotGap[i].Value
                );    

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
            textRt.anchorMin = new Vector2(0f, 0.8f);
            textRt.anchorMax = new Vector2(1f, 1f);
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var text = textGO.AddComponent<Text>();
            text.font = Font.CreateDynamicFontFromOSFont("Arial", 36);
            text.fontSize = 36;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = "EDIT MODE\n<size=24>Hover a slot and press a key to bind it.\nDrag a bar to reposition it. Press ESC to exit.</size>";

            var outline = textGO.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2f, -2f);

            _configOverlay.SetActive(false);
        }

        // ── Slots ──────────────────────────────────────────────

        private void SyncSlots(int barIndex)
        {
            int target = Plugin.SlotCount[barIndex].Value;

            while (_slots[barIndex].Count < target)
                _slots[barIndex].Add(CreateSlot(barIndex, _slots[barIndex].Count));

            while (_slots[barIndex].Count > target)
            {
                Destroy(_slots[barIndex][_slots[barIndex].Count - 1]);
                _slots[barIndex].RemoveAt(_slots[barIndex].Count - 1);
            }

            _lastSlotCount[barIndex] = target;
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

            SuppressVanillaBar();
            ApplyConfig();
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

                // Show yellow grab handles on action bar containers
                for (int i = 0; i < Plugin.MAX_BARS; i++)
                {
                    if (_containers[i] != null)
                    {
                        var img = _containers[i].GetComponent<Image>();
                        img.color = new Color(1f, 1f, 0f, 0.15f);

                        // Add 4px overflow border so the bar is easy to grab
                        var crt = _containers[i].GetComponent<RectTransform>();
                        crt.offsetMin = crt.offsetMin - new Vector2(4f, 4f);
                        crt.offsetMax = crt.offsetMax + new Vector2(4f, 4f);
                    }
                }
            }
            else if (!SlotDropHandler.IsEditMode && _wasConfigMode)
            {
                _configOverlay.SetActive(false);
                Time.timeScale = 1f; // Resume game
                Cursor.lockState = CursorLockMode.Confined;
                _wasConfigMode = false;
                RefreshAllSlotVisuals();

                // Remove yellow handles and restore container size
                for (int i = 0; i < Plugin.MAX_BARS; i++)
                {
                    if (_containers[i] != null)
                    {
                        _containers[i].GetComponent<Image>().color = Color.clear;
                        var crt = _containers[i].GetComponent<RectTransform>();
                        crt.offsetMin = crt.offsetMin + new Vector2(4f, 4f);
                        crt.offsetMax = crt.offsetMax - new Vector2(4f, 4f);
                    }
                }
            }

            // Global ESC to exit config mode
            if (SlotDropHandler.IsEditMode && Input.GetKeyDown(KeyCode.Escape))
            {
                SlotDropHandler.IsEditMode = false;
            }
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

                if (Plugin.SlotCount[i].Value != _lastSlotCount[i])
                    SyncSlots(i);

                float x     = Plugin.PositionX[i].Value / 100f;
                float y     = Plugin.PositionY[i].Value / 100f;
                float scale = Plugin.Scale[i].Value / 100f;
                int   gap   = Plugin.SlotGap[i].Value;

                // Update gap if changed
                if (gap != _lastGap[i])
                {
                    var layout = _containers[i].GetComponent<HorizontalLayoutGroup>();
                    if (layout != null) layout.spacing = gap;
                    _lastGap[i] = gap;
                }

                if (x == _lastPosX[i] && y == _lastPosY[i] && scale == _lastScale[i])
                    continue;

                var rect              = _containers[i].GetComponent<RectTransform>();
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
