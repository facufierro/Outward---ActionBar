using System;
using System.IO;
using System.Linq;

namespace fierrof.ActionBar
{
    /// <summary>
    /// Saves and loads per-character slot assignments and modes.
    /// File: {BepInExConfigDir}/ActionBar_Slots/{characterUID}.txt
    /// Format: one line per slot position: "ItemID|Mode" (-1 = empty, mode = 0/1/2).
    /// Total lines = Plugin.MAX_BARS * Plugin.MAX_SLOTS_PER_BAR.
    /// </summary>
    public static class SlotSaveManager
    {
        private static string SaveDir =>
            Path.Combine(BepInEx.Paths.ConfigPath, "ActionBar_Slots");

        private static string GetPath(string characterUID) =>
            Path.Combine(SaveDir, $"{characterUID}.txt");

        public static void Save(string characterUID, SlotDropHandler[] slots)
        {
            // Always save exactly Plugin.MAX_BARS * Plugin.MAX_SLOTS_PER_BAR lines
            var lines = Enumerable.Repeat("-1|0", Plugin.MAX_BARS * Plugin.MAX_SLOTS_PER_BAR).ToArray();

            foreach (var slot in slots)
            {
                int lineIndex = slot.BarIndex * Plugin.MAX_SLOTS_PER_BAR + slot.SlotIndex;
                string itemId = slot.AssignedItem != null ? slot.AssignedItem.ItemID.ToString() : "-1";
                int mode = (int)slot.Mode;
                lines[lineIndex] = $"{itemId}|{mode}";
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
                    int lineIndex = slot.BarIndex * Plugin.MAX_SLOTS_PER_BAR + slot.SlotIndex;
                    if (lineIndex >= lines.Length) continue;

                    string line = lines[lineIndex].Trim();
                    
                    // Parse "ItemID|Mode" or legacy "ItemID"
                    string[] parts = line.Split('|');
                    
                    if (!int.TryParse(parts[0], out int itemID))
                        continue;

                    // Load mode (0=Active, 1=Hidden, 2=Disabled, 3=Dynamic)
                    if (parts.Length > 1 && int.TryParse(parts[1], out int modeInt) && modeInt >= 0 && modeInt <= 3)
                        slot.Mode = (SlotMode)modeInt;

                    // Load item
                    if (slot.AssignedItem != null) continue; // already loaded
                    if (itemID < 0) continue;

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

        /// <summary>Public accessor for finding items by ID on a character.</summary>
        public static Item FindItemStatic(Character character, int itemID) => FindItem(character, itemID);

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
