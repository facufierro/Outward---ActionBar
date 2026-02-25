using HarmonyLib;
using ModifAmorphic.Outward.Logging;
using ModifAmorphic.Outward.Unity.ActionMenus;
using System;

namespace ModifAmorphic.Outward.ActionUI.Patches
{
    [HarmonyPatch(typeof(PauseMenu))]
    internal static class PauseMenuPatches
    {
        private static IModifLogger Logger => LoggerFactory.GetLogger(ModInfo.ModId);

        public static event Action<PauseMenu> AfterRefreshDisplay;

        [HarmonyPatch(nameof(PauseMenu.RefreshDisplay))]
        [HarmonyPostfix]
        private static void RefreshDisplayPostfix(PauseMenu __instance)
        {
            try
            {
#if DEBUG
                Logger.LogTrace($"{nameof(PauseMenu)}::{nameof(RefreshDisplayPostfix)}(): Invoked. Invoking {nameof(AfterRefreshDisplay)}.");
#endif
                AfterRefreshDisplay?.Invoke(__instance);
            }
            catch (Exception ex)
            {
                Logger.LogException($"{nameof(PauseMenu)}::{nameof(RefreshDisplayPostfix)}(): Exception invoking {nameof(AfterRefreshDisplay)}.", ex);
            }
        }
        [HarmonyPatch(nameof(PauseMenu.TogglePause))]
        [HarmonyPrefix]
        private static bool TogglePausePrefix(PauseMenu __instance)
        {
            // If we are in Hotkey Edit Mode, block the vanilla Pause Menu
            var menus = UnityEngine.Object.FindObjectsOfType<PlayerActionMenus>();
            foreach (var menu in menus)
            {
                 var container = menu.GetComponentInChildren<HotbarsContainer>();
                 if (container != null && container.Controller != null)
                 {
                     if (container.Controller.IsInHotkeyEditMode)
                     {
                         return false; 
                     }
                     if (container.Controller.JustExitedHotkeyMode)
                     {
                         container.Controller.JustExitedHotkeyMode = false;
                         return false;
                     }
                 }
            }
            return true;
        }
    }
}
