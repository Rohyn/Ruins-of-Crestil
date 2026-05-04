using ROC.Networking.Conditions;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ROC.Networking.Characters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkTransform))]
    public sealed class NetworkPlayerMotor : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerLookController lookController;
        [SerializeField] private NetworkPlayerConditionState conditionState;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float walkSpeed = 4.5f;
        [SerializeField, Min(1f)] private float runMultiplier = 1.55f;
        [SerializeField] private bool clampDiagonalMovement = true;

        [Header("Jump / Gravity")]
        [SerializeField, Min(0f)] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -22f;
        [SerializeField] private float groundedStickForce = -2f;
        [SerializeField, Min(0f)] private float coyoteTime = 0.08f;
        [SerializeField, Min(0f)] private float jumpBufferTime = 0.10f;

        [Header("Input")]
        [SerializeField] private Key jumpKey = Key.Space;
        [SerializeField] private Key runKey = Key.LeftShift;
        [SerializeField, Min(0.02f)] private float inputKeepAliveSeconds = 0.10f;
        [SerializeField, Min(0.05f)] private float serverInputTimeoutSeconds = 0.35f;
        [SerializeField, Min(1f)] private float maxInputSendRate = 30f;

        [Header("Facing")]
        [SerializeField] private bool serverAppliesGameplayYawToRoot = true;
        [SerializeField] private bool nonOwnersApplyReplicatedYawToRoot = true;
        [SerializeField, Min(0.01f)] private float yawReplicationThresholdDegrees = 0.1f;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging;

        private readonly NetworkVariable<float> _gameplayYaw = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private CharacterController _characterController;

        private Vector2 _localMoveInput;
        private bool _localRunHeld;
        private bool _localJumpQueued;
        private uint _localInputSequence;
        private Vector2 _lastSentMoveInput;
        private bool _lastSentRunHeld;
        private float _lastSentYaw;
        private float _nextInputSendTime;
        private float _nextKeepAliveTime;

        private Vector2 _serverMoveInput;
        private bool _serverRunHeld;
        private float _serverYawDegrees;
        private float _lastServerInputTime;
        private uint _lastProcessedInputSequence;

        private float _verticalVelocity;
        private float _lastGroundedTime = -999f;
        private float _jumpBufferExpiresAt = -999f;

        public float GameplayYawDegrees => IsServer ? _serverYawDegrees : _gameplayYaw.Value;
        public bool IsGrounded => _characterController != null && _characterController.isGrounded;
        public uint LastProcessedInputSequence => _lastProcessedInputSequence;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();

            if (lookController == null)
            {
                lookController = GetComponent<PlayerLookController>();
            }

            if (conditionState == null)
            {
                conditionState = GetComponent<NetworkPlayerConditionState>();
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _serverYawDegrees = NormalizeYaw(transform.eulerAngles.y);
                _gameplayYaw.Value = _serverYawDegrees;
                _lastServerInputTime = Time.time;
            }

            if (IsOwner)
            {
                _lastSentYaw = GetOwnerRequestedYaw();
            }
        }

        private void Update()
        {
            if (!IsOwner)
            {
                return;
            }

            CollectOwnerInput();
        }

        private void FixedUpdate()
        {
            if (IsOwner)
            {
                PushOwnerInputToServer();
            }

            if (IsServer)
            {
                SimulateServerMovement(Time.fixedDeltaTime);
            }
        }

        private void LateUpdate()
        {
            if (!IsSpawned || IsServer || IsOwner)
            {
                return;
            }

            if (nonOwnersApplyReplicatedYawToRoot)
            {
                ApplyYawToRoot(_gameplayYaw.Value);
            }
        }

        public void ClearServerMotionState()
        {
            if (!IsServer)
            {
                return;
            }

            _serverMoveInput = Vector2.zero;
            _serverRunHeld = false;
            _verticalVelocity = 0f;
            _jumpBufferExpiresAt = -999f;
            _lastGroundedTime = -999f;
        }

        private void CollectOwnerInput()
        {
            bool gameplayInputAllowed =
                lookController == null ||
                lookController.IsGameplayLookActive();

            if (!gameplayInputAllowed)
            {
                _localMoveInput = Vector2.zero;
                _localRunHeld = false;
                return;
            }

            bool blocksMovement = conditionState != null && conditionState.BlocksMovement;
            bool blocksSprint = conditionState != null && conditionState.BlocksSprint;
            bool blocksJump = conditionState != null && conditionState.BlocksJump;

            _localMoveInput = blocksMovement ? Vector2.zero : ReadMoveInput();
            _localRunHeld = !blocksSprint && IsPressed(runKey);

            if (!blocksJump && WasPressedThisFrame(jumpKey))
            {
                _localJumpQueued = true;
            }
        }

        private void PushOwnerInputToServer()
        {
            Vector2 moveInput = clampDiagonalMovement
                ? Vector2.ClampMagnitude(_localMoveInput, 1f)
                : ClampAxes(_localMoveInput, -1f, 1f);

            bool runHeld = _localRunHeld;
            bool jumpPressed = _localJumpQueued;
            float yaw = GetOwnerRequestedYaw();

            bool inputChanged = (moveInput - _lastSentMoveInput).sqrMagnitude > 0.0001f;
            bool runChanged = runHeld != _lastSentRunHeld;
            bool yawChanged = Mathf.Abs(Mathf.DeltaAngle(_lastSentYaw, yaw)) >= yawReplicationThresholdDegrees;
            bool sendRateReady = Time.unscaledTime >= _nextInputSendTime;
            bool keepAliveReady = Time.unscaledTime >= _nextKeepAliveTime;

            if (!jumpPressed && !inputChanged && !runChanged && !yawChanged && !keepAliveReady)
            {
                return;
            }

            if (!jumpPressed && !sendRateReady && !keepAliveReady)
            {
                return;
            }

            _localInputSequence++;

            _lastSentMoveInput = moveInput;
            _lastSentRunHeld = runHeld;
            _lastSentYaw = yaw;
            _nextInputSendTime = Time.unscaledTime + (1f / maxInputSendRate);
            _nextKeepAliveTime = Time.unscaledTime + inputKeepAliveSeconds;

            if (IsServer)
            {
                ApplySubmittedInputOnServer(moveInput, runHeld, jumpPressed, yaw, _localInputSequence);
            }
            else
            {
                SubmitMovementInputServerRpc(moveInput, runHeld, jumpPressed, yaw, _localInputSequence);
            }

            _localJumpQueued = false;
        }

        [ServerRpc]
        private void SubmitMovementInputServerRpc(
            Vector2 moveInput,
            bool runHeld,
            bool jumpPressed,
            float requestedYawDegrees,
            uint inputSequence,
            ServerRpcParams serverRpcParams = default)
        {
            if (serverRpcParams.Receive.SenderClientId != OwnerClientId)
            {
                return;
            }

            ApplySubmittedInputOnServer(
                moveInput,
                runHeld,
                jumpPressed,
                requestedYawDegrees,
                inputSequence);
        }

        private void ApplySubmittedInputOnServer(
            Vector2 moveInput,
            bool runHeld,
            bool jumpPressed,
            float requestedYawDegrees,
            uint inputSequence)
        {
            if (!IsServer)
            {
                return;
            }

            if (!IsFinite(moveInput) || !IsFinite(requestedYawDegrees))
            {
                if (verboseLogging)
                {
                    Debug.LogWarning("[NetworkPlayerMotor] Rejected non-finite movement input.", this);
                }

                return;
            }

            _serverMoveInput = clampDiagonalMovement
                ? Vector2.ClampMagnitude(moveInput, 1f)
                : ClampAxes(moveInput, -1f, 1f);

            _serverRunHeld = runHeld;
            _serverYawDegrees = NormalizeYaw(requestedYawDegrees);
            _lastServerInputTime = Time.time;
            _lastProcessedInputSequence = inputSequence;

            if (jumpPressed)
            {
                _jumpBufferExpiresAt = Time.time + jumpBufferTime;
            }

            ReplicateGameplayYawIfNeeded();
        }

        private void SimulateServerMovement(float deltaTime)
        {
            if (Time.time - _lastServerInputTime > serverInputTimeoutSeconds)
            {
                _serverMoveInput = Vector2.zero;
                _serverRunHeld = false;
            }

            PlayerControlBlockFlags blocks = conditionState != null
                ? conditionState.ControlBlocks.Value
                : PlayerControlBlockFlags.None;

            if ((blocks & PlayerControlBlockFlags.Movement) != 0)
            {
                _serverMoveInput = Vector2.zero;
            }

            if ((blocks & PlayerControlBlockFlags.Sprint) != 0)
            {
                _serverRunHeld = false;
            }

            bool blocksJump = (blocks & PlayerControlBlockFlags.Jump) != 0;
            bool blocksGravity = (blocks & PlayerControlBlockFlags.Gravity) != 0;

            bool grounded = _characterController.isGrounded;

            if (grounded)
            {
                _lastGroundedTime = Time.time;

                if (_verticalVelocity < 0f)
                {
                    _verticalVelocity = groundedStickForce;
                }
            }

            bool jumpBuffered = Time.time <= _jumpBufferExpiresAt;
            bool canUseCoyoteJump = Time.time <= _lastGroundedTime + coyoteTime;

            if (!blocksJump && jumpBuffered && canUseCoyoteJump && jumpHeight > 0f)
            {
                _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                _jumpBufferExpiresAt = -999f;
                _lastGroundedTime = -999f;
            }

            if (blocksGravity)
            {
                _verticalVelocity = 0f;
            }
            else
            {
                _verticalVelocity += gravity * deltaTime;
            }

            Vector3 horizontalDirection = CalculateMoveWorldFromInputAndYaw(
                _serverMoveInput,
                _serverYawDegrees);

            float speed = _serverRunHeld
                ? walkSpeed * runMultiplier
                : walkSpeed;

            Vector3 velocity =
                horizontalDirection * speed +
                Vector3.up * _verticalVelocity;

            CollisionFlags collisionFlags = _characterController.Move(velocity * deltaTime);

            if ((collisionFlags & CollisionFlags.Above) != 0 && _verticalVelocity > 0f)
            {
                _verticalVelocity = 0f;
            }

            if (_characterController.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = groundedStickForce;
            }

            if (serverAppliesGameplayYawToRoot)
            {
                ApplyYawToRoot(_serverYawDegrees);
            }

            ReplicateGameplayYawIfNeeded();
        }

        private Vector3 CalculateMoveWorldFromInputAndYaw(Vector2 moveInput, float yawDegrees)
        {
            Vector2 input = clampDiagonalMovement
                ? Vector2.ClampMagnitude(moveInput, 1f)
                : ClampAxes(moveInput, -1f, 1f);

            Quaternion yawRotation = Quaternion.Euler(0f, yawDegrees, 0f);

            Vector3 forward = yawRotation * Vector3.forward;
            Vector3 right = yawRotation * Vector3.right;

            Vector3 direction = forward * input.y + right * input.x;
            direction.y = 0f;

            if (clampDiagonalMovement)
            {
                direction = Vector3.ClampMagnitude(direction, 1f);
            }

            return direction;
        }

        private Vector2 ReadMoveInput()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
            {
                return Vector2.zero;
            }

            Vector2 input = Vector2.zero;

            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            {
                input.y += 1f;
            }

            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            {
                input.y -= 1f;
            }

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                input.x += 1f;
            }

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                input.x -= 1f;
            }

            return clampDiagonalMovement
                ? Vector2.ClampMagnitude(input, 1f)
                : ClampAxes(input, -1f, 1f);
        }

        private float GetOwnerRequestedYaw()
        {
            return NormalizeYaw(
                lookController != null
                    ? lookController.YawDegrees
                    : transform.eulerAngles.y);
        }

        private void ReplicateGameplayYawIfNeeded()
        {
            if (!IsServer)
            {
                return;
            }

            if (Mathf.Abs(Mathf.DeltaAngle(_gameplayYaw.Value, _serverYawDegrees)) < yawReplicationThresholdDegrees)
            {
                return;
            }

            _gameplayYaw.Value = _serverYawDegrees;
        }

        private void ApplyYawToRoot(float yawDegrees)
        {
            transform.rotation = Quaternion.Euler(0f, NormalizeYaw(yawDegrees), 0f);
        }

        private static bool IsPressed(Key key)
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
            return control != null && control.isPressed;
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

        private static Vector2 ClampAxes(Vector2 value, float min, float max)
        {
            value.x = Mathf.Clamp(value.x, min, max);
            value.y = Mathf.Clamp(value.y, min, max);
            return value;
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float NormalizeYaw(float yawDegrees)
        {
            if (!IsFinite(yawDegrees))
            {
                return 0f;
            }

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
            walkSpeed = Mathf.Max(0f, walkSpeed);
            runMultiplier = Mathf.Max(1f, runMultiplier);
            jumpHeight = Mathf.Max(0f, jumpHeight);

            if (gravity > 0f)
            {
                gravity = -gravity;
            }

            inputKeepAliveSeconds = Mathf.Max(0.02f, inputKeepAliveSeconds);
            serverInputTimeoutSeconds = Mathf.Max(0.05f, serverInputTimeoutSeconds);
            maxInputSendRate = Mathf.Max(1f, maxInputSendRate);
            yawReplicationThresholdDegrees = Mathf.Max(0.01f, yawReplicationThresholdDegrees);
        }
#endif
    }
}