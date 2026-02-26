using System.Collections.Generic;
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
        private const int   SLOT_GAP    = 4;

        private Transform        _vanillaBar;
        private GameObject       _container;
        private CanvasGroup      _canvasGroup;
        private List<GameObject> _slots = new List<GameObject>();

        private int   _lastSlotCount;
        private float _lastPosX, _lastPosY, _lastScale;
        
        private GameObject _configOverlay;
        private bool _wasConfigMode;

        // ── Setup ──────────────────────────────────────────────

        public void Setup(Transform vanillaBar)
        {
            _vanillaBar = vanillaBar;
            BuildUI();
            SyncSlots();
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

            _container = UIFactory.CreateUIObject("SlotContainer", gameObject);

            UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(
                _container,
                forceWidth: false, forceHeight: false,
                childControlWidth: false, childControlHeight: false,
                spacing: SLOT_GAP
            );

            var fitter           = _container.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

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
            bg.raycastTarget = true; // Block clicks to game world

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
            text.text = "HOTKEY CONFIGURATION MODE\n<size=24>Hover a slot and press a key to bind. Press ESC to exit.</size>";

            var outline = textGO.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2f, -2f);

            _configOverlay.SetActive(false);
        }

        // ── Slots ──────────────────────────────────────────────

        private void SyncSlots()
        {
            int target = Plugin.SlotCount.Value;

            while (_slots.Count < target)
                _slots.Add(CreateSlot(_slots.Count));

            while (_slots.Count > target)
            {
                Destroy(_slots[_slots.Count - 1]);
                _slots.RemoveAt(_slots.Count - 1);
            }

            _lastSlotCount = target;
        }

        private GameObject CreateSlot(int index)
        {
            var slot = UIFactory.CreateUIObject($"Slot_{index}", _container,
                           new Vector2(SLOT_WIDTH, SLOT_HEIGHT));

            var bg   = slot.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.12f, 0.12f, 0.85f);

            var outline            = slot.AddComponent<Outline>();
            outline.effectColor    = new Color(0.6f, 0.6f, 0.6f, 1f);
            outline.effectDistance = new Vector2(2f, -2f);

            UIFactory.SetLayoutElement(slot,
                minWidth: SLOT_WIDTH,       minHeight: SLOT_HEIGHT,
                preferredWidth: SLOT_WIDTH, preferredHeight: SLOT_HEIGHT);

            var dropHandler = slot.AddComponent<SlotDropHandler>();
            dropHandler.SlotIndex = index;

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

            HandleConfigModeState();

            SuppressVanillaBar();
            ApplyConfig();
        }

        private void HandleConfigModeState()
        {
            if (SlotDropHandler.IsConfigMode && !_wasConfigMode)
            {
                _configOverlay.SetActive(true);
                Time.timeScale = 0f; // Pause game
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                _wasConfigMode = true;
            }
            else if (!SlotDropHandler.IsConfigMode && _wasConfigMode)
            {
                _configOverlay.SetActive(false);
                Time.timeScale = 1f; // Resume game
                Cursor.lockState = CursorLockMode.Confined;
                _wasConfigMode = false;
            }

            // Global ESC to exit config mode
            if (SlotDropHandler.IsConfigMode && Input.GetKeyDown(KeyCode.Escape))
            {
                SlotDropHandler.IsConfigMode = false;
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
            if (_container == null) return;

            if (Plugin.SlotCount.Value != _lastSlotCount)
                SyncSlots();

            float x     = Plugin.PositionX.Value;
            float y     = Plugin.PositionY.Value;
            float scale = Plugin.Scale.Value;

            if (x == _lastPosX && y == _lastPosY && scale == _lastScale)
                return;

            var rect              = _container.GetComponent<RectTransform>();
            rect.anchorMin        = new Vector2(x, y);
            rect.anchorMax        = new Vector2(x, y);
            rect.pivot            = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.localScale       = new Vector3(scale, scale, 1f);

            _lastPosX  = x;
            _lastPosY  = y;
            _lastScale = scale;
        }
    }
}
