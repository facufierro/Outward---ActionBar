using System;
using HarmonyLib;

namespace fierrof.ActionBar
{
    /// <summary>
    /// Harmony patches to detect when equipment is equipped or unequipped.
    /// Fires static events that ActionBarManager listens to for dynamic slot updates.
    /// </summary>
    [HarmonyPatch(typeof(Equipment))]
    internal static class EquipmentPatch
    {
        public delegate void EquipmentChangedHandler(Character character);
        public static event EquipmentChangedHandler OnEquipmentChanged;

        [HarmonyPatch("OnEquip")]
        [HarmonyPatch(new Type[] { typeof(Character) })]
        [HarmonyPostfix]
        static void AfterEquip(Equipment __instance, Character _character)
        {
            try
            {
                if (_character == null || _character.OwnerPlayerSys == null || !_character.IsLocalPlayer) return;
                OnEquipmentChanged?.Invoke(_character);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"EquipmentPatch.AfterEquip error: {ex.Message}");
            }
        }

        [HarmonyPatch("OnUnequip")]
        [HarmonyPatch(new Type[] { typeof(Character) })]
        [HarmonyPostfix]
        static void AfterUnequip(Equipment __instance, Character _character)
        {
            try
            {
                if (_character == null || _character.OwnerPlayerSys == null || !_character.IsLocalPlayer) return;
                OnEquipmentChanged?.Invoke(_character);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"EquipmentPatch.AfterUnequip error: {ex.Message}");
            }
        }
    }
}
