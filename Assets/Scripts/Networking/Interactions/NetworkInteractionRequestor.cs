using ROC.Game.Common;
using ROC.Networking.Sessions;
using ROC.Networking.World;
using Unity.Netcode;
using UnityEngine;

namespace ROC.Networking.Interactions
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkInteractionRequestor : NetworkBehaviour
    {
        public void RequestInteract(ulong targetNetworkObjectId)
        {
            if (!IsOwner)
            {
                return;
            }

            RequestInteractServerRpc(targetNetworkObjectId);
        }

        [ServerRpc]
        private void RequestInteractServerRpc(
            ulong targetNetworkObjectId,
            ServerRpcParams serverRpcParams = default)
        {
            ulong clientId = serverRpcParams.Receive.SenderClientId;

            ServerActionResult result = TryHandleInteraction(clientId, targetNetworkObjectId);

            if (!result.Success)
            {
                Debug.LogWarning($"[NetworkInteractionRequestor] Interaction rejected: {result}");
            }
        }

        private ServerActionResult TryHandleInteraction(ulong clientId, ulong targetNetworkObjectId)
        {
            if (clientId != OwnerClientId)
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.PermissionDenied,
                    "Client does not own this interaction requestor.");
            }

            PlayerSessionRegistry registry = PlayerSessionRegistry.Instance;

            if (registry == null || !registry.TryGet(clientId, out PlayerSessionData session))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.NoSession,
                    "No server session exists for this client.");
            }

            if (session.State != PlayerSessionState.InWorld)
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.NotInWorld,
                    "Client is not currently in world.");
            }

            if (session.AvatarObject == null || !session.AvatarObject.IsSpawned)
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.InvalidState,
                    "Client has no active avatar.");
            }

            if (NetworkManager.Singleton == null ||
                !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                    targetNetworkObjectId,
                    out NetworkObject target))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.InvalidTarget,
                    "Target object is not spawned.");
            }

            if (!target.TryGetComponent(out NetworkInstanceObject targetInstanceObject))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.InvalidTarget,
                    "Target has no NetworkInstanceObject.");
            }

            if (targetInstanceObject.InstanceIdString != session.InstanceId)
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.DifferentInstance,
                    "Target is in a different instance.");
            }

            if (!targetInstanceObject.ShouldBeVisibleTo(clientId))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.TargetNotVisible,
                    "Target is not visible to this client.");
            }

            if (!TryGetInteractable(target, out IServerInteractable interactable))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.InvalidTarget,
                    "Target is not interactable.");
            }

            float maxDistance = interactable.MaxInteractDistance;
            float sqrDistance = (session.AvatarObject.transform.position - target.transform.position).sqrMagnitude;

            if (sqrDistance > maxDistance * maxDistance)
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.TooFarAway,
                    "Target is too far away.");
            }

            if (!interactable.CanInteract(clientId, session.AvatarObject, out string reason))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.InvalidState,
                    reason);
            }

            interactable.Interact(clientId, session.AvatarObject);

            return ServerActionResult.Ok();
        }

        private static bool TryGetInteractable(
            NetworkObject target,
            out IServerInteractable interactable)
        {
            interactable = null;

            if (target == null)
            {
                return false;
            }

            MonoBehaviour[] behaviours = target.GetComponents<MonoBehaviour>();

            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IServerInteractable candidate)
                {
                    interactable = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}