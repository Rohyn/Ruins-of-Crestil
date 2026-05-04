using ROC.Game.World;
using ROC.Networking.World;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ROC.Networking.Characters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkPlayerAvatar : NetworkBehaviour
    {
        public static NetworkPlayerAvatar Local { get; private set; }

        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 4.5f;
        [SerializeField, Min(0.05f)] private float inputKeepAliveSeconds = 0.15f;
        [SerializeField, Min(0.05f)] private float serverInputTimeoutSeconds = 0.35f;

        [Header("Optional References")]
        [SerializeField] private PlayerLookController lookController;

        public readonly NetworkVariable<FixedString64Bytes> CharacterId = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<FixedString64Bytes> DisplayName = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<FixedString64Bytes> SceneId = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<FixedString128Bytes> InstanceId = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Vector2 _serverMoveInput;
        private float _lastServerInputTime;
        private float _serverYawDegrees;

        private Vector2 _lastSentInput;
        private float _nextInputKeepAliveTime;

        private void Awake()
        {
            if (lookController == null)
            {
                lookController = GetComponent<PlayerLookController>();
            }
        }

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
        }

        public void InitializeServer(
            string accountId,
            string characterId,
            string displayName,
            WorldLocation location)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                Debug.LogError("[NetworkPlayerAvatar] InitializeServer called outside server context.");
                return;
            }

            CharacterId.Value = new FixedString64Bytes(characterId);
            DisplayName.Value = new FixedString64Bytes(displayName);
            SceneId.Value = new FixedString64Bytes(location.SceneId);
            InstanceId.Value = new FixedString128Bytes(location.InstanceId);

            _serverYawDegrees = transform.eulerAngles.y;

            if (TryGetComponent(out ServerPlayerLocationTracker tracker))
            {
                tracker.InitializeServer(accountId, characterId);
            }
        }

        public void SetServerYawDegrees(float yawDegrees)
        {
            if (!IsServer)
            {
                return;
            }

            _serverYawDegrees = NormalizeYaw(yawDegrees);
            transform.rotation = Quaternion.Euler(0f, _serverYawDegrees, 0f);
        }

        private void FixedUpdate()
        {
            if (IsOwner)
            {
                SendOwnerInput();
            }

            if (IsServer)
            {
                MoveOnServer();
            }
        }

        private void SendOwnerInput()
        {
            Vector2 input = ReadMovementInput();

            bool movementAllowed = lookController == null || lookController.IsGameplayLookActive();

            if (!movementAllowed)
            {
                input = Vector2.zero;
            }

            bool changed = (input - _lastSentInput).sqrMagnitude > 0.0001f;
            bool keepAliveDue = Time.unscaledTime >= _nextInputKeepAliveTime;

            if (!changed && !keepAliveDue)
            {
                return;
            }

            _lastSentInput = input;
            _nextInputKeepAliveTime = Time.unscaledTime + inputKeepAliveSeconds;

            SubmitMoveInputServerRpc(input);
        }

        private static Vector2 ReadMovementInput()
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

            if (input.sqrMagnitude > 1f)
            {
                input.Normalize();
            }

            return input;
        }

        [ServerRpc]
        private void SubmitMoveInputServerRpc(Vector2 input, ServerRpcParams serverRpcParams = default)
        {
            if (serverRpcParams.Receive.SenderClientId != OwnerClientId)
            {
                return;
            }

            if (input.sqrMagnitude > 1f)
            {
                input.Normalize();
            }

            _serverMoveInput = input;
            _lastServerInputTime = Time.time;
        }

        private void MoveOnServer()
        {
            if (Time.time - _lastServerInputTime > serverInputTimeoutSeconds)
            {
                _serverMoveInput = Vector2.zero;
            }

            if (_serverMoveInput.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector3 forward = transform.forward;
            Vector3 right = transform.right;

            forward.y = 0f;
            right.y = 0f;

            forward.Normalize();
            right.Normalize();

            Vector3 direction = forward * _serverMoveInput.y + right * _serverMoveInput.x;

            if (direction.sqrMagnitude > 1f)
            {
                direction.Normalize();
            }

            transform.position += direction * moveSpeed * Time.fixedDeltaTime;
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
    }
}