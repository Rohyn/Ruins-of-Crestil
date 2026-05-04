using ROC.Game.Common;
using ROC.Game.Conditions;
using Unity.Netcode;
using UnityEngine;

namespace ROC.Networking.Interactions
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkInteractableTarget))]
    public sealed class RestableInteractable : NetworkBehaviour, IServerInteractable
    {
        [Header("Anchors")]
        [SerializeField] private Transform restAnchor;
        [SerializeField] private Transform exitAnchor;

        [Header("Resting")]
        [SerializeField] private string restSourceId = "restable.object";
        [SerializeField] private string restingConditionId = "condition.resting";
        [SerializeField] private string exitPrompt = "Get Up";

        [Header("Interaction")]
        [SerializeField, Min(0.1f)] private float maxInteractDistance = 3f;

        public Transform RestAnchor => restAnchor;
        public Transform ExitAnchor => exitAnchor;
        public string RestSourceId => restSourceId;
        public string RestingConditionId => restingConditionId;
        public string ExitPrompt => exitPrompt;
        public float MaxInteractDistance => maxInteractDistance;

        public bool CanInteract(ulong clientId, NetworkObject actor, out string reason)
        {
            reason = string.Empty;

            if (AnchoringService.Instance == null)
            {
                reason = "Anchoring service is unavailable.";
                return false;
            }

            if (AnchoringService.Instance.IsAnchored(clientId))
            {
                reason = "Already anchored.";
                return false;
            }

            if (actor == null)
            {
                reason = "No actor available.";
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

            if (AnchoringService.Instance == null)
            {
                Debug.LogWarning("[RestableInteractable] AnchoringService is unavailable.", this);
                return;
            }

            Transform resolvedRestAnchor = restAnchor != null ? restAnchor : transform;
            Transform resolvedExitAnchor = exitAnchor != null ? exitAnchor : resolvedRestAnchor;

            ServerActionResult result = AnchoringService.Instance.BeginAnchor(
                clientId,
                actor,
                resolvedRestAnchor,
                resolvedExitAnchor,
                restSourceId,
                exitPrompt,
                restingConditionId,
                ConditionSourceType.Interaction);

            if (!result.Success)
            {
                Debug.LogWarning($"[RestableInteractable] Failed to begin rest anchor: {result}", this);
            }
        }
    }
}