namespace ROC.Game.Inventory
{
    public interface IInventoryRepository
    {
        CharacterInventoryRecord GetOrCreateInventory(string characterId);
        bool SaveInventory(CharacterInventoryRecord inventory);
    }
}