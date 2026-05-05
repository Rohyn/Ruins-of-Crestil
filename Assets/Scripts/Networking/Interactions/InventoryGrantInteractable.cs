using ROC.Game.Common;
using ROC.Game.ProgressFlags;
using ROC.Networking.Inventory;
using Unity.Netcode;
using UnityEngine;

namespace ROC.Networking.Interactions
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkInteractableTarget))]
    public sealed class InventoryGrantInteractable : NetworkBehaviour, IServerInteractable
    {
        [SerializeField, Min(0.1f)] private float maxInteractDistance = 3f;

        [Header("Progress Requirements")]
        [SerializeField] private ProgressFlagRequirement[] progressRequirements;

        [Header("Inventory Grants")]
        [SerializeField] private InventoryGrantEntry[] grants;

        [Header("Progress Results")]
        [SerializeField] private ProgressFlagMutation[] successProgressMutations;

        [Header("Behavior")]
        [SerializeField] private string source = "inventory_grant";
        [SerializeField] private bool disableAfterSuccessfulUse;

        public float MaxInteractDistance => maxInteractDistance;

        private bool _used;

        public bool CanInteract(ulong clientId, NetworkObject actor, out string reason)
        {
            reason = string.Empty;

            if (_used && disableAfterSuccessfulUse)
            {
                reason = "Already used.";
                return false;
            }

            if (InventoryService.Instance == null)
            {
                reason = "Inventory service is unavailable.";
                return false;
            }

            if (ProgressFlagService.Instance != null)
            {
                ServerActionResult requirements =
                    ProgressFlagService.Instance.EvaluateRequirementsForClient(
                        clientId,
                        progressRequirements);

                if (!requirements.Success)
                {
                    reason = requirements.Message;
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

            if (ProgressFlagService.Instance != null)
            {
                ServerActionResult mutations =
                    ProgressFlagService.Instance.ApplyMutationsForClient(
                        clientId,
                        successProgressMutations);

                if (!mutations.Success)
                {
                    Debug.LogWarning($"[InventoryGrantInteractable] Progress mutation failed: {mutations}", this);
                }
            }

            _used = true;
        }
    }
}