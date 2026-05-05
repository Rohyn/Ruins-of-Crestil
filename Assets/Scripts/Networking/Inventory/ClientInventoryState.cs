using System;
using ROC.Game.Inventory;
using Unity.Netcode;
using UnityEngine;

namespace ROC.Networking.Inventory
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class ClientInventoryState : NetworkBehaviour
    {
        public static ClientInventoryState Local { get; private set; }
        public static event Action<ClientInventoryState> LocalInventoryReady;

        public readonly NetworkList<InventoryItemSnapshot> Items = new(
            null,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);

        public event Action InventorySnapshotChanged;

        public override void OnNetworkSpawn()
        {
            Items.OnListChanged += HandleItemsChanged;

            if (IsOwner)
            {
                Local = this;
                LocalInventoryReady?.Invoke(this);
                RequestInventorySnapshot();
            }
        }

        public override void OnNetworkDespawn()
        {
            Items.OnListChanged -= HandleItemsChanged;

            if (Local == this)
            {
                Local = null;
            }
        }

        public void RequestInventorySnapshot()
        {
            if (!IsOwner)
            {
                return;
            }

            RequestInventorySnapshotServerRpc();
        }

        public void RequestEquipItem(string itemInstanceId)
        {
            if (!IsOwner || string.IsNullOrWhiteSpace(itemInstanceId))
            {
                return;
            }

            RequestEquipItemServerRpc(itemInstanceId);
        }

        public void RequestUnequipItem(string itemInstanceId)
        {
            if (!IsOwner || string.IsNullOrWhiteSpace(itemInstanceId))
            {
                return;
            }

            RequestUnequipItemServerRpc(itemInstanceId);
        }

        public void ReplaceSnapshotServer(InventoryItemSnapshot[] snapshot)
        {
            if (!IsServer)
            {
                return;
            }

            Items.Clear();

            if (snapshot == null)
            {
                return;
            }

            for (int i = 0; i < snapshot.Length; i++)
            {
                Items.Add(snapshot[i]);
            }
        }

        public int GetEntryCount(InventoryLocationKind location)
        {
            int count = 0;

            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i].Location == location)
                {
                    count++;
                }
            }

            return count;
        }

        public bool TryGetDisplayInfoAt(
            int filteredIndex,
            InventoryLocationKind location,
            out string itemInstanceId,
            out string displayName,
            out int quantity,
            out bool isStackable,
            out bool isEquippable)
        {
            itemInstanceId = string.Empty;
            displayName = string.Empty;
            quantity = 0;
            isStackable = false;
            isEquippable = false;

            if (filteredIndex < 0)
            {
                return false;
            }

            int seen = 0;

            for (int i = 0; i < Items.Count; i++)
            {
                InventoryItemSnapshot item = Items[i];

                if (item.Location != location)
                {
                    continue;
                }

                if (seen != filteredIndex)
                {
                    seen++;
                    continue;
                }

                itemInstanceId = item.ItemInstanceId.ToString();
                displayName = item.DisplayName.ToString();
                quantity = item.Quantity;
                isStackable = item.IsStackable;
                isEquippable = item.IsEquippable;
                return true;
            }

            return false;
        }

        [ServerRpc]
        private void RequestInventorySnapshotServerRpc(ServerRpcParams serverRpcParams = default)
        {
            InventoryService.Instance?.PushSnapshotToClient(serverRpcParams.Receive.SenderClientId);
        }

        [ServerRpc]
        private void RequestEquipItemServerRpc(string itemInstanceId, ServerRpcParams serverRpcParams = default)
        {
            InventoryService.Instance?.EquipItemForClient(
                serverRpcParams.Receive.SenderClientId,
                itemInstanceId);
        }

        [ServerRpc]
        private void RequestUnequipItemServerRpc(string itemInstanceId, ServerRpcParams serverRpcParams = default)
        {
            InventoryService.Instance?.UnequipItemForClient(
                serverRpcParams.Receive.SenderClientId,
                itemInstanceId);
        }

        private void HandleItemsChanged(NetworkListEvent<InventoryItemSnapshot> changeEvent)
        {
            InventorySnapshotChanged?.Invoke();
        }
    }
}