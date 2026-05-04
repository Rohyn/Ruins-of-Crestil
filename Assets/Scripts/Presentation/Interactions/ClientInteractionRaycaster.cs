using ROC.Networking.Interactions;
using ROC.Networking.Sessions;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ROC.Presentation.Interactions
{
    [DisallowMultipleComponent]
    public sealed class ClientInteractionRaycaster : MonoBehaviour
    {
        [SerializeField] private UnityEngine.Camera interactionCamera;
        [SerializeField, Min(0.1f)] private float maxDistance = 4f;
        [SerializeField] private LayerMask interactionMask = ~0;

        private void Awake()
        {
            if (interactionCamera == null)
            {
                interactionCamera = UnityEngine.Camera.main;
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null || !keyboard.eKey.wasPressedThisFrame)
            {
                return;
            }

            if (interactionCamera == null)
            {
                return;
            }

            Ray ray = new Ray(
                interactionCamera.transform.position,
                interactionCamera.transform.forward);

            if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, interactionMask, QueryTriggerInteraction.Collide))
            {
                return;
            }

            NetworkObject target = hit.collider.GetComponentInParent<NetworkObject>();

            if (target == null)
            {
                return;
            }

            NetworkInteractionRequestor requestor =
                ClientSessionProxy.Local != null
                    ? ClientSessionProxy.Local.GetComponent<NetworkInteractionRequestor>()
                    : null;

            if (requestor == null)
            {
                return;
            }

            requestor.RequestInteract(target.NetworkObjectId);
        }
    }
}