using HarmonyLib;

namespace fierrof.ActionBar
{
    /// <summary>
    /// Suppresses vanilla quickslot keyboard input so our action bar keybinds
    /// don't conflict with the game's built-in quickslot system.
    /// </summary>
    [HarmonyPatch(typeof(LocalCharacterControl))]
    internal static class QuickSlotPatch
    {
        [HarmonyPatch("UpdateQuickSlotInput")]
        [HarmonyPrefix]
        static bool SuppressQuickSlotInput()
        {
            return false; // Skip vanilla quickslot key processing entirely
        }

        [HarmonyPatch("UpdateQuickSlots")]
        [HarmonyPrefix]
        static bool SuppressQuickSlots()
        {
            return false; // Also skip alternate vanilla quickslot update path
        }
    }
}
