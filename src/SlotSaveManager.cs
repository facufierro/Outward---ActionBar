using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace fierrof.ActionBar
{
    /// <summary>
    /// Single save system for all slot data. One JSON file per character.
    /// Persists base slot state + equipped-context dynamic overrides.
    ///
    /// Format:
    /// {
    ///   "Slots": { "0_0": { "ItemID": 123, "Mode": 0, "IsDynamic": false } },
    ///   "Presets": { "0_1": { "baseline": 456, "main:2": 789 } }
    /// }
    /// </summary>
    public static class SlotSaveManager
    {
        private static bool _hasParsed;

        public static void Reset()
        {
            _hasParsed = false;
            _presets.Clear();
            _pendingSlots = null;
        }

        // ── Paths ───────────────────────────────────────────
        private static string SaveDir =>
            Path.Combine(BepInEx.Paths.ConfigPath, "ActionBar_Saves");

        private static string GetPath(string uid) =>
            Path.Combine(SaveDir, $"{Sanitize(uid)}.json");

        private static string Sanitize(string uid) =>
            uid.Replace("\\", "_").Replace("/", "_").Replace(":", "_");

        // ── In-memory data ──────────────────────────────────
        // Presets: slotKey → (contextKey → itemID)
        private static readonly Dictionary<string, Dictionary<string, int>> _presets
            = new Dictionary<string, Dictionary<string, int>>();

        // Cached parsed slot data for retry pattern (parse once, retry FindItem)
        private static ParsedSlotEntry[] _pendingSlots;

        private class ParsedSlotEntry
        {
            public string Key;
            public int ItemID;
            public int Mode;
            public bool IsDynamic;
        }

        // ── Load ────────────────────────────────────────────

        /// <summary>
        /// Loads save data. First call parses JSON and caches it.
        /// Subsequent calls only retry FindItem for missing items.
        /// Returns true if all non-dynamic items were found.
        /// </summary>
        public static bool Load(string uid, SlotDropHandler[] slots, Character character)
        {
            bool firstCall = !_hasParsed;

            if (firstCall)
            {
                _presets.Clear();
                _pendingSlots = null;
                ParseFile(GetPath(uid));
                _hasParsed = true;
            }

            if (_pendingSlots == null) return true;

            var slotMap = new Dictionary<string, SlotDropHandler>();
            foreach (var s in slots)
                slotMap[$"{s.BarIndex}_{s.SlotIndex}"] = s;

            bool allFound = true;
            foreach (var entry in _pendingSlots)
            {
                if (!slotMap.TryGetValue(entry.Key, out var slot)) continue;

                if (firstCall)
                {
                    if (entry.Mode >= 0 && entry.Mode <= 2) slot.Mode = (SlotMode)entry.Mode;
                    slot.IsDynamic = entry.IsDynamic;
                    slot.SetBaseItemIdOnly(entry.ItemID);
                    slot.RefreshEditModeVisuals();
                }

                // Always try to resolve base item from Slots section; dynamic overrides are applied later.
                if (slot.AssignedItem != null) continue;
                if (entry.ItemID < 0) continue;

                var item = FindItem(character, entry.ItemID);
                if (item != null)
                    slot.AssignItemSilent(item);
                else
                    allFound = false;
            }

            return allFound;
        }

        // ── Save ────────────────────────────────────────────

        public static void Save(string uid, SlotDropHandler[] slots)
        {
            string path = GetPath(uid);
            if (!_hasParsed)
            {
                _presets.Clear();
                _pendingSlots = null;
                ParseFile(path);
                _hasParsed = true;
            }

            var sb = new StringBuilder();
            sb.AppendLine("{");

            // Slots section
            sb.AppendLine("  \"Slots\": {");
            var slotLines = new List<string>();
            foreach (var slot in slots)
            {
                string key = $"{slot.BarIndex}_{slot.SlotIndex}";
                int itemId = slot.BaseItemID > 0 ? slot.BaseItemID : -1;
                int mode = (int)slot.Mode;
                bool dyn = slot.IsDynamic;
                slotLines.Add($"    \"{key}\": {{ \"ItemID\": {itemId}, \"Mode\": {mode}, \"IsDynamic\": {(dyn ? "true" : "false")} }}");
            }
            sb.AppendLine(string.Join(",\n", slotLines));
            sb.AppendLine("  },");

            // Presets section: slotKey → { context: itemID, ... }
            sb.AppendLine("  \"Presets\": {");
            var presetKeys = _presets.Keys.ToArray();
            for (int i = 0; i < presetKeys.Length; i++)
            {
                var slotKey = presetKeys[i];
                var contexts = _presets[slotKey];
                var pairs = contexts.Select(kvp => $"\"{kvp.Key}\": {kvp.Value}");
                sb.Append($"    \"{slotKey}\": {{ {string.Join(", ", pairs)} }}");
                if (i < presetKeys.Length - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.AppendLine("  }");
            sb.AppendLine("}");

            try
            {
                Directory.CreateDirectory(SaveDir);
                File.WriteAllText(path, sb.ToString());
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Failed to save: {ex.Message}");
            }
        }

        // ── Preset API ──────────────────────────────────────

        public static void SetPreset(int barIndex, int slotIndex, string contextKey, int itemID)
        {
            string key = $"{barIndex}_{slotIndex}";

            if (contextKey == "baseline")
                return;

            // Model: non-baseline contexts only store positive item overrides.
            // Empty override in equipped contexts means "no override" -> remove entry.
            if (itemID <= 0)
            {
                RemovePreset(barIndex, slotIndex, contextKey);
                return;
            }

            if (!_presets.TryGetValue(key, out var contexts))
            {
                contexts = new Dictionary<string, int>();
                _presets[key] = contexts;
            }
            contexts[contextKey] = itemID;
        }

        public static void RemovePreset(int barIndex, int slotIndex, string contextKey)
        {
            string key = $"{barIndex}_{slotIndex}";
            if (_presets.TryGetValue(key, out var contexts))
            {
                contexts.Remove(contextKey);
                if (contexts.Count == 0) _presets.Remove(key);
            }
        }

        public static void RemoveAllPresets(int barIndex, int slotIndex)
        {
            _presets.Remove($"{barIndex}_{slotIndex}");
        }

        public static void ClearBarPresets(int barIndex)
        {
            string prefix = $"{barIndex}_";
            var toRemove = _presets.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            foreach (var k in toRemove) _presets.Remove(k);
        }

        /// <summary>
        /// Resolves the best preset for a dynamic slot given the current weapon context.
        /// Returns the ItemID, or -1 if no preset found.
        /// </summary>
        public static int ResolvePreset(int barIndex, int slotIndex, string[] resolveKeys)
        {
            string key = $"{barIndex}_{slotIndex}";
            if (!_presets.TryGetValue(key, out var contexts)) return -1;
            foreach (var ctx in resolveKeys)
            {
                if (contexts.TryGetValue(ctx, out int itemID))
                    return itemID;
            }
            return -1;
        }

        /// <summary>
        /// Resolves preset with presence information.
        /// Returns true only when a matching context preset exists (including explicit -1 empty value).
        /// Returns false when no preset exists for any resolve key.
        /// </summary>
        public static bool TryResolvePreset(int barIndex, int slotIndex, string[] resolveKeys, out int itemID)
        {
            itemID = -1;

            string key = $"{barIndex}_{slotIndex}";
            if (!_presets.TryGetValue(key, out var contexts))
                return false;

            foreach (var ctx in resolveKeys)
            {
                if (contexts.TryGetValue(ctx, out itemID))
                    return true;
            }

            return false;
        }

        // ── Weapon context ──────────────────────────────────

        private const int NO_WEAPON = -1;
        private const int MAIN_OFFSET = 2000000;
        private const int OFF_OFFSET  = 1000000;

        public static string GetContextKey(Character character)
        {
            if (character == null) return "baseline";
            var equipment = character.Inventory?.Equipment;
            if (equipment == null) return "baseline";

            int main = GetWeaponType(equipment, true);
            int off  = GetWeaponType(equipment, false);

            if (main != NO_WEAPON && off != NO_WEAPON) return $"combo:{main}:{off}";
            if (main != NO_WEAPON) return $"main:{main}";
            if (off != NO_WEAPON) return $"off:{off}";
            return "baseline";
        }

        public static string[] GetResolveKeys(Character character)
        {
            if (character == null) return new[] { "baseline" };
            var equipment = character.Inventory?.Equipment;
            if (equipment == null) return new[] { "baseline" };

            int main = GetWeaponType(equipment, true);
            int off  = GetWeaponType(equipment, false);

            var keys = new List<string>(4);
            if (main != NO_WEAPON && off != NO_WEAPON) keys.Add($"combo:{main}:{off}");
            if (main != NO_WEAPON) keys.Add($"main:{main}");
            if (off != NO_WEAPON) keys.Add($"off:{off}");
            keys.Add("baseline");
            return keys.ToArray();
        }

        private static int GetWeaponType(CharacterEquipment equipment, bool mainHand)
        {
            if (equipment.EquipmentSlots == null) return NO_WEAPON;
            foreach (var slot in equipment.EquipmentSlots)
            {
                if (slot?.EquippedItem == null) continue;
                string name = slot.SlotType.ToString();
                bool target = mainHand
                    ? (name.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       name.IndexOf("Main", StringComparison.OrdinalIgnoreCase) >= 0)
                    : (name.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       name.IndexOf("Off", StringComparison.OrdinalIgnoreCase) >= 0);
                if (!target) continue;
                if (slot.EquippedItem is Weapon weapon) return (int)weapon.Type;
                return (mainHand ? MAIN_OFFSET : OFF_OFFSET) + slot.EquippedItem.ItemID;
            }
            return NO_WEAPON;
        }

        // ── Item finder ─────────────────────────────────────

        public static Item FindItem(Character character, int itemID)
        {
            var inv = character.Inventory;
            if (inv.SkillKnowledge != null)
                foreach (var item in inv.SkillKnowledge.GetLearnedItems())
                    if (item.ItemID == itemID) return item;
            if (inv.Pouch != null)
                foreach (var item in inv.Pouch.GetContainedItems())
                    if (item.ItemID == itemID) return item;
            if (inv.EquippedBag != null)
            {
                var bag = inv.EquippedBag.Container;
                if (bag != null)
                    foreach (var item in bag.GetContainedItems())
                        if (item.ItemID == itemID) return item;
            }
            if (inv.Equipment != null)
                foreach (var slot in inv.Equipment.EquipmentSlots)
                    if (slot?.EquippedItem != null && slot.EquippedItem.ItemID == itemID)
                        return slot.EquippedItem;
            return null;
        }

        // ── JSON parsing ────────────────────────────────────

        private static void ParseFile(string path)
        {
            if (!File.Exists(path)) return;
            try
            {
                string json = File.ReadAllText(path);
                ParseSlots(json);
                ParsePresets(json);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Failed to load save: {ex.Message}");
            }
        }

        private static void ParseSlots(string json)
        {
            int idx = json.IndexOf("\"Slots\"", StringComparison.Ordinal);
            if (idx < 0) return;

            int outerStart = json.IndexOf('{', idx + 7);
            if (outerStart < 0) return;
            int outerEnd = FindMatchingBrace(json, outerStart);
            if (outerEnd < 0) return;

            string inner = json.Substring(outerStart + 1, outerEnd - outerStart - 1);
            var entries = new List<ParsedSlotEntry>();
            int i = 0;
            while (i < inner.Length)
            {
                int keyStart = inner.IndexOf('"', i);
                if (keyStart < 0) break;
                int keyEnd = inner.IndexOf('"', keyStart + 1);
                if (keyEnd < 0) break;
                string key = inner.Substring(keyStart + 1, keyEnd - keyStart - 1);
                i = keyEnd + 1;

                int entryStart = inner.IndexOf('{', i);
                if (entryStart < 0) break;
                int entryEnd = inner.IndexOf('}', entryStart);
                if (entryEnd < 0) break;
                string entryJson = inner.Substring(entryStart, entryEnd - entryStart + 1);
                i = entryEnd + 1;

                entries.Add(new ParsedSlotEntry
                {
                    Key = key,
                    ItemID = ParseInt(entryJson, "ItemID"),
                    Mode = ParseInt(entryJson, "Mode"),
                    IsDynamic = ParseBool(entryJson, "IsDynamic")
                });
            }
            _pendingSlots = entries.ToArray();
        }

        private static void ParsePresets(string json)
        {
            int idx = json.IndexOf("\"Presets\"", StringComparison.Ordinal);
            if (idx < 0) return;

            int outerStart = json.IndexOf('{', idx + 9);
            if (outerStart < 0) return;
            int outerEnd = FindMatchingBrace(json, outerStart);
            if (outerEnd < 0) return;

            string inner = json.Substring(outerStart + 1, outerEnd - outerStart - 1);
            int i = 0;
            while (i < inner.Length)
            {
                // Find slot key
                int keyStart = inner.IndexOf('"', i);
                if (keyStart < 0) break;
                int keyEnd = inner.IndexOf('"', keyStart + 1);
                if (keyEnd < 0) break;
                string slotKey = inner.Substring(keyStart + 1, keyEnd - keyStart - 1);
                i = keyEnd + 1;

                // Find the context map { "baseline": 123, "main:2": 456 }
                int mapStart = inner.IndexOf('{', i);
                if (mapStart < 0) break;
                int mapEnd = inner.IndexOf('}', mapStart);
                if (mapEnd < 0) break;
                string mapJson = inner.Substring(mapStart + 1, mapEnd - mapStart - 1);
                i = mapEnd + 1;

                var contexts = new Dictionary<string, int>();
                int j = 0;
                while (j < mapJson.Length)
                {
                    int ctxStart = mapJson.IndexOf('"', j);
                    if (ctxStart < 0) break;
                    int ctxEnd = mapJson.IndexOf('"', ctxStart + 1);
                    if (ctxEnd < 0) break;
                    string ctxKey = mapJson.Substring(ctxStart + 1, ctxEnd - ctxStart - 1);
                    j = ctxEnd + 1;

                    // Find colon then integer
                    int colon = mapJson.IndexOf(':', j);
                    if (colon < 0) break;
                    j = colon + 1;
                    while (j < mapJson.Length && mapJson[j] == ' ') j++;
                    int numStart = j;
                    while (j < mapJson.Length && (char.IsDigit(mapJson[j]) || mapJson[j] == '-')) j++;
                    if (int.TryParse(mapJson.Substring(numStart, j - numStart), out int val))
                        contexts[ctxKey] = val;
                }

                if (contexts.Count > 0)
                {
                    // Sanitize stale data from older builds:
                    // drop baseline entries and empty overrides; base state lives in Slots section.
                    var staleKeys = contexts
                        .Where(kvp => kvp.Key == "baseline" || kvp.Value <= 0)
                        .Select(kvp => kvp.Key)
                        .ToArray();

                    foreach (var staleKey in staleKeys)
                        contexts.Remove(staleKey);

                    if (contexts.Count == 0)
                        continue;

                    _presets[slotKey] = contexts;
                }
            }
        }

        // ── JSON helpers ────────────────────────────────────

        private static int FindMatchingBrace(string json, int openPos)
        {
            int depth = 1;
            for (int i = openPos + 1; i < json.Length; i++)
            {
                if (json[i] == '{') depth++;
                else if (json[i] == '}') { depth--; if (depth == 0) return i; }
            }
            return -1;
        }

        private static int ParseInt(string json, string field)
        {
            string search = $"\"{field}\":";
            int idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return -1;
            int start = idx + search.Length;
            while (start < json.Length && json[start] == ' ') start++;
            int end = start;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-')) end++;
            return int.TryParse(json.Substring(start, end - start), out int val) ? val : -1;
        }

        private static bool ParseBool(string json, string field)
        {
            string search = $"\"{field}\":";
            int idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return false;
            int start = idx + search.Length;
            while (start < json.Length && json[start] == ' ') start++;
            return start < json.Length && json[start] == 't';
        }
    }
}
