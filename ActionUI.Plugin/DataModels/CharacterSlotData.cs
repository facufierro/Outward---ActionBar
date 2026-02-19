using System.Collections.Generic;

namespace ModifAmorphic.Outward.ActionUI.DataModels
{
    /// <summary>
    /// Stores slot assignments for a specific character.
    /// The key is (hotbarIndex, slotIndex), value is the slot data.
    /// </summary>
    public class CharacterSlotData
    {
        public string CharacterUID { get; set; }
        
        /// <summary>
        /// Dictionary of slot assignments. Key format: "barIndex_slotIndex"
        /// </summary>
        public Dictionary<string, SlotDataEntry> Slots { get; set; } = new Dictionary<string, SlotDataEntry>();
        
        /// <summary>
        /// Dictionary of disabled slot indices. Key format: "barIndex_slotIndex"
        /// </summary>
        public HashSet<string> DisabledSlots { get; set; } = new HashSet<string>();

        /// <summary>
        /// Dynamic slot presets. First key is weapon type, second key is slot key "barIndex_slotIndex".
        /// </summary>
        public Dictionary<string, Dictionary<string, SlotDataEntry>> DynamicPresets { get; set; } = new Dictionary<string, Dictionary<string, SlotDataEntry>>();
    }
    
    /// <summary>
    /// Simple entry for slot assignment data (what's in each slot)
    /// </summary>
    public class SlotDataEntry
    {
        public int ItemID { get; set; }
        public string ItemUID { get; set; }
    }
}
