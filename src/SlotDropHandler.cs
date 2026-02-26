using UnityEngine;
using UnityEngine.EventSystems;

namespace fierrof.ActionBar
{
    /// <summary>
    /// Handles drag-and-drop of items/skills onto an action bar slot.
    /// Attach to each slot GameObject.
    /// </summary>
    public class SlotDropHandler : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public int SlotIndex { get; set; }

        private ItemDisplay _hoveredItem;

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hoveredItem = GetDraggedItem(eventData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hoveredItem = null;
        }

        public void OnDrop(PointerEventData eventData)
        {
            var itemDisplay = GetDraggedItem(eventData);
            if (itemDisplay == null || itemDisplay.RefItem == null)
                return;

            var item = itemDisplay.RefItem;
            if (!item.IsQuickSlotable)
            {
                Plugin.Log.LogMessage($"Item '{item.Name}' is not quickslotable.");
                return;
            }

            var character = CharacterManager.Instance?.GetFirstLocalCharacter();
            if (character == null) return;

            var qsm = character.QuickSlotMngr;
            if (qsm == null) return;

            qsm.SetQuickSlot(SlotIndex, item);
            Plugin.Log.LogMessage($"Assigned '{item.Name}' to slot {SlotIndex}.");
        }

        private ItemDisplay GetDraggedItem(PointerEventData eventData)
        {
            if (eventData?.pointerDrag == null) return null;
            return eventData.pointerDrag.GetComponent<ItemDisplay>();
        }
    }
}
