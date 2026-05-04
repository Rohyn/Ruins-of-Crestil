using UnityEngine;

namespace ROC.Presentation.Cameras
{
    [DisallowMultipleComponent]
    public sealed class PlayerCameraSafetyRig : MonoBehaviour
    {
        [Header("Required Runtime References")]
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private Transform ownerRoot;
        [SerializeField] private Camera targetCamera;

        [Header("Desired Third-Person Offset")]
        [SerializeField] private Vector3 desiredLocalOffset = new(0f, 0.5f, -4.5f);
        [SerializeField] private Vector3 castOriginLocalOffset = new(0f, 0.25f, 0f);

        [Header("Collision")]
        [SerializeField] private LayerMask obstructionMask = ~0;
        [SerializeField, Min(0.01f)] private float collisionRadius = 0.25f;
        [SerializeField, Min(0f)] private float surfacePadding = 0.08f;
        [SerializeField, Min(0.05f)] private float minimumDistance = 0.45f;
        [SerializeField] private bool ignoreTriggers = true;
        [SerializeField] private bool ignoreOwnerColliders = true;

        [Header("Smoothing")]
        [SerializeField, Min(0.001f)] private float blockedSmoothTime = 0.035f;
        [SerializeField, Min(0.001f)] private float restoreSmoothTime = 0.12f;

        [Header("Camera")]
        [SerializeField, Min(0.01f)] private float nearClipPlane = 0.03f;
        [SerializeField] private bool forcePivotRotation = true;

        [Header("Debug")]
        [SerializeField] private bool drawDebugGizmos;

        private const int MaxHits = 16;

        private readonly RaycastHit[] _hits = new RaycastHit[MaxHits];

        private Transform _cameraTransform;
        private float _currentDistance;
        private float _distanceVelocity;
        private bool _isConfigured;

        private Vector3 _lastCastOrigin;
        private Vector3 _lastDesiredPosition;
        private Vector3 _lastResolvedPosition;
        private bool _lastWasBlocked;

        public void Configure(
            Transform pivot,
            Transform owner,
            UnityEngine.Camera cameraToControl,
            Vector3 desiredOffset,
            LayerMask cameraObstructionMask,
            float radius,
            float padding,
            float minDistance,
            float blockedSmooth,
            float restoreSmooth,
            float clipPlane)
        {
            cameraPivot = pivot;
            ownerRoot = owner;
            targetCamera = cameraToControl;
            desiredLocalOffset = desiredOffset;
            obstructionMask = cameraObstructionMask;
            collisionRadius = Mathf.Max(0.01f, radius);
            surfacePadding = Mathf.Max(0f, padding);
            minimumDistance = Mathf.Max(0.05f, minDistance);
            blockedSmoothTime = Mathf.Max(0.001f, blockedSmooth);
            restoreSmoothTime = Mathf.Max(0.001f, restoreSmooth);
            nearClipPlane = Mathf.Max(0.01f, clipPlane);

            _cameraTransform = targetCamera != null ? targetCamera.transform : transform;

            ApplyCameraSettings();
            ResetRuntimeDistance();

            _isConfigured = cameraPivot != null && _cameraTransform != null;
        }

        public void Clear()
        {
            _isConfigured = false;
            cameraPivot = null;
            ownerRoot = null;
            targetCamera = null;
            _cameraTransform = null;
            _distanceVelocity = 0f;
        }

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
            }

            _cameraTransform = targetCamera != null ? targetCamera.transform : transform;

            ApplyCameraSettings();
            ResetRuntimeDistance();

