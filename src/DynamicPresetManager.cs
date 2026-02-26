using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace fierrof.ActionBar
{
    /// <summary>
    /// Manages dynamic slot presets per weapon context.
    /// Each character gets a JSON file storing presets keyed by weapon context.
    /// Context keys: "baseline", "main:{type}", "off:{type}", "combo:{main}:{off}"
    /// </summary>
    public static class DynamicPresetManager
    {
        private const int NO_WEAPON = -1;
        private const int MAIN_OFFSET = 2000000;
        private const int OFF_OFFSET  = 1000000;

        private static string SaveDir =>
            Path.Combine(BepInEx.Paths.ConfigPath, "ActionBar_Dynamic");

        private static string GetPath(string characterUID) =>
            Path.Combine(SaveDir, $"{SanitizeUID(characterUID)}.json");

        // Current loaded data
        private static string _activeUID;
        private static Dictionary<string, Dictionary<string, PresetEntry>> _presets
            = new Dictionary<string, Dictionary<string, PresetEntry>>();

        // Current weapon context signature to detect changes
        private static string _lastContextSignature = "";

        // ── Context key generation ──────────────────────────

        public static string GetContextKey(Character character)
        {
            if (character == null) return "baseline";

            var equipment = character.Inventory?.Equipment;
            if (equipment == null) return "baseline";

            int mainType = GetWeaponType(equipment, true);
            int offType  = GetWeaponType(equipment, false);

            if (mainType != NO_WEAPON && offType != NO_WEAPON)
                return $"combo:{mainType}:{offType}";
            if (mainType != NO_WEAPON)
                return $"main:{mainType}";
            if (offType != NO_WEAPON)
                return $"off:{offType}";

            return "baseline";
        }

        /// <summary>Returns ordered resolve keys: most specific first, baseline last.</summary>
        public static string[] GetResolveKeys(Character character)
        {
            if (character == null) return new[] { "baseline" };

            var equipment = character.Inventory?.Equipment;
            if (equipment == null) return new[] { "baseline" };

            int mainType = GetWeaponType(equipment, true);
            int offType  = GetWeaponType(equipment, false);

            var keys = new List<string>(4);
            if (mainType != NO_WEAPON && offType != NO_WEAPON)
                keys.Add($"combo:{mainType}:{offType}");
            if (mainType != NO_WEAPON)
                keys.Add($"main:{mainType}");
            if (offType != NO_WEAPON)
                keys.Add($"off:{offType}");
            keys.Add("baseline");

            return keys.ToArray();
        }

        private static int GetWeaponType(CharacterEquipment equipment, bool mainHand)
        {
            if (equipment.EquipmentSlots == null) return NO_WEAPON;

            foreach (var slot in equipment.EquipmentSlots)
            {
                if (slot?.EquippedItem == null) continue;

                string slotName = slot.SlotType.ToString();
                bool isTarget = mainHand
                    ? (slotName.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       slotName.IndexOf("Main", StringComparison.OrdinalIgnoreCase) >= 0)
                    : (slotName.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       slotName.IndexOf("Off", StringComparison.OrdinalIgnoreCase) >= 0);

                if (!isTarget) continue;

                if (slot.EquippedItem is Weapon weapon)
                    return (int)weapon.Type;

                // Non-weapon equipment: offset + ItemID
                int offset = mainHand ? MAIN_OFFSET : OFF_OFFSET;
                return offset + slot.EquippedItem.ItemID;
            }

            return NO_WEAPON;
        }

        // ── Preset CRUD ─────────────────────────────────────

        public static void SetPreset(string contextKey, int barIndex, int slotIndex, int itemID, string itemUID)
        {
            string slotKey = $"{barIndex}_{slotIndex}";

            if (!_presets.TryGetValue(contextKey, out var slots))
            {
                slots = new Dictionary<string, PresetEntry>();
                _presets[contextKey] = slots;
            }

            slots[slotKey] = new PresetEntry { ItemID = itemID, ItemUID = itemUID };
        }

        public static bool TryGetPreset(string contextKey, int barIndex, int slotIndex, out PresetEntry entry)
        {
            entry = null;
            string slotKey = $"{barIndex}_{slotIndex}";

            return _presets.TryGetValue(contextKey, out var slots)
                && slots != null
                && slots.TryGetValue(slotKey, out entry)
                && entry != null;
        }

        public static void RemovePreset(string contextKey, int barIndex, int slotIndex)
        {
            string slotKey = $"{barIndex}_{slotIndex}";
            if (_presets.TryGetValue(contextKey, out var slots))
            {
                slots.Remove(slotKey);
                if (slots.Count == 0) _presets.Remove(contextKey);
            }
        }

        /// <summary>
        /// Resolves the best preset for a slot given current weapon context.
        /// Tries combo → main → off → baseline in order.
        /// </summary>
        public static bool ResolvePreset(string[] resolveKeys, int barIndex, int slotIndex, out PresetEntry entry)
        {
            entry = null;
            foreach (var key in resolveKeys)
            {
                if (TryGetPreset(key, barIndex, slotIndex, out entry))
                    return true;
            }
            return false;
        }

        // ── Save / Load ─────────────────────────────────────

        public static void EnsureLoaded(string characterUID)
        {
            if (characterUID == _activeUID) return;
            _activeUID = characterUID;
            _presets.Clear();
            _lastContextSignature = "";
            Load(characterUID);
        }

        public static void Save(string characterUID)
        {
            SavePresets(characterUID);
        }

        private static void Load(string characterUID)
        {
            var path = GetPath(characterUID);
            if (!File.Exists(path)) return;

            try
            {
                string json = File.ReadAllText(path);
                // Simple JSON parsing since we can't use Newtonsoft
                ParseJson(json);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Failed to load dynamic presets: {ex.Message}");
            }
        }

        // ── Context change detection ────────────────────────

        public static bool HasContextChanged(Character character)
        {
            string sig = GetContextKey(character);
            if (sig == _lastContextSignature) return false;
            _lastContextSignature = sig;
            return true;
        }

        public static void ResetContextSignature()
        {
            _lastContextSignature = "";
        }

        // ── Simple JSON serialization (no Newtonsoft dependency) ──

        private static void ParseJson(string json)
        {
            _presets.Clear();

            // Parse manually: look for context keys and their slot entries
            // Format: {"Presets":{"baseline":{"0_0":{"ItemID":123,"ItemUID":"abc"},...},...}}
            int presetsStart = json.IndexOf("\"Presets\"", StringComparison.Ordinal);
            if (presetsStart < 0) return;

            // Find the opening brace of Presets value
            int braceStart = json.IndexOf('{', presetsStart + 10);
            if (braceStart < 0) return;

            int depth = 0;
            string currentContext = null;
            int slotMapStart = -1;

            for (int i = braceStart; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 1 && currentContext != null && slotMapStart >= 0)
                    {
                        // Parse slot map
                        string slotMapJson = json.Substring(slotMapStart, i - slotMapStart + 1);
                        ParseSlotMap(currentContext, slotMapJson);
                        currentContext = null;
                        slotMapStart = -1;
                    }
                    if (depth <= 0) break;
                }
                else if (c == '"' && depth == 2 && currentContext == null)
                {
                    // Read context key
                    int keyEnd = json.IndexOf('"', i + 1);
                    if (keyEnd > i)
                    {
                        currentContext = json.Substring(i + 1, keyEnd - i - 1);
                        i = keyEnd;
                    }
                }
                else if (c == '{' && depth == 3 && currentContext != null && slotMapStart < 0)
                {
                    slotMapStart = i;
                    // Undo the depth increment since we already counted this brace
                }
            }
        }

        private static void ParseSlotMap(string contextKey, string json)
        {
            var slots = new Dictionary<string, PresetEntry>();

            int i = 0;
            while (i < json.Length)
            {
                // Find slot key like "0_0"
                int keyStart = json.IndexOf('"', i);
                if (keyStart < 0) break;
                int keyEnd = json.IndexOf('"', keyStart + 1);
                if (keyEnd < 0) break;

                string slotKey = json.Substring(keyStart + 1, keyEnd - keyStart - 1);
                i = keyEnd + 1;

                // Skip if this doesn't look like a slot key
                if (!slotKey.Contains("_") || slotKey == "ItemID" || slotKey == "ItemUID")
                    continue;

                // Find the entry object
                int entryStart = json.IndexOf('{', i);
                if (entryStart < 0) break;
                int entryEnd = json.IndexOf('}', entryStart);
                if (entryEnd < 0) break;

                string entryJson = json.Substring(entryStart, entryEnd - entryStart + 1);
                i = entryEnd + 1;

                int itemID = ParseIntField(entryJson, "ItemID");
                string itemUID = ParseStringField(entryJson, "ItemUID");

                slots[slotKey] = new PresetEntry { ItemID = itemID, ItemUID = itemUID };
            }

            if (slots.Count > 0)
                _presets[contextKey] = slots;
        }

        private static int ParseIntField(string json, string field)
        {
            string search = $"\"{field}\":";
            int idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return -1;

            int start = idx + search.Length;
            while (start < json.Length && (json[start] == ' ' || json[start] == '\t')) start++;

            int end = start;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-')) end++;

            if (int.TryParse(json.Substring(start, end - start), out int val))
                return val;
            return -1;
        }

        private static string ParseStringField(string json, string field)
        {
            string search = $"\"{field}\":";
            int idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return null;

            int start = idx + search.Length;
            while (start < json.Length && json[start] == ' ') start++;

            if (start < json.Length && json[start] == 'n') return null; // null

            if (start < json.Length && json[start] == '"')
            {
                int end = json.IndexOf('"', start + 1);
                if (end > start)
                    return json.Substring(start + 1, end - start - 1);
            }
            return null;
        }

        private static string SanitizeUID(string uid) =>
            uid.Replace("\\", "_").Replace("/", "_").Replace(":", "_");

        // ── Simple serialization to JSON string ──────────────

        private static string SerializeToJson()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("{");
            sb.Append("  \"CharacterUID\": \"").Append(_activeUID ?? "").AppendLine("\",");
            sb.AppendLine("  \"Presets\": {");

            var contextKeys = _presets.Keys.ToArray();
            for (int c = 0; c < contextKeys.Length; c++)
            {
                var contextKey = contextKeys[c];
                var slots = _presets[contextKey];

                sb.Append("    \"").Append(contextKey).AppendLine("\": {");

                var slotKeys = slots.Keys.ToArray();
                for (int s = 0; s < slotKeys.Length; s++)
                {
                    var entry = slots[slotKeys[s]];
                    sb.Append("      \"").Append(slotKeys[s]).Append("\": { \"ItemID\": ")
                      .Append(entry.ItemID);
                    if (entry.ItemUID != null)
                        sb.Append(", \"ItemUID\": \"").Append(entry.ItemUID).Append("\"");
                    else
                        sb.Append(", \"ItemUID\": null");
                    sb.Append(" }");
                    if (s < slotKeys.Length - 1) sb.Append(",");
                    sb.AppendLine();
                }

                sb.Append("    }");
                if (c < contextKeys.Length - 1) sb.Append(",");
                sb.AppendLine();
            }

            sb.AppendLine("  }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        // Override Save to use our serializer
        public static void SavePresets(string characterUID)
        {
            try
            {
                Directory.CreateDirectory(SaveDir);
                string json = SerializeToJson();
                File.WriteAllText(GetPath(characterUID), json);
                Plugin.Log.LogMessage($"Saved dynamic presets for {characterUID}.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Failed to save dynamic presets: {ex.Message}");
            }
        }

        // ── Data types ──────────────────────────────────────

        public class PresetEntry
        {
            public int ItemID = -1;
            public string ItemUID;
        }

    }
}
