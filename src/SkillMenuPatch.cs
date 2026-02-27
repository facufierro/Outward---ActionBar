using HarmonyLib;
using System;

namespace fierrof.ActionBar
{
    /// <summary>
    /// Makes skills draggable in the skill menu.
    /// Patches SkillMenu.OnSectionSelected(int) — after the game populates the
    /// skill list, we set Movable = true on every ItemDisplay so they can be
    /// dragged onto our action bar.
    /// </summary>
    [HarmonyPatch(typeof(SkillMenu), "OnSectionSelected", new Type[] { typeof(int) })]
    public static class SkillMenuPatch
    {
        static void Postfix(ItemListDisplay ___m_skillList)
        {
            if (___m_skillList == null) return;

            var displays = ___m_skillList.GetComponentsInChildren<ItemDisplay>(true);
            for (int i = 0; i < displays.Length; i++)
            {
                displays[i].Movable = true;
            }
        }
    }
}
