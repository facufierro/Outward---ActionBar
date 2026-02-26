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
            var lines = slots.Select(s =>
                s.AssignedItem != null ? s.AssignedItem.ItemID.ToString() : "-1");

            Directory.CreateDirectory(SaveDir);
            File.WriteAllLines(GetPath(characterUID), lines.ToArray());
            Plugin.Log.LogMessage($"Saved {slots.Length} slots for character {characterUID}.");
        }

        public static void Load(string characterUID, SlotDropHandler[] slots, Character character)
        {
            var path = GetPath(characterUID);
            if (!File.Exists(path)) return;

            try
            {
                var lines = File.ReadAllLines(path);
                var inventory = character.Inventory;

                for (int i = 0; i < slots.Length && i < lines.Length; i++)
                {
                    if (!int.TryParse(lines[i].Trim(), out int itemID) || itemID < 0)
                        continue;

                    var item = FindItem(inventory, itemID);
                    if (item != null)
                        slots[i].AssignItem(item);
                }

                Plugin.Log.LogMessage($"Loaded slots for character {characterUID}.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Failed to load slots: {ex.Message}");
            }
        }

        private static Item FindItem(CharacterInventory inventory, int itemID)
        {
            // Check learned skills first
            if (inventory.SkillKnowledge != null)
            {
                foreach (var item in inventory.SkillKnowledge.GetLearnedItems())
                {
                    if (item.ItemID == itemID) return item;
                }
            }

            // Then check pouch
            if (inventory.Pouch != null)
            {
                foreach (var item in inventory.Pouch.GetContainedItems())
                {
                    if (item.ItemID == itemID) return item;
                }
            }

            // Then check bag
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

            return null;
        }
    }
}
