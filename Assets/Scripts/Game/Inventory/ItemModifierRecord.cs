using System;
using System.Collections.Generic;

namespace ROC.Game.Inventory
{
    [Serializable]
    public sealed class ItemModifierRecord
    {
        public string ModifierId;
        public string DisplayName;
        public int ValueModifier;
        public List<string> Tags = new();
    }
}