using System;
using System.Collections.Generic;

namespace ROC.Game.Inventory
{
    [Serializable]
    public sealed class CharacterInventoryRecord
    {
        public string CharacterId;
        public List<InventoryItemInstanceRecord> Items = new();
    }
}