using System;
using Unity.Netcode;
using UnityEngine;

namespace ROC.Networking.Interactions
{
    [DisallowMultipleComponent]
    public sealed class PlayerInteractionSelector : NetworkBehaviour
    {
        public static PlayerInteractionSelector Local { get; private set; }
        public static event Action<PlayerInteractionSelector> LocalSelectorReady;

        [Header("References")]
        [SerializeField] private PlayerInteractionSensor interactionSensor;
        [SerializeField] private Transform cameraTransformOverride;

        [Header("Selection Timing")]
        [SerializeField, Min(0.01f)] private float rescoreIntervalSeconds = 0.08f;

        [Header("Selection Origin")]
        [SerializeField] private float selectionOriginHeight = 1.0f;

        [Header("Facing Rules")]
        [Range(-1f, 1f)]
        [SerializeField] private float minimumFacingDot = 0.0f;

        [SerializeField, Min(0f)] private float closeRangeOverrideDistance = 1.0f;

        [Header("Line Of Sight")]
        [SerializeField] private bool requireLineOfSight = true;

        [Tooltip("Layers that block interaction line of sight. Usually world/architecture/props. Exclude player and trigger-only interaction layers.")]
        [SerializeField] private LayerMask lineOfSightBlockers = ~0;

        [Header("Stickiness")]
        [SerializeField, Min(0f)] private float targetLostGraceSeconds = 0.18f;
        [SerializeField, Min(0f)] private float switchScoreMargin = 8f;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging;

        private Transform _cameraTransform;
        private float _nextRescoreTime;
        private float _currentTargetInvalidSince = -1f;

        public NetworkInteractableTarget CurrentTarget { get; private set; }

        public event Action<NetworkInteractableTarget> CurrentTargetChanged;

        private void Awake()
        {
            if (interactionSensor == null)
            {
                interactionSensor = GetComponent<PlayerInteractionSensor>();
            }
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                enabled = false;
                return;
            }

            Local = this;
            LocalSelectorReady?.Invoke(this);

            CacheCamera();

            if (interactionSensor != null)
            {
                interactionSensor.NearbySetChanged += HandleNearbySetChanged;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (interactionSensor != null)
            {
                interactionSensor.NearbySetChanged -= HandleNearbySetChanged;
            }

            if (Local == this)
            {
                Local = null;
            }

            SetCurrentTarget(null);
        }

        private void Update()
        {
            if (!IsOwner)
            {
                return;
            }

            if (_cameraTransform == null)
            {
                CacheCamera();
            }

            if (interactionSensor == null)
            {
                return;
            }

            if (Time.time < _nextRescoreTime)
            {
                return;
            }

            RescoreCurrentTarget();
            _nextRescoreTime = Time.time + rescoreIntervalSeconds;
        }

        public void ForceRefresh()
        {
            RescoreCurrentTarget();
            _nextRescoreTime = Time.time + rescoreIntervalSeconds;
        }

        private void HandleNearbySetChanged()
        {
            ForceRefresh();
        }

        private void RescoreCurrentTarget()
        {
            interactionSensor.CleanupNulls();

            Vector3 origin = GetSelectionOrigin();
            Vector3 forward = GetSelectionForward();

            NetworkInteractableTarget bestCandidate = null;
            float bestScore = float.NegativeInfinity;
            float currentTargetScore = float.NegativeInfinity;

            foreach (NetworkInteractableTarget candidate in interactionSensor.NearbyTargets)
            {
                if (candidate == null || !candidate.IsSelectable())
                {
                    continue;
                }

                float score = ScoreTarget(origin, forward, candidate);

                if (candidate == CurrentTarget)
                {
                    currentTargetScore = score;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCandidate = candidate;
                }
            }

            bool currentTargetIsValid =
                CurrentTarget != null &&
                currentTargetScore > float.NegativeInfinity;

            if (currentTargetIsValid)
            {
                _currentTargetInvalidSince = -1f;

                if (bestCandidate == null || bestCandidate == CurrentTarget)
                {
                    return;
                }

                if (bestScore < currentTargetScore + switchScoreMargin)
                {
                    return;
                }

                SetCurrentTarget(bestCandidate);
                return;
            }

            if (CurrentTarget != null && !currentTargetIsValid)
            {
                if (_currentTargetInvalidSince < 0f)
                {
                    _currentTargetInvalidSince = Time.time;
                }

                if (Time.time - _currentTargetInvalidSince < targetLostGraceSeconds)
                {
                    return;
                }

                _currentTargetInvalidSince = -1f;
                SetCurrentTarget(bestCandidate);
                return;
            }

            _currentTargetInvalidSince = -1f;
            SetCurrentTarget(bestCandidate);
        }

