using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace fierrof.ActionBar
{
    [HarmonyPatch]
    internal static class ControlsInputQuickSlotPatch
    {
        private static readonly string[] QuickSlotBoolMethods =
        {
            nameof(ControlsInput.QuickSlot1),
            nameof(ControlsInput.QuickSlot2),
            nameof(ControlsInput.QuickSlot3),
            nameof(ControlsInput.QuickSlot4),
            nameof(ControlsInput.QuickSlotInstant1),
            nameof(ControlsInput.QuickSlotInstant2),
            nameof(ControlsInput.QuickSlotInstant3),
            nameof(ControlsInput.QuickSlotInstant4),
            nameof(ControlsInput.QuickSlotInstant5),
            nameof(ControlsInput.QuickSlotInstant6),
            nameof(ControlsInput.QuickSlotInstant7),
            nameof(ControlsInput.QuickSlotInstant8),
            nameof(ControlsInput.QuickSlotItem1),
            nameof(ControlsInput.QuickSlotItem2),
            nameof(ControlsInput.QuickSlotItem3),
            nameof(ControlsInput.QuickSlotToggle1),
            nameof(ControlsInput.QuickSlotToggle2),
            nameof(ControlsInput.QuickSlotToggled)
        };

        static IEnumerable<MethodBase> TargetMethods()
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            return typeof(ControlsInput)
                .GetMethods(flags)
                .Where(m => m.ReturnType == typeof(bool) && QuickSlotBoolMethods.Contains(m.Name));
        }

        [HarmonyPrefix]
        static bool SuppressAllQuickSlotChecks(ref bool __result)
        {
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(ControlsInput), "SetQuickSlotActive")]
    internal static class ControlsInputActivationPatch
    {
        [HarmonyPrefix]
        static void ForceVanillaKeyboardQuickSlotsInactive(int _playerID, ref bool _active)
        {
            // Old-mod pattern: keep quickslot system inactive.
            if (ControlsInput.IsLastActionGamepad(_playerID))
                return;
            _active = false;
        }
    }

    [HarmonyPatch(typeof(LocalCharacterControl), "UpdateQuickSlots")]
    internal static class LocalCharacterControlPatch
    {
        [HarmonyPrefix]
        static bool SuppressUpdateQuickSlots()
        {
            return false;
        }
    }

    [HarmonyPatch]
    internal static class CharacterQuickSlotManagerExecutionPatch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            return typeof(CharacterQuickSlotManager)
                .GetMethods(flags)
                .Where(m => m.Name == nameof(CharacterQuickSlotManager.QuickSlotInput)
                         || m.Name == "DelayedInput"
                         || m.Name == nameof(CharacterQuickSlotManager.SetQuickSlot)
                         || m.Name == nameof(CharacterQuickSlotManager.SetItemQuickSlot)
                         || m.Name == nameof(CharacterQuickSlotManager.OnAssigningQuickSlot));
        }

        [HarmonyPrefix]
        static bool SuppressQuickSlotExecution()
        {
            return false;
        }
    }

    [HarmonyPatch(typeof(CharacterQuickSlotManager), nameof(CharacterQuickSlotManager.RefreshQuickSlots))]
    internal static class CharacterQuickSlotManagerRefreshPatch
    {
        [HarmonyPostfix]
        static void ClearVanillaQuickSlots(CharacterQuickSlotManager __instance)
        {
            int count = __instance.QuickSlotCount;
            for (int index = 0; index < count; index++)
            {
                __instance.ClearQuickSlot(index);
            }
        }
    }
}
