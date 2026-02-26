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
        private Transform     _vanillaBar;
        private GameObject    _canvas;
        private GameObject    _container;
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
            // Independent overlay canvas (same pattern SideLoader uses)
            _canvas = gameObject;
            Object.DontDestroyOnLoad(_canvas);
            _canvas.layer = 5;

            var canvas       = _canvas.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            var scaler = _canvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution  = new Vector2(1920f, 1080f);
            scaler.screenMatchMode      = CanvasScaler.ScreenMatchMode.Shrink;

            _canvas.AddComponent<GraphicRaycaster>();

            // Slot container with horizontal layout
            _container = UIFactory.CreateUIObject("SlotContainer", _canvas);

            UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(
                _container,
                forceWidth: false, forceHeight: false,
                childControlWidth: false, childControlHeight: false,
                spacing: 4
            );

            var fitter = _container.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        }

        // ── Slots ──────────────────────────────────────────────

        private void SyncSlots()
        {
            int target = Plugin.SlotCount.Value;

            // Add missing slots
            while (_slots.Count < target)
            {
                _slots.Add(CreateSlot(_slots.Count));
            }

            // Remove excess slots
            while (_slots.Count > target)
            {
                var last = _slots[_slots.Count - 1];
                _slots.RemoveAt(_slots.Count - 1);
                Destroy(last);
            }

            _lastSlotCount = target;
        }

        private GameObject CreateSlot(int index)
        {
            var slot = UIFactory.CreateUIObject($"Slot_{index}", _container, new Vector2(64f, 64f));

            var bg      = slot.AddComponent<Image>();
            bg.color    = new Color(0.12f, 0.12f, 0.12f, 0.85f);

            var outline         = slot.AddComponent<Outline>();
            outline.effectColor    = new Color(0.6f, 0.6f, 0.6f, 1f);
            outline.effectDistance  = new Vector2(2f, -2f);

            UIFactory.SetLayoutElement(slot,
                minWidth: 64, minHeight: 64,
                preferredWidth: 64, preferredHeight: 64);

            return slot;
        }

        // ── Runtime loop ───────────────────────────────────────

        void Update()
        {
            SuppressVanillaBar();
            ApplyConfig();
        }

        private void SuppressVanillaBar()
        {
            if (_vanillaBar != null && _vanillaBar.gameObject.activeSelf)
                _vanillaBar.gameObject.SetActive(false);
        }

        private void ApplyConfig()
        {
            if (_container == null) return;

            // Re-sync slot count if config changed
            if (Plugin.SlotCount.Value != _lastSlotCount)
                SyncSlots();

            float x     = Plugin.PositionX.Value;
            float y     = Plugin.PositionY.Value;
            float scale = Plugin.Scale.Value;

            // Only touch the RectTransform if something actually changed
            if (x == _lastPosX && y == _lastPosY && scale == _lastScale)
                return;

            var rect = _container.GetComponent<RectTransform>();
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
