using ROC.Networking.World;
using Unity.Netcode;
using UnityEngine;

namespace ROC.Networking.Interactions
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkInstanceObject))]
    public sealed class SharedDoorInteractable : NetworkBehaviour, IServerInteractable
    {
        [SerializeField, Min(0.1f)] private float maxInteractDistance = 4f;
        [SerializeField] private Transform doorPivot;
        [SerializeField] private Vector3 closedEuler;
        [SerializeField] private Vector3 openEuler = new(0f, 90f, 0f);
        [SerializeField, Min(0f)] private float rotationLerpSpeed = 12f;

        public readonly NetworkVariable<bool> IsOpen = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public float MaxInteractDistance => maxInteractDistance;

        private void Awake()
        {
            if (doorPivot == null)
            {
                doorPivot = transform;
            }
        }

        public override void OnNetworkSpawn()
        {
            IsOpen.OnValueChanged += HandleOpenChanged;
            ApplyRotationImmediate();
        }

        public override void OnNetworkDespawn()
        {
            IsOpen.OnValueChanged -= HandleOpenChanged;
        }

        private void Update()
        {
            Quaternion target = Quaternion.Euler(IsOpen.Value ? openEuler : closedEuler);
            doorPivot.localRotation = Quaternion.Lerp(
                doorPivot.localRotation,
                target,
                Time.deltaTime * rotationLerpSpeed);
        }

        public bool CanInteract(ulong clientId, NetworkObject actor, out string reason)
        {
            reason = string.Empty;
            return true;
        }

        public void Interact(ulong clientId, NetworkObject actor)
        {
            if (!IsServer)
            {
                return;
            }

            IsOpen.Value = !IsOpen.Value;
        }

        private void HandleOpenChanged(bool previousValue, bool newValue)
        {
            ApplyRotationImmediate();
        }

        private void ApplyRotationImmediate()
        {
            if (doorPivot == null)
            {
                return;
            }

            doorPivot.localRotation = Quaternion.Euler(IsOpen.Value ? openEuler : closedEuler);
        }
    }
}