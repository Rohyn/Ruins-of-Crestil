using System;
using ROC.Game.Common;
using ROC.Networking.Inventory;
using Unity.Netcode;
using UnityEngine;

namespace ROC.Networking.Interactions
{
    /// <summary>
    /// Legacy component kept so existing prefabs do not immediately break.
    /// Prefer InteractionExecutor + InventoryGrantActionDefinition for all new content.
    /// This legacy component now grants inventory only; progress flags and once-only behavior are separate systems.
    /// </summary>
    [Obsolete("Use InteractionExecutor with InventoryGrantActionDefinition instead. This legacy component is inventory-only.")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkInteractableTarget))]
    public sealed class InventoryGrantInteractable : NetworkBehaviour, IServerInteractable
    {
        [SerializeField, Min(0.1f)] private float maxInteractDistance = 3f;

        [Header("Inventory Grants")]
        [SerializeField] private InventoryGrantEntry[] grants;

        [Header("Behavior")]
        [SerializeField] private string source = "inventory_grant";

        public float MaxInteractDistance => maxInteractDistance;

        public bool CanInteract(ulong clientId, NetworkObject actor, out string reason)
        {
            reason = string.Empty;

            if (InventoryService.Instance == null)
            {
                reason = "InventoryService is unavailable.";
                return false;
            }

            if (grants == null || grants.Length == 0)
            {
                reason = "No inventory grants are configured.";
                return false;
            }

            for (int i = 0; i < grants.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(grants[i].ItemDefinitionId))
                {
                    reason = $"Grant at index {i} has no item definition ID.";
                    return false;
                }
            }

            return true;
        }

        public void Interact(ulong clientId, NetworkObject actor)
        {
            if (!IsServer)
            {
                return;
            }

            if (!CanInteract(clientId, actor, out string reason))
            {
                Debug.LogWarning($"[InventoryGrantInteractable] Rejected: {reason}", this);
                return;
            }

            for (int i = 0; i < grants.Length; i++)
            {
                InventoryGrantEntry grant = grants[i];
                ServerActionResult result = InventoryService.Instance.GrantItemForClient(
                    clientId,
                    grant.ItemDefinitionId,
                    grant.Quantity,
                    source);

                if (!result.Success)
                {
                    Debug.LogWarning($"[InventoryGrantInteractable] Grant failed: {result}", this);
                    return;
                }
            }
        }
    }
}
