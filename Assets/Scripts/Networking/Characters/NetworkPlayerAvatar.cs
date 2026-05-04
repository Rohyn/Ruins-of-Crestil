using ROC.Game.World;
using ROC.Networking.World;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ROC.Networking.Characters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkPlayerAvatar : NetworkBehaviour
    {
        public static NetworkPlayerAvatar Local { get; private set; }

        public readonly NetworkVariable<FixedString64Bytes> CharacterId = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<FixedString64Bytes> DisplayName = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<FixedString64Bytes> SceneId = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<FixedString128Bytes> InstanceId = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private string _pendingAccountId;
        private string _pendingCharacterId;
        private string _pendingDisplayName;
        private WorldLocation _pendingLocation;
        private bool _hasPendingInitialization;

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                Local = this;
            }

            if (IsServer && _hasPendingInitialization)
            {
                ApplyServerInitialization();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (Local == this)
            {
                Local = null;
            }
        }

        public void InitializeServer(
            string accountId,
            string characterId,
            string displayName,
            WorldLocation location)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                Debug.LogError("[NetworkPlayerAvatar] InitializeServer called outside server context.");
                return;
            }

            _pendingAccountId = accountId;
            _pendingCharacterId = characterId;
            _pendingDisplayName = displayName;
            _pendingLocation = location;
            _hasPendingInitialization = true;

            if (IsSpawned)
            {
                ApplyServerInitialization();
            }
        }

        private void ApplyServerInitialization()
        {
            CharacterId.Value = new FixedString64Bytes(_pendingCharacterId ?? string.Empty);
            DisplayName.Value = new FixedString64Bytes(_pendingDisplayName ?? string.Empty);
            SceneId.Value = new FixedString64Bytes(_pendingLocation.SceneId ?? string.Empty);
            InstanceId.Value = new FixedString128Bytes(_pendingLocation.InstanceId ?? string.Empty);

            if (TryGetComponent(out ServerPlayerLocationTracker tracker))
            {
                tracker.InitializeServer(_pendingAccountId, _pendingCharacterId);
            }

            _hasPendingInitialization = false;
        }
    }
}