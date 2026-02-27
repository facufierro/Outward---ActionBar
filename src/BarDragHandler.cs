using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace fierrof.ActionBar
{
    /// <summary>
    /// Makes a bar container draggable in Edit Mode.
    /// Converts final screen position back to 0-100 config values.
    /// </summary>
    public class BarDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public int BarIndex;
        
        private RectTransform _rect;
        private Canvas _rootCanvas;
        private bool _dragging;
        private bool _hovered;

        void Awake()
        {
            _rect = GetComponent<RectTransform>();
        }

        void Start()
        {
            _rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        }

        public void OnPointerEnter(PointerEventData eventData) => _hovered = true;
        public void OnPointerExit(PointerEventData eventData) => _hovered = false;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!SlotDropHandler.IsEditMode) return;
            if (eventData.button != PointerEventData.InputButton.Left) return;
            _dragging = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || _rootCanvas == null) return;

            // Convert mouse delta to normalized screen coords
            var canvasRect = _rootCanvas.GetComponent<RectTransform>();
            var size = canvasRect.rect.size;

            float dx = eventData.delta.x / size.x;
            float dy = eventData.delta.y / size.y;

            var newAnchor = _rect.anchorMin + new Vector2(dx, dy);
            newAnchor.x = Mathf.Clamp01(newAnchor.x);
            newAnchor.y = Mathf.Clamp01(newAnchor.y);

            _rect.anchorMin = newAnchor;
            _rect.anchorMax = newAnchor;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _dragging = false;

            // Write final position back to config as 0-100 int
            int newX = Mathf.RoundToInt(_rect.anchorMin.x * 100f);
            int newY = Mathf.RoundToInt(_rect.anchorMin.y * 100f);

            Plugin.PositionX[BarIndex].Value = Mathf.Clamp(newX, 0, 100);
            Plugin.PositionY[BarIndex].Value = Mathf.Clamp(newY, 0, 100);

            Plugin.Log.LogMessage($"Bar {BarIndex + 1}: dragged to X={newX}, Y={newY}.");
        }

        void Update()
        {
            // Show move cursor when hovered in edit mode
            if (_hovered && SlotDropHandler.IsEditMode)
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            }
        }
    }
}
