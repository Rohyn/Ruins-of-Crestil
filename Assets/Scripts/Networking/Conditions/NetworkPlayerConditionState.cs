using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ROC.Networking.Conditions
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkPlayerConditionState : NetworkBehaviour
    {
        public static NetworkPlayerConditionState Local { get; private set; }

        public readonly NetworkVariable<PlayerControlBlockFlags> ControlBlocks = new(
            PlayerControlBlockFlags.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<bool> IsAnchored = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<FixedString64Bytes> AnchorExitPrompt = new(
            default,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);

        public bool BlocksMovement => (ControlBlocks.Value & PlayerControlBlockFlags.Movement) != 0;
        public bool BlocksJump => (ControlBlocks.Value & PlayerControlBlockFlags.Jump) != 0;
        public bool BlocksSprint => (ControlBlocks.Value & PlayerControlBlockFlags.Sprint) != 0;
        public bool BlocksGravity => (ControlBlocks.Value & PlayerControlBlockFlags.Gravity) != 0;

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                Local = this;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (Local == this)
            {
                Local = null;
            }
        }

        public void SetControlBlocksServer(PlayerControlBlockFlags flags)
        {
            if (!IsServer || !IsSpawned)
            {
                return;
            }

            ControlBlocks.Value = flags;
        }

        public void SetAnchorPresentationServer(bool isAnchored, string exitPrompt)
        {
            if (!IsServer || !IsSpawned)
            {
                return;
            }

            IsAnchored.Value = isAnchored;
            AnchorExitPrompt.Value = new FixedString64Bytes(exitPrompt ?? string.Empty);
        }
    }
}