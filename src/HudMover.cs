using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace fierrof.ActionBar
{
    /// <summary>
    /// Attached to a game HUD element to make it draggable in Edit Mode.
    /// Stores the original position so it can be reset.
    /// </summary>
    public class HudMover : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public string ElementId;
        public bool AnchorBottom;

        private RectTransform _rect;
        private Vector2 _originalAnchoredPos;
        private Vector2 _dragOffset;
        private bool _dragging;

        private GameObject _handleObj;   // contains both highlight + label
        private Canvas _addedCanvas;
        private GraphicRaycaster _addedRaycaster;
        private CanvasGroup _canvasGroup;

        // State tracking to restore after edit mode
        private bool _wasActive;
        private float _originalAlpha = -1f;

        void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _originalAnchoredPos = _rect.anchoredPosition;
        }

        public Vector2 OriginalPosition => _originalAnchoredPos;

        // ── Drag handling ──────────────────────────────────

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!SlotDropHandler.IsEditMode) return;
            if (eventData.button != PointerEventData.InputButton.Left) return;

            _dragging = true;
            _dragOffset = eventData.position - new Vector2(_rect.position.x, _rect.position.y);
        }

        void Update()
        {
            if (SlotDropHandler.IsEditMode)
            {
                // Keep CanvasGroup alpha up (manager handles gameObject.SetActive)
                if (_canvasGroup != null && _canvasGroup.alpha < 0.05f) _canvasGroup.alpha = 1f;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _rect.position = new Vector2(Input.mousePosition.x, Input.mousePosition.y) - _dragOffset;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _dragging = false;

            Plugin.Log.LogMessage($"HUD '{ElementId}': moved to ({_rect.anchoredPosition.x:F1}, {_rect.anchoredPosition.y:F1}).");
            HudMoverManager.Instance?.SavePositions();
        }

        // ── Edit mode visuals ──────────────────────────────

        public void EnableEditVisuals()
        {
            // Add Canvas + GraphicRaycaster so drag events work on this element
            if (_addedCanvas == null)
            {
                _addedCanvas = gameObject.GetComponent<Canvas>();
                if (_addedCanvas == null)
                    _addedCanvas = gameObject.AddComponent<Canvas>();
            }
            // Always force high sort order so our handle is on top
            _addedCanvas.overrideSorting = true;
            _addedCanvas.sortingOrder = 100;

            if (_addedRaycaster == null)
            {
                _addedRaycaster = gameObject.GetComponent<GraphicRaycaster>();
                if (_addedRaycaster == null)
                    _addedRaycaster = gameObject.AddComponent<GraphicRaycaster>();
            }

            // Capture state for restoring
            _wasActive = gameObject.activeSelf;
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup != null)
            {
                _originalAlpha = _canvasGroup.alpha;
            }

            // Force visibility right now
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            if (_canvasGroup != null && _canvasGroup.alpha < 0.05f) _canvasGroup.alpha = 1f;

            // Build the handle (highlight + label in one GameObject)
            if (_handleObj == null)
            {
                _handleObj = new GameObject("HudMover_Handle");
                _handleObj.transform.SetParent(transform, false);
                _handleObj.transform.SetAsLastSibling();

                // ── Yellow highlight background ──
                var img = _handleObj.AddComponent<Image>();
                img.color = new Color(1f, 1f, 0f, 0.15f);
                img.raycastTarget = true; // needed for drag events

                var hRect = _handleObj.GetComponent<RectTransform>();

                hRect.anchorMin = new Vector2(0.5f, 0.5f);
                hRect.anchorMax = new Vector2(0.5f, 0.5f);
                hRect.pivot     = new Vector2(0.5f, 0.5f);

                // For tall tutorial rects, find the actual icon child and match it
                var contentRect = AnchorBottom ? FindContentChild(_rect) : null;
                var sizeRef = contentRect ?? _rect;
                float w = Mathf.Clamp(sizeRef.rect.width + 10f, 60f, 150f);
                float h = Mathf.Clamp(sizeRef.rect.height + 10f, 60f, 150f);
                hRect.sizeDelta = new Vector2(w, h);

                if (contentRect != null)
                    hRect.position = contentRect.position;
                else
                    hRect.anchoredPosition = Vector2.zero;

                // ── Label text sits just above the highlight ──
                var labelGO = new GameObject("Label");
                labelGO.transform.SetParent(_handleObj.transform, false);

                var text = labelGO.AddComponent<Text>();
                text.text = ElementId;
                text.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
                text.fontSize = 12;
                text.alignment = TextAnchor.LowerCenter;
                text.color = Color.yellow;
                text.raycastTarget = false;

                var outline = labelGO.AddComponent<Outline>();
                outline.effectColor = Color.black;
                outline.effectDistance = new Vector2(1f, -1f);

                var labelRect = labelGO.GetComponent<RectTransform>();
                labelRect.anchorMin = new Vector2(0f, 1f);
                labelRect.anchorMax = new Vector2(1f, 1f);
                labelRect.pivot = new Vector2(0.5f, 0f);
                labelRect.anchoredPosition = new Vector2(0f, 2f);
                labelRect.sizeDelta = new Vector2(0f, 16f);

                // ── Visual Proxy (for hidden elements like Backpack/Bandage) ──
                if (contentRect != null)
                {
                    var sourceImage = contentRect.GetComponent<Image>();
                    var sourceRaw = contentRect.GetComponent<RawImage>();

                    if (sourceImage != null && sourceImage.sprite != null)
                    {
                        var proxyGO = new GameObject("VisualProxy");
                        proxyGO.transform.SetParent(_handleObj.transform, false);
                        var proxyImg = proxyGO.AddComponent<Image>();
                        proxyImg.sprite = sourceImage.sprite;
                        proxyImg.color = new Color(1f, 1f, 1f, 0.7f); // slightly transparent
                        proxyImg.raycastTarget = false;

                        var proxyRect = proxyGO.GetComponent<RectTransform>();
                        proxyRect.anchorMin = new Vector2(0.5f, 0.5f);
                        proxyRect.anchorMax = new Vector2(0.5f, 0.5f);
                        proxyRect.pivot = new Vector2(0.5f, 0.5f);
                        proxyRect.sizeDelta = contentRect.rect.size;
                        proxyRect.anchoredPosition = Vector2.zero;
                    }
                    else if (sourceRaw != null && sourceRaw.texture != null)
                    {
                        var proxyGO = new GameObject("VisualProxy_Raw");
                        proxyGO.transform.SetParent(_handleObj.transform, false);
                        var proxyRaw = proxyGO.AddComponent<RawImage>();
                        proxyRaw.texture = sourceRaw.texture;
                        proxyRaw.color = new Color(1f, 1f, 1f, 0.7f); // slightly transparent
                        proxyRaw.raycastTarget = false;

                        var proxyRect = proxyGO.GetComponent<RectTransform>();
                        proxyRect.anchorMin = new Vector2(0.5f, 0.5f);
                        proxyRect.anchorMax = new Vector2(0.5f, 0.5f);
                        proxyRect.pivot = new Vector2(0.5f, 0.5f);
                        proxyRect.sizeDelta = contentRect.rect.size;
                        proxyRect.anchoredPosition = Vector2.zero;
                    }
                }
            }
            _handleObj.SetActive(true);
        }

        public void DisableEditVisuals()
        {
            if (_handleObj != null) _handleObj.SetActive(false);

            // Remove added Canvas/Raycaster to not interfere with game UI
            if (_addedRaycaster != null)
            {
                Destroy(_addedRaycaster);
                _addedRaycaster = null;
            }
            if (_addedCanvas != null)
            {
                Destroy(_addedCanvas);
                _addedCanvas = null;
            }

            // Restore visibility
            if (!_wasActive) gameObject.SetActive(false);
            if (_canvasGroup != null && _originalAlpha >= 0f) _canvasGroup.alpha = _originalAlpha;
        }

        // ── Position management ────────────────────────────

        public void SetPosition(float x, float y)
        {
            _rect.anchoredPosition = new Vector2(x, y);
        }

        public Vector2 GetPosition()
        {
            return _rect.anchoredPosition;
        }

        public void ResetToOriginal()
        {
            _rect.anchoredPosition = _originalAnchoredPos;
        }

        private static RectTransform FindContentChild(RectTransform parent)
        {
            // Walk children recursively, return the first one with an Image component
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i).GetComponent<RectTransform>();
                if (child == null || child.name == "HudMover_Handle") continue;
                if (child.GetComponent<Image>() != null || child.GetComponent<RawImage>() != null)
                    return child;
                var deeper = FindContentChild(child);
                if (deeper != null) return deeper;
            }
            return null;
        }
    }
}
