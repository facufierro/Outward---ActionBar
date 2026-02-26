using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace fierrof.ActionBar
{
    /// <summary>
    /// Handles drag-and-drop onto a single action bar slot.
    /// When an item or skill is dropped, its icon is displayed in the slot.
    /// Right-click clears the slot.
    /// </summary>
    public class SlotDropHandler : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public int SlotIndex { get; set; }

        /// <summary>True when the pointer is over any action bar slot.</summary>
        public static bool IsPointerOverSlot { get; private set; }

        /// <summary>The item currently assigned to this slot (null = empty).</summary>
        public Item AssignedItem { get; private set; }

        private Image _iconImage;
        private Canvas _dragBoostCanvas;

        public void OnPointerEnter(PointerEventData eventData)
        {
            IsPointerOverSlot = true;

            // Boost the dragged item's render order above our bar
            if (eventData.dragging && eventData.pointerDrag != null)
            {
                _dragBoostCanvas = eventData.pointerDrag.GetComponent<Canvas>();
                if (_dragBoostCanvas == null)
                    _dragBoostCanvas = eventData.pointerDrag.AddComponent<Canvas>();
                _dragBoostCanvas.overrideSorting = true;
                _dragBoostCanvas.sortingOrder = 100;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            IsPointerOverSlot = false;
            CleanupDragBoost();
        }

        private void CleanupDragBoost()
        {
            if (_dragBoostCanvas != null)
            {
                Object.Destroy(_dragBoostCanvas);
                _dragBoostCanvas = null;
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            CleanupDragBoost();

            var itemDisplay = GetDraggedItem(eventData);
            if (itemDisplay?.RefItem == null) return;

            var item = itemDisplay.RefItem;
            Plugin.Log.LogMessage($"[Slot {SlotIndex}] OnDrop: item='{item.Name}', quickslotable={item.IsQuickSlotable}");

            if (!item.IsQuickSlotable)
            {
                Plugin.Log.LogMessage($"'{item.Name}' is not quickslotable — ignoring.");
                return;
            }

            AssignItem(item);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Right-click clears the slot
            if (eventData.button == PointerEventData.InputButton.Right && AssignedItem != null)
            {
                Plugin.Log.LogMessage($"Slot {SlotIndex}: cleared '{AssignedItem.Name}'.");
                ClearSlot();
            }
        }

        public void AssignItem(Item item)
        {
            AssignedItem = item;
            UpdateIcon();
            Plugin.Log.LogMessage($"Slot {SlotIndex}: assigned '{item.Name}'.");
        }

        public void ClearSlot()
        {
            AssignedItem = null;
            UpdateIcon();
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
        }

        private void EnsureIconImage()
        {
            if (_iconImage != null) return;

            var iconGO = new GameObject("Icon");
            iconGO.layer = 5;
            iconGO.transform.SetParent(transform, false);

            _iconImage = iconGO.AddComponent<Image>();
            _iconImage.preserveAspect = true;
            _iconImage.raycastTarget  = false;
            _iconImage.enabled        = false;

            var rect = iconGO.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.1f, 0.1f);
            rect.anchorMax = new Vector2(0.9f, 0.9f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private ItemDisplay GetDraggedItem(PointerEventData eventData)
        {
            if (eventData?.pointerDrag == null) return null;
            return eventData.pointerDrag.GetComponent<ItemDisplay>();
        }
    }
}
