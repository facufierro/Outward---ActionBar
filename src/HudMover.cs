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

        private RectTransform _rect;
        private Vector2 _originalAnchoredPos;
        private Vector2 _dragOffset;
        private bool _dragging;

        // Visual indicators for edit mode
        private GameObject _labelObj;
        private GameObject _highlightObj;
        private Canvas _addedCanvas;
        private GraphicRaycaster _addedRaycaster;

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
                {
                    _addedCanvas = gameObject.AddComponent<Canvas>();
                    _addedCanvas.overrideSorting = true;
                    _addedCanvas.sortingOrder = 100;
                }
            }

            if (_addedRaycaster == null)
            {
                _addedRaycaster = gameObject.GetComponent<GraphicRaycaster>();
                if (_addedRaycaster == null)
                    _addedRaycaster = gameObject.AddComponent<GraphicRaycaster>();
            }

            // Show label
            if (_labelObj == null)
            {
                _labelObj = new GameObject("HudMover_Label");
                _labelObj.transform.SetParent(transform, false);

                var text = _labelObj.AddComponent<Text>();
                text.text = ElementId;
                text.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
                text.fontSize = 12;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.yellow;
                text.raycastTarget = false;

                var outline = _labelObj.AddComponent<Outline>();
                outline.effectColor = Color.black;
                outline.effectDistance = new Vector2(1f, -1f);

                var labelRect = _labelObj.GetComponent<RectTransform>();
                labelRect.anchorMin = new Vector2(0f, 1f);
                labelRect.anchorMax = new Vector2(1f, 1f);
                labelRect.pivot = new Vector2(0.5f, 0f);
                labelRect.anchoredPosition = new Vector2(0f, 2f);
                labelRect.sizeDelta = new Vector2(0f, 18f);
            }
            _labelObj.SetActive(true);

            // Show highlight border
            if (_highlightObj == null)
            {
                _highlightObj = new GameObject("HudMover_Highlight");
                _highlightObj.transform.SetParent(transform, false);
                _highlightObj.transform.SetAsFirstSibling();

                var img = _highlightObj.AddComponent<Image>();
                img.color = new Color(1f, 1f, 0f, 0.15f); // subtle yellow overlay
                img.raycastTarget = true; // needed for drag events

                var hRect = _highlightObj.GetComponent<RectTransform>();
                hRect.anchorMin = Vector2.zero;
                hRect.anchorMax = Vector2.one;
                hRect.offsetMin = Vector2.zero;
                hRect.offsetMax = Vector2.zero;
            }
            _highlightObj.SetActive(true);
        }

        public void DisableEditVisuals()
        {
            if (_labelObj != null) _labelObj.SetActive(false);
            if (_highlightObj != null) _highlightObj.SetActive(false);

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
    }
}
