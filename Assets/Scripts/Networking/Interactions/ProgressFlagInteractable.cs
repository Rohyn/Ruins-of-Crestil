using ROC.Game.Common;
using ROC.Game.ProgressFlags;
using ROC.Networking.World;
using Unity.Netcode;
using UnityEngine;

namespace ROC.Networking.Interactions
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkInstanceObject))]
    public sealed class ProgressFlagInteractable : NetworkBehaviour, IServerInteractable
    {
        [SerializeField, Min(0.1f)] private float maxInteractDistance = 3f;

        [Header("Requirements")]
        [SerializeField] private ProgressFlagRequirement[] requirements;

        [Header("Results")]
        [SerializeField] private ProgressFlagMutation[] mutations;

        public float MaxInteractDistance => maxInteractDistance;

        public bool CanInteract(ulong clientId, NetworkObject actor, out string reason)
        {
            reason = string.Empty;

            ProgressFlagService service = ProgressFlagService.Instance;

            if (service == null)
            {
                reason = "Progress flag service is unavailable.";
                return false;
            }

            ServerActionResult result = service.EvaluateRequirementsForClient(clientId, requirements);

            if (!result.Success)
            {
                reason = result.Message;
                return false;
            }

            return true;
        }

        public void Interact(ulong clientId, NetworkObject actor)
        {
            if (!IsServer)
            {
                return;
            }

            ProgressFlagService service = ProgressFlagService.Instance;

            if (service == null)
            {
                Debug.LogWarning("[ProgressFlagInteractable] Missing ProgressFlagService.", this);
                return;
            }

            ServerActionResult result = service.ApplyMutationsForClient(clientId, mutations);

            if (!result.Success)
            {
                Debug.LogWarning($"[ProgressFlagInteractable] Mutation failed: {result}", this);
            }
        }
    }
}