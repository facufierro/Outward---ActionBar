using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

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

            // Embed inside the game's own canvas hierarchy so that drag visuals
            // and our bar share the same GraphicRaycaster. We use overrideSorting
            // to control visual layering without affecting raycasting.
            var gameplayPanels = __instance.transform.Find("Canvas/GameplayPanels");

            // Set the game's DropPanel to sortingOrder=0 so drag visuals render
            // at a known layer (matching the old mod's approach).
            var dropPanel = __instance.transform.Find("Canvas/GameplayPanels/Menus/DropPanel");
            if (dropPanel != null)
            {
                var dropCanvas = dropPanel.gameObject.GetComponent<Canvas>();
                if (dropCanvas == null)
                    dropCanvas = dropPanel.gameObject.AddComponent<Canvas>();
                dropPanel.gameObject.GetComponent<GraphicRaycaster>();
                if (dropPanel.gameObject.GetComponent<GraphicRaycaster>() == null)
                    dropPanel.gameObject.AddComponent<GraphicRaycaster>();
                dropCanvas.overrideSorting = true;
                dropCanvas.sortingOrder = 0;
            }

            var actionBar = new GameObject("ActionBar_Root");
            actionBar.transform.SetParent(gameplayPanels, false);

            var behaviour = actionBar.AddComponent<ActionBarManager>();
            behaviour.Setup(vanillaBar);

            Plugin.Log.LogMessage("Custom ActionBar created.");
        }
    }
}
