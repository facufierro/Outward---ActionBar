using HarmonyLib;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace fierrof.ActionBar
{
    [HarmonyPatch(typeof(CharacterUI), "Start")]
    public static class CharacterUIPatch
    {
        static void Postfix(CharacterUI __instance)
        {
            var quickSlotRoot = __instance.transform.Find("Canvas/GameplayPanels/HUD/QuickSlot");
            if (quickSlotRoot == null)
            {
                Plugin.Log.LogWarning("Vanilla quickslot root not found — skipping.");
                return;
            }

            var vanillaBar = quickSlotRoot.Find("Keyboard");
            if (vanillaBar == null)
            {
                Plugin.Log.LogWarning("Vanilla keyboard action bar not found — skipping keyboard suppression.");
                return;
            }

            SuppressVanillaKeyboardQuickSlots(quickSlotRoot, vanillaBar);

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

        private static void SuppressVanillaKeyboardQuickSlots(Transform quickSlotRoot, Transform keyboardBar)
        {
            var switcher = quickSlotRoot.GetComponent("QuickSlotControllerSwitcher");
            if (switcher == null)
                return;

            var switcherType = switcher.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;

            var keyboardField = switcherType.GetField("m_keyboardQuickSlots", flags);
            if (keyboardField == null)
                return;

            var keyboardGroup = keyboardField.GetValue(switcher) as CanvasGroup;
            if (keyboardGroup != null)
            {
                keyboardGroup.gameObject.SetActive(false);
                keyboardGroup.alpha = 0f;
                keyboardGroup.blocksRaycasts = false;
                keyboardGroup.interactable = false;
            }

            var dummyObj = new GameObject("DummyKeyboardQuickSlots");
            dummyObj.layer = 5;
            dummyObj.transform.SetParent(quickSlotRoot, false);
            var dummyGroup = dummyObj.AddComponent<CanvasGroup>();
            dummyGroup.alpha = 0f;
            dummyGroup.blocksRaycasts = false;
            dummyGroup.interactable = false;

            keyboardField.SetValue(switcher, dummyGroup);

            keyboardBar.gameObject.SetActive(false);
        }
    }
}