        private float ScoreTarget(
            Vector3 origin,
            Vector3 forward,
            NetworkInteractableTarget target)
        {
            Vector3 distancePoint = target.GetInteractionEvaluationPoint(origin);
            Vector3 focusPoint = target.GetBestInteractionFocusPosition(origin);

            float distance = Vector3.Distance(origin, distancePoint);

            if (target.MaxSelectionDistance > 0f && distance > target.MaxSelectionDistance)
            {
                return float.NegativeInfinity;
            }

            bool useCloseRangeOverride = distance <= closeRangeOverrideDistance;

            if (!useCloseRangeOverride)
            {
                Vector3 toFocus = focusPoint - origin;
                float focusDistance = toFocus.magnitude;

                if (focusDistance <= 0.001f)
                {
                    return float.PositiveInfinity;
                }

                Vector3 directionToFocus = toFocus / focusDistance;
                float facingDot = Vector3.Dot(forward, directionToFocus);

                if (facingDot < minimumFacingDot)
                {
                    return float.NegativeInfinity;
                }

                if (requireLineOfSight && !HasLineOfSight(origin, focusPoint, target))
                {
                    return float.NegativeInfinity;
                }

                return
                    facingDot * 100f -
                    distance * 12f +
                    target.SelectionPriorityBonus;
            }

            return
                100f -
                distance * 12f +
                target.SelectionPriorityBonus;
        }

        private bool HasLineOfSight(
            Vector3 origin,
            Vector3 targetPosition,
            NetworkInteractableTarget candidate)
        {
            Vector3 toTarget = targetPosition - origin;
            float distance = toTarget.magnitude;

            if (distance <= 0.001f)
            {
                return true;
            }

            Vector3 direction = toTarget / distance;

            if (Physics.Raycast(
                    origin,
                    direction,
                    out RaycastHit hit,
                    distance,
                    lineOfSightBlockers,
                    QueryTriggerInteraction.Ignore))
            {
                NetworkInteractableTarget hitTarget =
                    hit.collider.GetComponentInParent<NetworkInteractableTarget>();

                return hitTarget == candidate;
            }

            return true;
        }

        private void SetCurrentTarget(NetworkInteractableTarget newTarget)
        {
            if (newTarget == CurrentTarget)
            {
                return;
            }

            CurrentTarget = newTarget;

            if (verboseLogging)
            {
                string targetName = CurrentTarget != null ? CurrentTarget.name : "null";
                Debug.Log($"[PlayerInteractionSelector] Current target changed to '{targetName}'.", this);
            }

            CurrentTargetChanged?.Invoke(CurrentTarget);
        }

        private Vector3 GetSelectionOrigin()
        {
            return transform.position + Vector3.up * selectionOriginHeight;
        }

        private Vector3 GetSelectionForward()
        {
            Transform referenceTransform = _cameraTransform != null ? _cameraTransform : transform;

            Vector3 forward = referenceTransform.forward;

            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = transform.forward;
            }

            forward.Normalize();
            return forward;
        }

        private void CacheCamera()
        {
            if (cameraTransformOverride != null)
            {
                _cameraTransform = cameraTransformOverride;
                return;
            }

            if (UnityEngine.Camera.main != null)
            {
                _cameraTransform = UnityEngine.Camera.main.transform;
            }
        }
    }
}