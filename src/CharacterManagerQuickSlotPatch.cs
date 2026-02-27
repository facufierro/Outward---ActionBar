using HarmonyLib;

namespace fierrof.ActionBar
{
    /// <summary>
    /// Vanilla quickslots are (re)applied by CharacterManager during load/sync.
    /// Immediately clear them so vanilla keybinds have no assigned targets.
    /// </summary>
    [HarmonyPatch(typeof(CharacterManager))]
    internal static class CharacterManagerQuickSlotPatch
    {
        [HarmonyPatch(nameof(CharacterManager.ApplyQuickSlots))]
        [HarmonyPatch(new System.Type[] { typeof(Character) })]
        [HarmonyPostfix]
        static void ClearVanillaQuickSlotsAfterApply(Character _character)
        {
            if (_character == null || !_character.IsLocalPlayer) return;
            if (_character.QuickSlotMngr == null) return;

            int count = _character.QuickSlotMngr.QuickSlotCount;
            for (int index = 0; index < count; index++)
            {
                _character.QuickSlotMngr.ClearQuickSlot(index);
            }
        }
    }
}
