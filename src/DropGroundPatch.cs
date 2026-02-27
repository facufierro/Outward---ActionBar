using HarmonyLib;

namespace fierrof.ActionBar
{
    /// <summary>
    /// Prevents items and skills from being dropped on the ground when
    /// the cursor is over our action bar, and prevents skills from being
    /// dropped on the ground entirely (skills should never be lost).
    /// </summary>
    [HarmonyPatch(typeof(ItemDisplayDropGround), "IsDropValid")]
    public static class DropGroundPatch
    {
        static void Postfix(ItemDisplay ___m_draggedDisplay, ref bool __result)
        {
            if (!__result) return;

            // Block ground drop when cursor is over an action bar slot
            if (SlotDropHandler.IsPointerOverSlot)
            {
                __result = false;
                return;
            }

            // Never allow skills to be dropped on the ground
            if (___m_draggedDisplay?.RefItem is Skill)
            {
                __result = false;
            }
        }
    }
}
