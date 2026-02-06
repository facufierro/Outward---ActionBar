namespace ModifAmorphic.Outward.Unity.ActionUI.Data
{
    public interface ISlotData
    {
        int SlotIndex { get; set; }
        int ItemID { get; set; }
        string ItemUID { get; set; }
        IActionSlotConfig Config { get; set; }
    }
}
