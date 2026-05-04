using System.Collections.Generic;
using ROC.Game.Common;
using ROC.Game.ProgressFlags;
using ROC.Networking.Characters;
using Unity.Netcode;
using UnityEngine;

namespace ROC.Networking.ProgressFlags
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class ProgressFlagTriggerVolume : MonoBehaviour
    {
        [Header("Requirements")]
        [SerializeField] private ProgressFlagRequirement[] requirements;

        [Header("On Enter")]
        [SerializeField] private ProgressFlagMutation[] onEnterMutations;

        [Header("On Exit")]
        [SerializeField] private ProgressFlagMutation[] onExitMutations;

        [Header("Behavior")]
        [SerializeField] private bool applyOnlyOncePerClient = true;

        private readonly HashSet<ulong> _appliedEnterClients = new();

        private void Reset()
        {
            Collider trigger = GetComponent<Collider>();
            trigger.isTrigger = true;
        }

        private void Awake()
        {
            Collider trigger = GetComponent<Collider>();

            if (trigger != null)
            {
                trigger.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServerRunning())
            {
                return;
            }

            if (!TryGetClientId(other, out ulong clientId))
            {
                return;
            }

            if (applyOnlyOncePerClient && _appliedEnterClients.Contains(clientId))
            {
                return;
            }

            ProgressFlagService service = ProgressFlagService.Instance;

            if (service == null)
            {
                return;
            }

            ServerActionResult requirementResult =
                service.EvaluateRequirementsForClient(clientId, requirements);

            if (!requirementResult.Success)
            {
                return;
            }

            ServerActionResult mutationResult =
                service.ApplyMutationsForClient(clientId, onEnterMutations);

            if (!mutationResult.Success)
            {
                Debug.LogWarning($"[ProgressFlagTriggerVolume] OnEnter mutation failed: {mutationResult}", this);
                return;
            }

            _appliedEnterClients.Add(clientId);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsServerRunning())
            {
                return;
            }

            if (!TryGetClientId(other, out ulong clientId))
            {
                return;
            }

            ProgressFlagService service = ProgressFlagService.Instance;

            if (service == null)
            {
                return;
            }

            ServerActionResult requirementResult =
                service.EvaluateRequirementsForClient(clientId, requirements);

            if (!requirementResult.Success)
            {
                return;
            }

            ServerActionResult mutationResult =
                service.ApplyMutationsForClient(clientId, onExitMutations);

            if (!mutationResult.Success)
            {
                Debug.LogWarning($"[ProgressFlagTriggerVolume] OnExit mutation failed: {mutationResult}", this);
            }
        }

        private static bool TryGetClientId(Collider other, out ulong clientId)
        {
            clientId = default;

            if (other == null)
            {
                return false;
            }

            NetworkPlayerAvatar avatar =
                other.GetComponentInParent<NetworkPlayerAvatar>();

            if (avatar == null || avatar.NetworkObject == null || !avatar.NetworkObject.IsSpawned)
            {
                return false;
            }

            clientId = avatar.OwnerClientId;
            return true;
        }

        private static bool IsServerRunning()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            return networkManager != null && networkManager.IsServer;
        }
    }
}