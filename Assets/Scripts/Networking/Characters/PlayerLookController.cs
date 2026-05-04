using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ROC.Networking.Characters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PlayerLookController : NetworkBehaviour
    {
        public enum CursorModeState
        {
            GameplayLocked = 0,
            TemporaryFreeCursor = 1,
            MenuCursor = 2,
            ConversationCursor = 3
        }

        public static PlayerLookController Local { get; private set; }
        public static event Action<CursorModeState> LocalCursorModeChanged;

        [Header("Required References")]
        [SerializeField] private Transform cameraPivot;

        [Header("Look Tuning")]
        [SerializeField, Min(0.001f)] private float baseYawSensitivity = 0.12f;
        [SerializeField, Min(0.001f)] private float basePitchSensitivity = 0.10f;
        [SerializeField, Min(0.1f)] private float mouseSensitivityMultiplier = 1f;
        [SerializeField] private bool invertY;

        [Header("Pitch Clamp")]
        [SerializeField] private float minPitch = -35f;
        [SerializeField] private float maxPitch = 60f;

        [Header("Keys")]
        [SerializeField] private Key toggleMenuKey = Key.Tab;
        [SerializeField] private Key closeMenuKey = Key.Escape;
        [SerializeField] private Key toggleTemporaryFreeCursorKey = Key.LeftAlt;

        [Header("Startup")]
        [SerializeField] private bool startLockedInGameplay = true;

        public event Action<CursorModeState> CursorModeChanged;

        public CursorModeState CurrentCursorMode { get; private set; } = CursorModeState.GameplayLocked;
        public float YawDegrees { get; private set; }
        public float PitchDegrees => _pitchDegrees;

        private float _pitchDegrees;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                enabled = false;
                return;
            }

            Local = this;

            if (cameraPivot == null)
            {
                Debug.LogError("[PlayerLookController] No CameraPivot assigned.", this);
                enabled = false;
                return;
            }

            YawDegrees = NormalizeYaw(transform.eulerAngles.y);
            _pitchDegrees = 0f;

            ApplyCameraPivotRotation();

            SetCursorMode(startLockedInGameplay
                ? CursorModeState.GameplayLocked
                : CursorModeState.TemporaryFreeCursor);
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner)
            {
                return;
            }

            if (Local == this)
            {
                Local = null;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            LocalCursorModeChanged?.Invoke(CursorModeState.TemporaryFreeCursor);
        }

        private void Update()
        {
            if (!IsOwner)
            {
                return;
            }

            HandleModeToggleInput();

            if (CurrentCursorMode != CursorModeState.GameplayLocked)
            {
                return;
            }

            Mouse mouse = Mouse.current;

            if (mouse == null)
            {
                return;
            }

            ApplyMouseLook(mouse);
        }

        private void LateUpdate()
        {
            if (!IsOwner || cameraPivot == null)
            {
                return;
            }

            // NetworkTransform may update the root after Update in dedicated-client mode.
            // Re-apply local camera intent late so horizontal camera look remains responsive.
            ApplyCameraPivotRotation();
        }

        public void SetCursorMode(CursorModeState newMode)
        {
            if (CurrentCursorMode == newMode)
            {
                ApplyCursorState(newMode);
                return;
            }

            CurrentCursorMode = newMode;
            ApplyCursorState(newMode);

            CursorModeChanged?.Invoke(CurrentCursorMode);

            if (IsOwner)
            {
                LocalCursorModeChanged?.Invoke(CurrentCursorMode);
            }
        }

        public bool IsGameplayLookActive()
        {
            return CurrentCursorMode == CursorModeState.GameplayLocked;
        }

        private void HandleModeToggleInput()
        {
            if (CurrentCursorMode == CursorModeState.ConversationCursor)
            {
                return;
            }

            if (WasPressedThisFrame(toggleMenuKey))
            {
                SetCursorMode(CurrentCursorMode == CursorModeState.MenuCursor
                    ? CursorModeState.GameplayLocked
                    : CursorModeState.MenuCursor);

                return;
            }

            if (CurrentCursorMode == CursorModeState.MenuCursor &&
                WasPressedThisFrame(closeMenuKey))
            {
                SetCursorMode(CursorModeState.GameplayLocked);
                return;
            }

            if (WasPressedThisFrame(toggleTemporaryFreeCursorKey))
            {
                if (CurrentCursorMode == CursorModeState.MenuCursor)
                {
                    return;
                }

                SetCursorMode(CurrentCursorMode == CursorModeState.TemporaryFreeCursor
                    ? CursorModeState.GameplayLocked
                    : CursorModeState.TemporaryFreeCursor);
            }
        }

        private void ApplyMouseLook(Mouse mouse)
        {
            Vector2 mouseDelta = mouse.delta.ReadValue();

            float yawDelta = mouseDelta.x * baseYawSensitivity * mouseSensitivityMultiplier;
            YawDegrees = NormalizeYaw(YawDegrees + yawDelta);

            float pitchInput = mouseDelta.y * basePitchSensitivity * mouseSensitivityMultiplier;

            if (!invertY)
            {
                pitchInput = -pitchInput;
            }

            _pitchDegrees = Mathf.Clamp(_pitchDegrees + pitchInput, minPitch, maxPitch);

            ApplyCameraPivotRotation();
        }

        private void ApplyCameraPivotRotation()
        {
            if (cameraPivot == null)
            {
                return;
            }

            Quaternion yaw = Quaternion.Euler(0f, YawDegrees, 0f);
            Quaternion pitch = Quaternion.Euler(_pitchDegrees, 0f, 0f);

            // World rotation, not local rotation. This avoids depending on owner-side root yaw.
            cameraPivot.rotation = yaw * pitch;
        }

        private void ApplyCursorState(CursorModeState mode)
        {
            switch (mode)
            {
                case CursorModeState.GameplayLocked:
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                    break;

                case CursorModeState.TemporaryFreeCursor:
                case CursorModeState.MenuCursor:
                case CursorModeState.ConversationCursor:
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    break;
            }
        }

        private static bool WasPressedThisFrame(Key key)
        {
            if (key == Key.None)
            {
                return false;
            }

            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
            {
                return false;
            }

            var control = keyboard[key];
            return control != null && control.wasPressedThisFrame;
        }

        private static float NormalizeYaw(float yawDegrees)
        {
            yawDegrees %= 360f;

            if (yawDegrees < 0f)
            {
                yawDegrees += 360f;
            }

            return yawDegrees;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            baseYawSensitivity = Mathf.Max(0.001f, baseYawSensitivity);
            basePitchSensitivity = Mathf.Max(0.001f, basePitchSensitivity);
            mouseSensitivityMultiplier = Mathf.Max(0.1f, mouseSensitivityMultiplier);

            if (maxPitch < minPitch)
            {
                (minPitch, maxPitch) = (maxPitch, minPitch);
            }
        }
#endif
    }
}