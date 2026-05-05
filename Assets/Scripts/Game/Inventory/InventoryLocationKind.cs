namespace ROC.Game.Inventory
{
    public enum InventoryLocationKind : byte
    {
        Bag = 0,
        Equipped = 1,

        // Reserved for later systems.
        Bank = 10,
        Storage = 11,
        Pockets = 12
    }
}