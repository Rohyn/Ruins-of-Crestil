using System.Collections.Generic;
using ROC.Networking.World;
using Unity.Netcode;
using UnityEngine;

namespace ROC.Networking.Interactions
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkInstanceObject))]
    public sealed class PerPlayerPickupInteractable : NetworkBehaviour, IServerInteractable, IInstanceVisibilityRule
    {
        [SerializeField, Min(0.1f)] private float maxInteractDistance = 3f;

        private readonly HashSet<ulong> _collectedClientIds = new();
        private NetworkInstanceObject _instanceObject;

        public float MaxInteractDistance => maxInteractDistance;

        private void Awake()
        {
            _instanceObject = GetComponent<NetworkInstanceObject>();
        }

        public bool IsVisibleToClient(ulong clientId)
        {
            return !_collectedClientIds.Contains(clientId);
        }

        public bool CanInteract(ulong clientId, NetworkObject actor, out string reason)
        {
            if (_collectedClientIds.Contains(clientId))
            {
                reason = "Already collected by this client.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public void Interact(ulong clientId, NetworkObject actor)
        {
            if (!IsServer)
            {
                return;
            }

            _collectedClientIds.Add(clientId);

            InstanceVisibilityService.Instance?.RefreshObject(_instanceObject);

            Debug.Log($"[PerPlayerPickupInteractable] Client {clientId} collected pickup.");
        }
    }
}