using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ROC.Networking.Interactions
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkInteractableTarget : NetworkBehaviour
    {
        [Header("Display")]
        [SerializeField] private string interactionPrompt = "Interact";

        [Header("Selection")]
        [Tooltip("Higher values make this object more likely to be selected when competing with nearby interactables.")]
        [SerializeField] private float selectionPriorityBonus;

        [Tooltip("Maximum client-side selection distance. Set to 0 or less to ignore this client-side limit.")]
        [SerializeField] private float maxSelectionDistance = 0f;

        [Header("Interaction Geometry")]
        [Tooltip("Optional colliders used for selection/evaluation. If empty, child colliders are discovered at runtime.")]
        [SerializeField] private List<Collider> interactionColliders = new();

        [Tooltip("Optional focus points used for facing/LOS/prompt positioning. If empty, this object's transform is used.")]
        [SerializeField] private List<Transform> interactionFoci = new();

        private readonly List<Collider> _runtimeColliders = new();
        private NetworkObject _networkObject;

        public string InteractionPrompt => interactionPrompt;
        public float SelectionPriorityBonus => selectionPriorityBonus;
        public float MaxSelectionDistance => maxSelectionDistance;

        public ulong TargetNetworkObjectId =>
            _networkObject != null && _networkObject.IsSpawned
                ? _networkObject.NetworkObjectId
                : 0UL;

        private void Awake()
        {
            _networkObject = GetComponent<NetworkObject>();
            RebuildRuntimeColliderCache();
        }

        private void OnEnable()
        {
            RebuildRuntimeColliderCache();
        }

        public bool IsSelectable()
        {
            return isActiveAndEnabled &&
                   _networkObject != null &&
                   _networkObject.IsSpawned;
        }

        public bool TryGetTargetNetworkObject(out NetworkObject networkObject)
        {
            networkObject = _networkObject;
            return networkObject != null && networkObject.IsSpawned;
        }

        public Vector3 GetInteractionEvaluationPoint(Vector3 referencePosition)
        {
            RebuildRuntimeColliderCacheIfNeeded();

            bool found = false;
            Vector3 bestPoint = transform.position;
            float bestSqrDistance = float.MaxValue;

            for (int i = 0; i < _runtimeColliders.Count; i++)
            {
                Collider candidate = _runtimeColliders[i];

                if (candidate == null || !candidate.enabled)
                {
                    continue;
                }

                Vector3 point = candidate.ClosestPoint(referencePosition);
                float sqrDistance = (point - referencePosition).sqrMagnitude;

                if (sqrDistance < bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    bestPoint = point;
                    found = true;
                }
            }

            return found ? bestPoint : transform.position;
        }

        public Vector3 GetBestInteractionFocusPosition(Vector3 referencePosition)
        {
            bool found = false;
            Vector3 bestPoint = transform.position;
            float bestSqrDistance = float.MaxValue;

            for (int i = 0; i < interactionFoci.Count; i++)
            {
                Transform focus = interactionFoci[i];

                if (focus == null)
                {
                    continue;
                }

                float sqrDistance = (focus.position - referencePosition).sqrMagnitude;

                if (sqrDistance < bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    bestPoint = focus.position;
                    found = true;
                }
            }

            return found ? bestPoint : transform.position;
        }

        private void RebuildRuntimeColliderCacheIfNeeded()
        {
            if (_runtimeColliders.Count == 0)
            {
                RebuildRuntimeColliderCache();
            }
        }

        private void RebuildRuntimeColliderCache()
        {
            _runtimeColliders.Clear();

            for (int i = 0; i < interactionColliders.Count; i++)
            {
                Collider assigned = interactionColliders[i];

                if (assigned != null)
                {
                    _runtimeColliders.Add(assigned);
                }
            }

            if (_runtimeColliders.Count > 0)
            {
                return;
            }

            GetComponentsInChildren(includeInactive: false, _runtimeColliders);
        }
    }
}