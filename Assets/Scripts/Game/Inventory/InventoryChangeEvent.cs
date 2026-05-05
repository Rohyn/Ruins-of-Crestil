namespace ROC.Game.Inventory
{
    public readonly struct InventoryChangeEvent
    {
        public readonly ulong ClientId;
        public readonly string CharacterId;
        public readonly InventoryChangeKind ChangeKind;
        public readonly string ItemInstanceId;
        public readonly string DefinitionId;
        public readonly int Quantity;
        public readonly string Source;

        public InventoryChangeEvent(
            ulong clientId,
            string characterId,
            InventoryChangeKind changeKind,
            string itemInstanceId,
            string definitionId,
            int quantity,
            string source)
        {
            ClientId = clientId;
            CharacterId = characterId ?? string.Empty;
            ChangeKind = changeKind;
            ItemInstanceId = itemInstanceId ?? string.Empty;
            DefinitionId = definitionId ?? string.Empty;
            Quantity = quantity;
            Source = source ?? string.Empty;
        }
    }
}