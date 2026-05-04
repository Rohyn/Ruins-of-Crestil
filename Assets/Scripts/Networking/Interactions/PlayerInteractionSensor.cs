using System;
using System.Collections.Generic;
using ROC.Networking.World;
using Unity.Netcode;
using UnityEngine;

namespace ROC.Networking.Interactions
{
    [DisallowMultipleComponent]
    public sealed class PlayerInteractionSensor : NetworkBehaviour
    {
        private const int MaxOverlapResults = 96;

        [Header("Detection")]
        [SerializeField, Min(0.1f)] private float detectionRadius = 5f;

        [Tooltip("Physics layers to search for interaction colliders.")]
        [SerializeField] private LayerMask detectionMask = ~0;

        [SerializeField, Min(0.01f)] private float refreshIntervalSeconds = 0.05f;
        [SerializeField] private QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.Collide;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging;

        private readonly Collider[] _hits = new Collider[MaxOverlapResults];
        private readonly HashSet<NetworkInteractableTarget> _nearbyTargets = new();
        private readonly HashSet<NetworkInteractableTarget> _currentFrameTargets = new();
        private readonly List<NetworkInteractableTarget> _scratchRemove = new();

        private float _nextRefreshTime;

        public event Action NearbySetChanged;

        public IReadOnlyCollection<NetworkInteractableTarget> NearbyTargets => _nearbyTargets;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                enabled = false;
                return;
            }

            ClientWorldSceneStreamer.LocalWorldSceneStreamStarted += HandleWorldSceneChanging;
            ClientWorldSceneStreamer.LocalWorldSceneCleared += HandleWorldSceneChanging;

            _nextRefreshTime = 0f;
        }

        public override void OnNetworkDespawn()
        {
            ClientWorldSceneStreamer.LocalWorldSceneStreamStarted -= HandleWorldSceneChanging;
            ClientWorldSceneStreamer.LocalWorldSceneCleared -= HandleWorldSceneChanging;

            ClearNearby();
        }

        private void OnDisable()
        {
            ClearNearby();
        }

        private void Update()
        {
            if (!IsOwner)
            {
                return;
            }

            if (Time.time < _nextRefreshTime)
            {
                return;
            }

            _nextRefreshTime = Time.time + refreshIntervalSeconds;
            RefreshNearbyTargets();
        }

        public void ForceRefresh()
        {
            RefreshNearbyTargets();
            _nextRefreshTime = Time.time + refreshIntervalSeconds;
        }

        public void CleanupNulls()
        {
            _scratchRemove.Clear();

            foreach (NetworkInteractableTarget target in _nearbyTargets)
            {
                if (target == null || !target.isActiveAndEnabled)
                {
                    _scratchRemove.Add(target);
                }
            }

            if (_scratchRemove.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _scratchRemove.Count; i++)
            {
                _nearbyTargets.Remove(_scratchRemove[i]);
            }

            NearbySetChanged?.Invoke();
        }

        public void RefreshNearbyTargets()
        {
            CleanupNulls();

            _currentFrameTargets.Clear();

            int hitCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                detectionRadius,
                _hits,
                detectionMask,
                queryTriggerInteraction);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _hits[i];

                if (hit == null)
                {
                    continue;
                }

                NetworkInteractableTarget target = hit.GetComponentInParent<NetworkInteractableTarget>();

                if (target == null || !target.IsSelectable())
                {
                    continue;
                }

                _currentFrameTargets.Add(target);
            }

            bool changed = false;

            _scratchRemove.Clear();

            foreach (NetworkInteractableTarget existing in _nearbyTargets)
            {
                if (existing != null && _currentFrameTargets.Contains(existing))
                {
                    continue;
                }

                _scratchRemove.Add(existing);
            }

            for (int i = 0; i < _scratchRemove.Count; i++)
            {
                NetworkInteractableTarget removed = _scratchRemove[i];
                _nearbyTargets.Remove(removed);
                changed = true;

                if (verboseLogging && removed != null)
                {
                    Debug.Log($"[PlayerInteractionSensor] Removed nearby target '{removed.name}'.", this);
                }
            }

            foreach (NetworkInteractableTarget target in _currentFrameTargets)
            {
                if (target == null)
                {
                    continue;
                }

                if (_nearbyTargets.Add(target))
                {
                    changed = true;

                    if (verboseLogging)
                    {
                        Debug.Log($"[PlayerInteractionSensor] Added nearby target '{target.name}'.", this);
                    }
                }
            }

            if (changed)
            {
                NearbySetChanged?.Invoke();
            }
        }

        private void ClearNearby()
        {
            if (_nearbyTargets.Count == 0)
            {
                return;
            }

            _nearbyTargets.Clear();
            NearbySetChanged?.Invoke();
        }

        private void HandleWorldSceneChanging(string sceneId, string instanceId)
        {
            ClearNearby();
        }

        private void HandleWorldSceneChanging()
        {
            ClearNearby();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }
#endif
    }
}