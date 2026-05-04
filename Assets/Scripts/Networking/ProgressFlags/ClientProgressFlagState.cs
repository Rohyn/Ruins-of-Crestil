using System;
using System.Collections.Generic;
using ROC.Game.ProgressFlags;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ROC.Networking.ProgressFlags
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class ClientProgressFlagState : NetworkBehaviour
    {
        public static ClientProgressFlagState Local { get; private set; }

        public event Action<string, ProgressFlagLifetime> FlagSet;
        public event Action<string> FlagRemoved;
        public event Action<string> PrefixCleared;

        private readonly HashSet<string> _knownFlags = new();

        public IReadOnlyCollection<string> KnownFlags => _knownFlags;

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

            _knownFlags.Clear();
        }

        public bool HasFlag(string flagId)
        {
            string normalized = ProgressFlagIdUtility.NormalizeFlagId(flagId);
            return _knownFlags.Contains(normalized);
        }

        public void SendFlagSet(
            ulong targetClientId,
            string flagId,
            ProgressFlagLifetime lifetime)
        {
            if (!IsServer)
            {
                return;
            }

            ReceiveFlagSetClientRpc(
                new FixedString128Bytes(flagId),
                lifetime,
                TargetClient(targetClientId));
        }

        public void SendFlagRemoved(
            ulong targetClientId,
            string flagId)
        {
            if (!IsServer)
            {
                return;
            }

            ReceiveFlagRemovedClientRpc(
                new FixedString128Bytes(flagId),
                TargetClient(targetClientId));
        }

        public void SendPrefixCleared(
            ulong targetClientId,
            string prefix)
        {
            if (!IsServer)
            {
                return;
            }

            ReceivePrefixClearedClientRpc(
                new FixedString128Bytes(prefix),
                TargetClient(targetClientId));
        }

        [ClientRpc]
        private void ReceiveFlagSetClientRpc(
            FixedString128Bytes flagId,
            ProgressFlagLifetime lifetime,
            ClientRpcParams clientRpcParams = default)
        {
            if (!IsOwner)
            {
                return;
            }

            string normalized = ProgressFlagIdUtility.NormalizeFlagId(flagId.ToString());
            _knownFlags.Add(normalized);
            FlagSet?.Invoke(normalized, lifetime);
        }

        [ClientRpc]
        private void ReceiveFlagRemovedClientRpc(
            FixedString128Bytes flagId,
            ClientRpcParams clientRpcParams = default)
        {
            if (!IsOwner)
            {
                return;
            }

            string normalized = ProgressFlagIdUtility.NormalizeFlagId(flagId.ToString());
            _knownFlags.Remove(normalized);
            FlagRemoved?.Invoke(normalized);
        }

        [ClientRpc]
        private void ReceivePrefixClearedClientRpc(
            FixedString128Bytes prefix,
            ClientRpcParams clientRpcParams = default)
        {
            if (!IsOwner)
            {
                return;
            }

            string normalizedPrefix = ProgressFlagIdUtility.NormalizePrefix(prefix.ToString());

            List<string> toRemove = null;

            foreach (string flagId in _knownFlags)
            {
                if (!flagId.StartsWith(normalizedPrefix))
                {
                    continue;
                }

                toRemove ??= new List<string>();
                toRemove.Add(flagId);
            }

            if (toRemove != null)
            {
                for (int i = 0; i < toRemove.Count; i++)
                {
                    _knownFlags.Remove(toRemove[i]);
                }
            }

            PrefixCleared?.Invoke(normalizedPrefix);
        }

        private static ClientRpcParams TargetClient(ulong clientId)
        {
            return new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { clientId }
                }
            };
        }
    }
}