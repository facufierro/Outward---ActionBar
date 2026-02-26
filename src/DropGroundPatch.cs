using HarmonyLib;

namespace fierrof.ActionBar
{
    /// <summary>
    /// Prevents items and skills from being dropped on the ground when
    /// the cursor is over our action bar. Without this, the game's default
    /// drop-to-ground logic fires even when our IDropHandler catches the event.
    /// </summary>
    [HarmonyPatch(typeof(ItemDisplayDropGround), "IsDropValid")]
    public static class DropGroundPatch
    {
        static void Postfix(ref bool __result)
        {
            // If the cursor is currently over one of our action bar slots,
            // block the ground drop entirely.
            if (SlotDropHandler.IsPointerOverSlot)
            {
                __result = false;
            }
        }
    }
}
