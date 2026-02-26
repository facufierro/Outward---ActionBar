using HarmonyLib;

namespace fierrof.ActionBar
{
    /// <summary>
    /// When IsMenuFocused returns true the game shows the cursor and stops
    /// locking it. We force it true during our hotkey config mode so the
    /// player can hover over action bar slots to assign keys.
    /// </summary>
    [HarmonyPatch(typeof(CharacterUI), "IsMenuFocused", MethodType.Getter)]
    public static class CursorUnlockPatch
    {
        static void Postfix(ref bool __result)
        {
            if (SlotDropHandler.IsConfigMode)
                __result = true;
        }
    }
}
