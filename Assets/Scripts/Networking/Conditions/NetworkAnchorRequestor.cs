using ROC.Game.Common;
using ROC.Game.Conditions;
using Unity.Netcode;
using UnityEngine;

namespace ROC.Networking.Conditions
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkAnchorRequestor : NetworkBehaviour
    {
        public void RequestReleaseAnchor()
        {
            if (!IsOwner)
            {
                return;
            }

            ReleaseAnchorServerRpc();
        }

        [ServerRpc]
        private void ReleaseAnchorServerRpc(ServerRpcParams serverRpcParams = default)
        {
            ulong clientId = serverRpcParams.Receive.SenderClientId;

            if (clientId != OwnerClientId)
            {
                Debug.LogWarning("[NetworkAnchorRequestor] Rejected release-anchor request: ownership mismatch.");
                return;
            }

            ServerActionResult result =
                AnchoringService.Instance != null
                    ? AnchoringService.Instance.ReleaseAnchor(clientId)
                    : ServerActionResult.Fail(
                        ServerActionErrorCode.InvalidState,
                        "Anchoring service is unavailable.");

            if (!result.Success)
            {
                Debug.LogWarning($"[NetworkAnchorRequestor] Release-anchor failed: {result}");
            }
        }
    }
}