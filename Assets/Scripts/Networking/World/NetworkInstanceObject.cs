using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ROC.Networking.World
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkInstanceObject : NetworkBehaviour
    {
        public readonly NetworkVariable<FixedString128Bytes> InstanceId = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<FixedString64Bytes> StableObjectId = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly List<IInstanceVisibilityRule> _visibilityRules = new();

        private NetworkObject _networkObject;

        private string _serverInstanceId;
        private string _serverStableObjectId;
        private bool _hasPendingServerState;

        public string InstanceIdString => IsServer
            ? _serverInstanceId
            : InstanceId.Value.ToString();

        private void Awake()
        {
            _networkObject = GetComponent<NetworkObject>();
            _networkObject.CheckObjectVisibility = CheckObjectVisibility;

            CacheVisibilityRules();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                if (_hasPendingServerState)
                {
                    ApplyServerState();
                }

                InstanceVisibilityService.Instance?.Register(this);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                InstanceVisibilityService.Instance?.Unregister(this);
            }
        }

        public override void OnDestroy()
        {
            if (_networkObject != null)
            {
                _networkObject.CheckObjectVisibility = null;
            }

            base.OnDestroy();
        }

        public void InitializeServer(string instanceId, string stableObjectId)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                Debug.LogError("[NetworkInstanceObject] InitializeServer called outside server context.");
                return;
            }

            _serverInstanceId = instanceId ?? string.Empty;
            _serverStableObjectId = stableObjectId ?? string.Empty;
            _hasPendingServerState = true;

            if (IsSpawned)
            {
                ApplyServerState();
                InstanceVisibilityService.Instance?.RefreshObject(this);
            }
        }

        public bool ShouldBeVisibleTo(ulong clientId)
        {
            if (string.IsNullOrWhiteSpace(_serverInstanceId))
            {
                return false;
            }

            if (WorldInstanceManager.Instance == null ||
                !WorldInstanceManager.Instance.AreClientAndInstanceObjectTogether(clientId, _serverInstanceId))
            {
                return false;
            }

            for (int i = 0; i < _visibilityRules.Count; i++)
            {
                if (!_visibilityRules[i].IsVisibleToClient(clientId))
                {
                    return false;
                }
            }

            return true;
        }

        private void ApplyServerState()
        {
            InstanceId.Value = new FixedString128Bytes(_serverInstanceId);
            StableObjectId.Value = new FixedString64Bytes(_serverStableObjectId);
            _hasPendingServerState = false;
        }

        private bool CheckObjectVisibility(ulong clientId)
        {
            return ShouldBeVisibleTo(clientId);
        }

        private void CacheVisibilityRules()
        {
            _visibilityRules.Clear();

            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();

            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IInstanceVisibilityRule rule)
                {
                    _visibilityRules.Add(rule);
                }
            }
        }
    }
}