            _isConfigured = cameraPivot != null && _cameraTransform != null;
        }

        private void OnEnable()
        {
            ApplyCameraSettings();
            ResetRuntimeDistance();
        }

        private void LateUpdate()
        {
            if (!_isConfigured)
            {
                TrySelfConfigure();
            }

            if (!_isConfigured || cameraPivot == null || _cameraTransform == null)
            {
                return;
            }

            ResolveCameraPosition();

            if (forcePivotRotation)
            {
                _cameraTransform.rotation = cameraPivot.rotation;
            }
        }

        private void TrySelfConfigure()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
            }

            _cameraTransform = targetCamera != null ? targetCamera.transform : transform;
            _isConfigured = cameraPivot != null && _cameraTransform != null;

            if (_isConfigured)
            {
                ApplyCameraSettings();
                ResetRuntimeDistance();
            }
        }

        private void ResolveCameraPosition()
        {
            Vector3 castOrigin = cameraPivot.TransformPoint(castOriginLocalOffset);
            Vector3 desiredPosition = cameraPivot.TransformPoint(desiredLocalOffset);
            Vector3 desiredVector = desiredPosition - castOrigin;

            float desiredDistance = desiredVector.magnitude;

            if (desiredDistance <= 0.001f)
            {
                _cameraTransform.position = desiredPosition;
                return;
            }

            Vector3 castDirection = desiredVector / desiredDistance;

            bool blocked = TryFindNearestValidObstruction(
                castOrigin,
                castDirection,
                desiredDistance,
                out RaycastHit nearestHit);

            float targetDistance = desiredDistance;

            if (blocked)
            {
                targetDistance = Mathf.Clamp(
                    nearestHit.distance - surfacePadding,
                    minimumDistance,
                    desiredDistance);
            }

            float smoothTime = targetDistance < _currentDistance
                ? blockedSmoothTime
                : restoreSmoothTime;

            _currentDistance = Mathf.SmoothDamp(
                _currentDistance,
                targetDistance,
                ref _distanceVelocity,
                smoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime);

            _currentDistance = Mathf.Clamp(_currentDistance, minimumDistance, desiredDistance);

            Vector3 resolvedPosition = castOrigin + castDirection * _currentDistance;
            _cameraTransform.position = resolvedPosition;

            _lastCastOrigin = castOrigin;
            _lastDesiredPosition = desiredPosition;
            _lastResolvedPosition = resolvedPosition;
            _lastWasBlocked = blocked;
        }

        private bool TryFindNearestValidObstruction(
            Vector3 origin,
            Vector3 direction,
            float distance,
            out RaycastHit nearestHit)
        {
            nearestHit = default;

            QueryTriggerInteraction triggerInteraction = ignoreTriggers
                ? QueryTriggerInteraction.Ignore
                : QueryTriggerInteraction.Collide;

            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                collisionRadius,
                direction,
                _hits,
                distance,
                obstructionMask,
                triggerInteraction);

            bool found = false;
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _hits[i];

                if (hit.collider == null)
                {
                    continue;
                }

                if (!IsValidObstruction(hit.collider))
                {
                    continue;
                }

                if (hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    nearestHit = hit;
                    found = true;
                }
            }

            return found;
        }

        private bool IsValidObstruction(Collider collider)
        {
            if (collider == null)
            {
                return false;
            }

            Transform colliderTransform = collider.transform;

            if (ignoreOwnerColliders && ownerRoot != null && colliderTransform.IsChildOf(ownerRoot))
            {
                return false;
            }

            if (_cameraTransform != null && colliderTransform.IsChildOf(_cameraTransform))
            {
                return false;
            }

            return true;
        }

        private void ApplyCameraSettings()
        {
            if (targetCamera == null)
            {
                return;
            }

            targetCamera.nearClipPlane = Mathf.Max(0.01f, nearClipPlane);
        }

        private void ResetRuntimeDistance()
        {
            Vector3 castOrigin = cameraPivot != null
                ? cameraPivot.TransformPoint(castOriginLocalOffset)
                : Vector3.zero;

            Vector3 desiredPosition = cameraPivot != null
                ? cameraPivot.TransformPoint(desiredLocalOffset)
                : desiredLocalOffset;

            _currentDistance = Mathf.Max(minimumDistance, Vector3.Distance(castOrigin, desiredPosition));
            _distanceVelocity = 0f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            collisionRadius = Mathf.Max(0.01f, collisionRadius);
            surfacePadding = Mathf.Max(0f, surfacePadding);
            minimumDistance = Mathf.Max(0.05f, minimumDistance);
            blockedSmoothTime = Mathf.Max(0.001f, blockedSmoothTime);
            restoreSmoothTime = Mathf.Max(0.001f, restoreSmoothTime);
            nearClipPlane = Mathf.Max(0.01f, nearClipPlane);
        }
#endif

        private void OnDrawGizmosSelected()
        {
            if (!drawDebugGizmos)
            {
                return;
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_lastCastOrigin, collisionRadius);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_lastDesiredPosition, collisionRadius);
            Gizmos.DrawLine(_lastCastOrigin, _lastDesiredPosition);

            Gizmos.color = _lastWasBlocked ? Color.red : Color.green;
            Gizmos.DrawWireSphere(_lastResolvedPosition, collisionRadius);
            Gizmos.DrawLine(_lastCastOrigin, _lastResolvedPosition);
        }
    }
}