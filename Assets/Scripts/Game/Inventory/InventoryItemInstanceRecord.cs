using System;
using System.Collections.Generic;

namespace ROC.Game.Inventory
{
    [Serializable]
    public sealed class InventoryItemInstanceRecord
    {
        public string ItemInstanceId;
        public string DefinitionId;
        public int Quantity = 1;
        public InventoryLocationKind Location = InventoryLocationKind.Bag;

        public string GeneratedDisplayNameOverride;
        public int ValueModifier;
        public string VanityAppearanceId;

        public List<string> InstanceTags = new();
        public List<ItemModifierRecord> Modifiers = new();

        public bool HasInstanceTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            for (int i = 0; i < InstanceTags.Count; i++)
            {
                if (InstanceTags[i] == tag)
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasModifiers()
        {
            return Modifiers != null && Modifiers.Count > 0;
        }

        public bool HasInstanceSpecificData()
        {
            return !string.IsNullOrWhiteSpace(GeneratedDisplayNameOverride)
                   || ValueModifier != 0
                   || !string.IsNullOrWhiteSpace(VanityAppearanceId)
                   || HasModifiers()
                   || (InstanceTags != null && InstanceTags.Count > 0);
        }
    }
}