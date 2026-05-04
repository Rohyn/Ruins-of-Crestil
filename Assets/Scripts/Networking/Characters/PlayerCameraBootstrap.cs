using ROC.Presentation.Cameras;
using Unity.Netcode;
using UnityEngine;

namespace ROC.Networking.Characters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PlayerCameraBootstrap : NetworkBehaviour
    {
        [Header("Camera Pivot")]
        [SerializeField] private Transform cameraPivot;

        [Header("Camera Placement")]
        [SerializeField, Min(0.25f)] private float cameraDistance = 4.5f;
        [SerializeField] private float verticalOffset = 0.5f;
        [SerializeField] private float sideOffset = 0f;
        [SerializeField, Range(30f, 100f)] private float cameraFieldOfView = 65f;

        [Header("Camera Collision")]
        [SerializeField] private LayerMask obstructionMask = ~0;
        [SerializeField, Min(0.01f)] private float collisionRadius = 0.25f;
        [SerializeField, Min(0f)] private float surfacePadding = 0.08f;
        [SerializeField, Min(0.05f)] private float minimumCameraDistance = 0.45f;
        [SerializeField, Min(0.001f)] private float blockedSmoothTime = 0.035f;
        [SerializeField, Min(0.001f)] private float restoreSmoothTime = 0.12f;
        [SerializeField, Min(0.01f)] private float nearClipPlane = 0.03f;

        [Header("Camera Creation")]
        [SerializeField] private bool createCameraIfMissing = true;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging;

        private UnityEngine.Camera _attachedCamera;
        private PlayerCameraSafetyRig _safetyRig;
        private bool _createdCameraAtRuntime;
        private bool _addedSafetyRigAtRuntime;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                return;
            }

            AttachLocalCamera();
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner)
            {
                return;
            }

            DetachOrDestroyLocalCamera();
        }

        private void AttachLocalCamera()
        {
            if (cameraPivot == null)
            {
                Debug.LogError("[PlayerCameraBootstrap] No CameraPivot assigned.", this);
                return;
            }

            UnityEngine.Camera cameraToUse = Camera.main;

            if (cameraToUse == null)
            {
                if (!createCameraIfMissing)
                {
                    Debug.LogWarning("[PlayerCameraBootstrap] No Main Camera found and camera creation is disabled.", this);
                    return;
                }

                GameObject cameraObject = new("Local Player Camera");
                cameraObject.tag = "MainCamera";

                cameraToUse = cameraObject.AddComponent<Camera>();

                if (FindFirstObjectByType<AudioListener>() == null)
                {
                    cameraObject.AddComponent<AudioListener>();
                }

                _createdCameraAtRuntime = true;
            }

            _attachedCamera = cameraToUse;
            _attachedCamera.gameObject.tag = "MainCamera";
            _attachedCamera.fieldOfView = Mathf.Clamp(cameraFieldOfView, 30f, 100f);
            _attachedCamera.nearClipPlane = Mathf.Max(0.01f, nearClipPlane);

            EnsureAudioListener(_attachedCamera);

            Transform cameraTransform = _attachedCamera.transform;
            cameraTransform.SetParent(cameraPivot, false);
            cameraTransform.localPosition = new Vector3(sideOffset, verticalOffset, -cameraDistance);
            cameraTransform.localRotation = Quaternion.identity;

            _safetyRig = _attachedCamera.GetComponent<PlayerCameraSafetyRig>();

            if (_safetyRig == null)
            {
                _safetyRig = _attachedCamera.gameObject.AddComponent<PlayerCameraSafetyRig>();
                _addedSafetyRigAtRuntime = true;
            }

            _safetyRig.Configure(
                cameraPivot,
                transform,
                _attachedCamera,
                new Vector3(sideOffset, verticalOffset, -cameraDistance),
                obstructionMask,
                collisionRadius,
                surfacePadding,
                minimumCameraDistance,
                blockedSmoothTime,
                restoreSmoothTime,
                nearClipPlane);

            if (verboseLogging)
            {
                Debug.Log("[PlayerCameraBootstrap] Attached local player camera.", this);
            }
        }

        private void DetachOrDestroyLocalCamera()
        {
            if (_safetyRig != null)
            {
                _safetyRig.Clear();

                if (_addedSafetyRigAtRuntime)
                {
                    Destroy(_safetyRig);
                }
            }

            _safetyRig = null;
            _addedSafetyRigAtRuntime = false;

            if (_attachedCamera == null)
            {
                return;
            }

            if (_createdCameraAtRuntime)
            {
                Destroy(_attachedCamera.gameObject);
            }
            else
            {
                _attachedCamera.transform.SetParent(null, true);
            }

            _attachedCamera = null;
            _createdCameraAtRuntime = false;
        }

        private static void EnsureAudioListener(UnityEngine.Camera cameraToUse)
        {
            if (cameraToUse == null)
            {
                return;
            }

            if (cameraToUse.GetComponent<AudioListener>() != null)
            {
                return;
            }

            if (FindFirstObjectByType<AudioListener>() != null)
            {
                return;
            }

            cameraToUse.gameObject.AddComponent<AudioListener>();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            cameraDistance = Mathf.Max(0.25f, cameraDistance);
            cameraFieldOfView = Mathf.Clamp(cameraFieldOfView, 30f, 100f);
            collisionRadius = Mathf.Max(0.01f, collisionRadius);
            surfacePadding = Mathf.Max(0f, surfacePadding);
            minimumCameraDistance = Mathf.Max(0.05f, minimumCameraDistance);
            blockedSmoothTime = Mathf.Max(0.001f, blockedSmoothTime);
            restoreSmoothTime = Mathf.Max(0.001f, restoreSmoothTime);
            nearClipPlane = Mathf.Max(0.01f, nearClipPlane);
        }
#endif
    }
}