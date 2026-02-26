using System.Collections.Generic;
using SideLoader.UI;
using UnityEngine;
using UnityEngine.UI;

namespace fierrof.ActionBar
{
    /// <summary>
    /// Owns the entire custom action bar lifecycle:
    /// canvas setup, slot creation/destruction, config sync, and vanilla suppression.
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
            BuildCanvas();
            SyncSlots();
        }

        // ── Canvas ─────────────────────────────────────────────

        private void BuildCanvas()
        {
            Object.DontDestroyOnLoad(gameObject);
            gameObject.layer = 5;

            var canvas          = gameObject.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -1; // Below game UI so dragged items render on top

            var scaler                 = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.Shrink;

            gameObject.AddComponent<GraphicRaycaster>();

            // CanvasGroup to toggle visibility without disabling the GameObject
            // (disabling the GO would stop Update from running)
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

            // Toggle visibility via alpha (not SetActive, which would kill Update)
            float targetAlpha = inGameplay ? 1f : 0f;
            if (_canvasGroup.alpha != targetAlpha)
            {
                _canvasGroup.alpha = targetAlpha;
                _canvasGroup.blocksRaycasts = inGameplay;
            }

            if (!inGameplay) return;

            SuppressVanillaBar();
            ApplyConfig();
        }

        private void SuppressVanillaBar()
        {
            if (_vanillaBar == null) return;

            // Disable the direct target
            if (_vanillaBar.gameObject.activeSelf)
                _vanillaBar.gameObject.SetActive(false);

            // Also kill the parent QuickSlot container — the game may re-enable children
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
