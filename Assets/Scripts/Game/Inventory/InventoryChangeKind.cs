namespace ROC.Game.Inventory
{
    public enum InventoryChangeKind : byte
    {
        ItemGranted = 0,
        ItemRemoved = 1,
        ItemEquipped = 2,
        ItemUnequipped = 3,
        SnapshotPushed = 4
    }
}