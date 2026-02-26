using System;
using System.IO;
using System.Linq;

namespace fierrof.ActionBar
{
    /// <summary>
    /// Saves and loads per-character slot assignments.
    /// File: {BepInExConfigDir}/ActionBar_Slots/{characterUID}.txt
    /// Format: one ItemID per line (-1 = empty slot).
    /// </summary>
    public static class SlotSaveManager
    {
        private static string SaveDir =>
            Path.Combine(BepInEx.Paths.ConfigPath, "ActionBar_Slots");

        private static string GetPath(string characterUID) =>
            Path.Combine(SaveDir, $"{characterUID}.txt");

        public static void Save(string characterUID, SlotDropHandler[] slots)
        {
            // Always save exactly 80 lines (4 bars * 20 slots max)
            var lines = Enumerable.Repeat("-1", Plugin.MAX_BARS * Plugin.MAX_SLOTS).ToArray();

            foreach (var slot in slots)
            {
                if (slot.AssignedItem != null)
                {
                    int lineIndex = slot.BarIndex * Plugin.MAX_SLOTS + slot.SlotIndex;
                    lines[lineIndex] = slot.AssignedItem.ItemID.ToString();
                }
            }

            Directory.CreateDirectory(SaveDir);
            File.WriteAllLines(GetPath(characterUID), lines);
            Plugin.Log.LogMessage($"Saved slots to {GetPath(characterUID)}.");
        }

        /// <summary>Returns true if all saved items were found (or no save exists).</summary>
        public static bool Load(string characterUID, SlotDropHandler[] slots, Character character)
        {
            var path = GetPath(characterUID);
            if (!File.Exists(path)) return true;

            try
            {
                var lines = File.ReadAllLines(path);
                bool allFound = true;

                foreach (var slot in slots)
                {
                    if (slot.AssignedItem != null) continue; // already loaded
                    
                    int lineIndex = slot.BarIndex * Plugin.MAX_SLOTS + slot.SlotIndex;
                    if (lineIndex >= lines.Length) continue;

                    if (!int.TryParse(lines[lineIndex].Trim(), out int itemID) || itemID < 0)
                        continue;

                    var item = FindItem(character, itemID);
                    if (item != null)
                        slot.AssignItem(item);
                    else
                        allFound = false;
                }

                return allFound;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Failed to load slots: {ex.Message}");
                return true; // don't retry on error
            }
        }

        private static Item FindItem(Character character, int itemID)
        {
            var inventory = character.Inventory;

            // Check learned skills
            if (inventory.SkillKnowledge != null)
            {
                foreach (var item in inventory.SkillKnowledge.GetLearnedItems())
                {
                    if (item.ItemID == itemID) return item;
                }
            }

            // Check pouch
            if (inventory.Pouch != null)
            {
                foreach (var item in inventory.Pouch.GetContainedItems())
                {
                    if (item.ItemID == itemID) return item;
                }
            }

            // Check equipped bag contents
            if (inventory.EquippedBag != null)
            {
                var bag = inventory.EquippedBag.Container;
                if (bag != null)
                {
                    foreach (var item in bag.GetContainedItems())
                    {
                        if (item.ItemID == itemID) return item;
                    }
                }
            }

            // Check equipped items (weapons, armor, lantern, etc.)
            if (inventory.Equipment != null)
            {
                foreach (var slot in inventory.Equipment.EquipmentSlots)
                {
                    if (slot?.EquippedItem != null && slot.EquippedItem.ItemID == itemID)
                        return slot.EquippedItem;
                }
            }

            return null;
        }
    }
}
