using System;
using ROC.Game.Inventory;
using Unity.Collections;
using Unity.Netcode;

namespace ROC.Networking.Inventory
{
    [Serializable]
    public struct InventoryItemSnapshot : INetworkSerializable, IEquatable<InventoryItemSnapshot>
    {
        public FixedString64Bytes ItemInstanceId;
        public FixedString64Bytes DefinitionId;
        public FixedString128Bytes DisplayName;
        public int Quantity;
        public bool IsStackable;
        public bool IsEquippable;
        public int UnitValue;
        public InventoryLocationKind Location;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ItemInstanceId);
            serializer.SerializeValue(ref DefinitionId);
            serializer.SerializeValue(ref DisplayName);
            serializer.SerializeValue(ref Quantity);
            serializer.SerializeValue(ref IsStackable);
            serializer.SerializeValue(ref IsEquippable);
            serializer.SerializeValue(ref UnitValue);

            byte location = (byte)Location;
            serializer.SerializeValue(ref location);

            if (serializer.IsReader)
            {
                Location = (InventoryLocationKind)location;
            }
        }

        public bool Equals(InventoryItemSnapshot other)
        {
            return ItemInstanceId.Equals(other.ItemInstanceId)
                   && DefinitionId.Equals(other.DefinitionId)
                   && Quantity == other.Quantity
                   && Location == other.Location;
        }

        public override bool Equals(object obj)
        {
            return obj is InventoryItemSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ItemInstanceId, DefinitionId, Quantity, Location);
        }
    }
}