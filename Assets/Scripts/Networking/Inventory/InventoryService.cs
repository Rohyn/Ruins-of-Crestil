using System;
using System.Collections.Generic;
using ROC.Game.Common;
using ROC.Game.Inventory;
using ROC.Networking.Sessions;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ROC.Networking.Inventory
{
    [DisallowMultipleComponent]
    public sealed class InventoryService : MonoBehaviour
    {
        public static InventoryService Instance { get; private set; }

        public event Action<InventoryChangeEvent> InventoryChanged;

        [Header("References")]
        [SerializeField] private ItemCatalog itemCatalog;

        [Tooltip("Assign LocalInventoryRepository for now.")]
        [SerializeField] private MonoBehaviour repositoryBehaviour;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging;

        private IInventoryRepository _repository;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            ResolveRepository();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public ServerActionResult GrantItemForClient(
            ulong clientId,
            string definitionId,
            int quantity,
            string source = "")
        {
            if (!RequireServer(out ServerActionResult serverResult))
            {
                return serverResult;
            }

            if (!TryGetCharacterId(clientId, out string characterId))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.NoCharacterSelected,
                    "Cannot grant item because no character is selected.");
            }

            return GrantItemToCharacter(clientId, characterId, definitionId, quantity, source);
        }

        public ServerActionResult GrantItemToCharacter(
            ulong clientId,
            string characterId,
            string definitionId,
            int quantity,
            string source = "")
        {
            if (!RequireReady(out ServerActionResult readyResult))
            {
                return readyResult;
            }

            if (quantity <= 0)
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.InvalidRequest,
                    "Quantity must be greater than zero.");
            }

            if (!itemCatalog.TryGetDefinition(definitionId, out ItemDefinition definition))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.InvalidRequest,
                    $"Unknown item definition: {definitionId}");
            }

            CharacterInventoryRecord inventory = _repository.GetOrCreateInventory(characterId);

            if (definition.IsStackable)
            {
                AddStackableItems(inventory, definition, quantity);
            }
            else
            {
                for (int i = 0; i < quantity; i++)
                {
                    inventory.Items.Add(CreateInstance(definition, 1, InventoryLocationKind.Bag));
                }
            }

            if (!_repository.SaveInventory(inventory))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.PersistenceFailed,
                    "Failed to save inventory.");
            }

            PublishChange(new InventoryChangeEvent(
                clientId,
                characterId,
                InventoryChangeKind.ItemGranted,
                string.Empty,
                definitionId,
                quantity,
                source));

            PushSnapshotToClient(clientId);

            return ServerActionResult.Ok();
        }

        public ServerActionResult EquipItemForClient(ulong clientId, string itemInstanceId)
        {
            if (!RequireServer(out ServerActionResult serverResult))
            {
                return serverResult;
            }

            if (!TryGetCharacterId(clientId, out string characterId))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.NoCharacterSelected,
                    "Cannot equip item because no character is selected.");
            }

            return EquipItem(clientId, characterId, itemInstanceId);
        }

        public ServerActionResult UnequipItemForClient(ulong clientId, string itemInstanceId)
        {
            if (!RequireServer(out ServerActionResult serverResult))
            {
                return serverResult;
            }

            if (!TryGetCharacterId(clientId, out string characterId))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.NoCharacterSelected,
                    "Cannot unequip item because no character is selected.");
            }

            return UnequipItem(clientId, characterId, itemInstanceId);
        }

        public bool HasItemByDefinitionForClient(
            ulong clientId,
            string definitionId,
            int minimumQuantity = 1,
            InventoryLocationKind? location = null)
        {
            if (!TryGetCharacterId(clientId, out string characterId))
            {
                return false;
            }

            return HasItemByDefinition(characterId, definitionId, minimumQuantity, location);
        }

        public bool IsEquippedByDefinitionForClient(ulong clientId, string definitionId)
        {
            return HasItemByDefinitionForClient(
                clientId,
                definitionId,
                1,
                InventoryLocationKind.Equipped);
        }

        public bool HasItemByDefinition(
            string characterId,
            string definitionId,
            int minimumQuantity = 1,
            InventoryLocationKind? location = null)
        {
            ResolveRepository();

            if (_repository == null ||
                string.IsNullOrWhiteSpace(characterId) ||
                string.IsNullOrWhiteSpace(definitionId))
            {
                return false;
            }

            CharacterInventoryRecord inventory = _repository.GetOrCreateInventory(characterId);
            int count = 0;

            for (int i = 0; i < inventory.Items.Count; i++)
            {
                InventoryItemInstanceRecord item = inventory.Items[i];

                if (item == null || item.DefinitionId != definitionId)
                {
                    continue;
                }

                if (location.HasValue && item.Location != location.Value)
                {
                    continue;
                }

                count += Mathf.Max(0, item.Quantity);

                if (count >= minimumQuantity)
                {
                    return true;
                }
            }

            return false;
        }

        public ServerActionResult RemoveItemsByDefinitionForClient(
            ulong clientId,
            string definitionId,
            int quantity,
            InventoryLocationKind? location = null,
            string source = "")
        {
            if (!RequireServer(out ServerActionResult serverResult))
            {
                return serverResult;
            }

            if (!TryGetCharacterId(clientId, out string characterId))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.NoCharacterSelected,
                    "Cannot remove item because no character is selected.");
            }

            return RemoveItemsByDefinition(
                clientId,
                characterId,
                definitionId,
                quantity,
                location,
                source);
        }

        public void PushSnapshotToClient(ulong clientId)
        {
            if (clientId == ulong.MaxValue)
            {
                return;
            }

            if (!TryGetCharacterId(clientId, out string characterId))
            {
                return;
            }

            if (!TryGetClientInventoryState(clientId, out ClientInventoryState state))
            {
                return;
            }

            CharacterInventoryRecord inventory = GetInventory(characterId);
            InventoryItemSnapshot[] snapshot = BuildSnapshot(inventory);

            state.ReplaceSnapshotServer(snapshot);

            PublishChange(new InventoryChangeEvent(
                clientId,
                characterId,
                InventoryChangeKind.SnapshotPushed,
                string.Empty,
                string.Empty,
                0,
                "snapshot"));
        }

        private ServerActionResult EquipItem(
            ulong clientId,
            string characterId,
            string itemInstanceId)
        {
            if (!RequireReady(out ServerActionResult readyResult))
            {
                return readyResult;
            }

            CharacterInventoryRecord inventory = _repository.GetOrCreateInventory(characterId);

            if (!TryFindItem(inventory, itemInstanceId, out InventoryItemInstanceRecord item))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.InvalidTarget,
                    "Item instance not found.");
            }

            if (item.Location != InventoryLocationKind.Bag)
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.InvalidState,
                    "Only bag items can be equipped.");
            }

            if (!itemCatalog.TryGetDefinition(item.DefinitionId, out ItemDefinition definition) ||
                !definition.IsEquippable)
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.InvalidState,
                    "Item is not equippable.");
            }

            if (item.Quantity != 1)
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.InvalidState,
                    "Stacked equipment is not supported in this first pass.");
            }

            item.Location = InventoryLocationKind.Equipped;

            if (!_repository.SaveInventory(inventory))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.PersistenceFailed,
                    "Failed to save inventory.");
            }

            PublishChange(new InventoryChangeEvent(
                clientId,
                characterId,
                InventoryChangeKind.ItemEquipped,
                item.ItemInstanceId,
                item.DefinitionId,
                item.Quantity,
                "equip"));

            PushSnapshotToClient(clientId);

            return ServerActionResult.Ok();
        }

        private ServerActionResult UnequipItem(
            ulong clientId,
            string characterId,
            string itemInstanceId)
        {
            if (!RequireReady(out ServerActionResult readyResult))
            {
                return readyResult;
            }

            CharacterInventoryRecord inventory = _repository.GetOrCreateInventory(characterId);

            if (!TryFindItem(inventory, itemInstanceId, out InventoryItemInstanceRecord item))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.InvalidTarget,
                    "Item instance not found.");
            }

            if (item.Location != InventoryLocationKind.Equipped)
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.InvalidState,
                    "Only equipped items can be unequipped.");
            }

            item.Location = InventoryLocationKind.Bag;

            if (!_repository.SaveInventory(inventory))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.PersistenceFailed,
                    "Failed to save inventory.");
            }

            PublishChange(new InventoryChangeEvent(
                clientId,
                characterId,
                InventoryChangeKind.ItemUnequipped,
                item.ItemInstanceId,
                item.DefinitionId,
                item.Quantity,
                "unequip"));

            PushSnapshotToClient(clientId);

            return ServerActionResult.Ok();
        }

        private ServerActionResult RemoveItemsByDefinition(
            ulong clientId,
            string characterId,
            string definitionId,
            int quantity,
            InventoryLocationKind? location,
            string source)
        {
            if (!RequireReady(out ServerActionResult readyResult))
            {
                return readyResult;
            }

            if (quantity <= 0)
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.InvalidRequest,
                    "Quantity must be greater than zero.");
            }

            CharacterInventoryRecord inventory = _repository.GetOrCreateInventory(characterId);

            int remaining = quantity;

            for (int i = inventory.Items.Count - 1; i >= 0 && remaining > 0; i--)
            {
                InventoryItemInstanceRecord item = inventory.Items[i];

                if (item == null || item.DefinitionId != definitionId)
                {
                    continue;
                }

                if (location.HasValue && item.Location != location.Value)
                {
                    continue;
                }

                int remove = Mathf.Min(item.Quantity, remaining);
                item.Quantity -= remove;
                remaining -= remove;

                if (item.Quantity <= 0)
                {
                    inventory.Items.RemoveAt(i);
                }
            }

            int removed = quantity - remaining;

            if (removed <= 0)
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.InvalidState,
                    "No matching items were found.");
            }

            if (!_repository.SaveInventory(inventory))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.PersistenceFailed,
                    "Failed to save inventory.");
            }

            PublishChange(new InventoryChangeEvent(
                clientId,
                characterId,
                InventoryChangeKind.ItemRemoved,
                string.Empty,
                definitionId,
                removed,
                source));

            PushSnapshotToClient(clientId);

            return ServerActionResult.Ok();
        }

        private CharacterInventoryRecord GetInventory(string characterId)
        {
            ResolveRepository();
            return _repository?.GetOrCreateInventory(characterId);
        }

        private void AddStackableItems(
            CharacterInventoryRecord inventory,
            ItemDefinition definition,
            int quantity)
        {
            int remaining = quantity;
            int maxStack = Mathf.Max(1, definition.MaxStack);

            for (int i = 0; i < inventory.Items.Count && remaining > 0; i++)
            {
                InventoryItemInstanceRecord existing = inventory.Items[i];

                if (existing == null ||
                    existing.Location != InventoryLocationKind.Bag ||
                    existing.DefinitionId != definition.ItemId ||
                    existing.HasInstanceSpecificData())
                {
                    continue;
                }

                int available = maxStack - existing.Quantity;

                if (available <= 0)
                {
                    continue;
                }

                int add = Mathf.Min(available, remaining);
                existing.Quantity += add;
                remaining -= add;
            }

            while (remaining > 0)
            {
                int stackQuantity = Mathf.Min(maxStack, remaining);

                inventory.Items.Add(CreateInstance(
                    definition,
                    stackQuantity,
                    InventoryLocationKind.Bag));

                remaining -= stackQuantity;
            }
        }

        private static InventoryItemInstanceRecord CreateInstance(
            ItemDefinition definition,
            int quantity,
            InventoryLocationKind location)
        {
            return new InventoryItemInstanceRecord
            {
                ItemInstanceId = GenerateItemInstanceId(),
                DefinitionId = definition.ItemId,
                Quantity = Mathf.Max(1, quantity),
                Location = location,
                GeneratedDisplayNameOverride = string.Empty,
                ValueModifier = 0,
                VanityAppearanceId = definition.DefaultAppearanceId,
                InstanceTags = new List<string>(),
                Modifiers = new List<ItemModifierRecord>()
            };
        }

        private InventoryItemSnapshot[] BuildSnapshot(CharacterInventoryRecord inventory)
        {
            if (inventory == null || inventory.Items == null)
            {
                return Array.Empty<InventoryItemSnapshot>();
            }

            var snapshot = new List<InventoryItemSnapshot>(inventory.Items.Count);

            for (int i = 0; i < inventory.Items.Count; i++)
            {
                InventoryItemInstanceRecord item = inventory.Items[i];

                if (item == null || string.IsNullOrWhiteSpace(item.ItemInstanceId))
                {
                    continue;
                }

                ItemDefinition definition = null;
                itemCatalog?.TryGetDefinition(item.DefinitionId, out definition);

                string displayName = !string.IsNullOrWhiteSpace(item.GeneratedDisplayNameOverride)
                    ? item.GeneratedDisplayNameOverride
                    : definition != null
                        ? definition.DisplayName
                        : item.DefinitionId;

                int unitValue = Mathf.Max(0, (definition != null ? definition.BaseValue : 0) + item.ValueModifier);

                snapshot.Add(new InventoryItemSnapshot
                {
                    ItemInstanceId = new FixedString64Bytes(item.ItemInstanceId),
                    DefinitionId = new FixedString64Bytes(item.DefinitionId ?? string.Empty),
                    DisplayName = new FixedString128Bytes(displayName ?? string.Empty),
                    Quantity = Mathf.Max(1, item.Quantity),
                    IsStackable = definition != null && definition.IsStackable,
                    IsEquippable = definition != null && definition.IsEquippable,
                    UnitValue = unitValue,
                    Location = item.Location
                });
            }

            return snapshot.ToArray();
        }

        private static bool TryFindItem(
            CharacterInventoryRecord inventory,
            string itemInstanceId,
            out InventoryItemInstanceRecord item)
        {
            item = null;

            if (inventory == null || inventory.Items == null || string.IsNullOrWhiteSpace(itemInstanceId))
            {
                return false;
            }

            for (int i = 0; i < inventory.Items.Count; i++)
            {
                InventoryItemInstanceRecord candidate = inventory.Items[i];

                if (candidate != null && candidate.ItemInstanceId == itemInstanceId)
                {
                    item = candidate;
                    return true;
                }
            }

            return false;
        }

        private void ResolveRepository()
        {
            if (_repository != null)
            {
                return;
            }

            if (repositoryBehaviour is IInventoryRepository assignedRepository)
            {
                _repository = assignedRepository;
                return;
            }

            if (LocalInventoryRepository.Instance != null)
            {
                _repository = LocalInventoryRepository.Instance;
            }
        }

        private bool RequireReady(out ServerActionResult result)
        {
            if (!RequireServer(out result))
            {
                return false;
            }

            ResolveRepository();

            if (_repository == null)
            {
                result = ServerActionResult.Fail(
                    ServerActionErrorCode.PersistenceUnavailable,
                    "No inventory repository is available.");
                return false;
            }

            if (itemCatalog == null)
            {
                result = ServerActionResult.Fail(
                    ServerActionErrorCode.InvalidState,
                    "No item catalog is assigned.");
                return false;
            }

            result = ServerActionResult.Ok();
            return true;
        }

        private static bool RequireServer(out ServerActionResult result)
        {
            NetworkManager networkManager = NetworkManager.Singleton;

            if (networkManager != null &&
                networkManager.IsListening &&
                !networkManager.IsServer)
            {
                result = ServerActionResult.Fail(
                    ServerActionErrorCode.PermissionDenied,
                    "Inventory can only be changed by the server.");
                return false;
            }

            result = ServerActionResult.Ok();
            return true;
        }

        private static bool TryGetCharacterId(ulong clientId, out string characterId)
        {
            characterId = string.Empty;

            PlayerSessionRegistry registry = PlayerSessionRegistry.Instance;

            return registry != null &&
                   registry.TryGetCharacterId(clientId, out characterId) &&
                   !string.IsNullOrWhiteSpace(characterId);
        }

        private static bool TryGetClientInventoryState(
            ulong clientId,
            out ClientInventoryState state)
        {
            state = null;

            NetworkManager networkManager = NetworkManager.Singleton;

            if (networkManager == null ||
                !networkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) ||
                client.PlayerObject == null)
            {
                return false;
            }

            return client.PlayerObject.TryGetComponent(out state);
        }

        private void PublishChange(InventoryChangeEvent change)
        {
            InventoryChanged?.Invoke(change);

            if (verboseLogging)
            {
                Debug.Log(
                    $"[InventoryService] {change.ChangeKind}: Character={change.CharacterId}, " +
                    $"Definition={change.DefinitionId}, Instance={change.ItemInstanceId}, Quantity={change.Quantity}, Source={change.Source}");
            }
        }

        private static string GenerateItemInstanceId()
        {
            return $"item_{Guid.NewGuid():N}";
        }
    }
}