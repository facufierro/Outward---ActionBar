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

            SuppressVanillaBar();
            ApplyConfig();
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
