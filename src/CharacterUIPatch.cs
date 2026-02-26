using HarmonyLib;
using UnityEngine;

namespace fierrof.ActionBar
{
    [HarmonyPatch(typeof(CharacterUI), "Start")]
    public static class CharacterUIPatch
    {
        static void Postfix(CharacterUI __instance)
        {
            var vanillaBar = __instance.transform.Find("Canvas/GameplayPanels/HUD/QuickSlot/Keyboard");
            if (vanillaBar == null)
            {
                Plugin.Log.LogWarning("Vanilla action bar not found — skipping.");
                return;
            }

            vanillaBar.gameObject.SetActive(false);

            var actionBar = new GameObject("ActionBar_Root");
            var behaviour = actionBar.AddComponent<ActionBarManager>();
            behaviour.Setup(vanillaBar);

            Plugin.Log.LogMessage("Custom ActionBar created.");
        }
    }
}
