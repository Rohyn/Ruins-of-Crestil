using ROC.Networking.Characters;
using ROC.Networking.Sessions;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ROC.Networking.Interactions
{
    [DisallowMultipleComponent]
    public sealed class PlayerInteractor : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInteractionSelector interactionSelector;
        [SerializeField] private PlayerLookController lookController;

        [Header("Input")]
        [SerializeField] private Key interactKey = Key.E;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging;

        private void Awake()
        {
            if (interactionSelector == null)
            {
                interactionSelector = GetComponent<PlayerInteractionSelector>();
            }

            if (lookController == null)
            {
                lookController = GetComponent<PlayerLookController>();
            }
        }

        private void Update()
        {
            if (!IsOwner)
            {
                return;
            }

            if (lookController != null && !lookController.IsGameplayLookActive())
            {
                return;
            }

            if (!WasPressedThisFrame(interactKey))
            {
                return;
            }

            RequestInteractCurrentTarget();
        }

        private void RequestInteractCurrentTarget()
        {
            if (interactionSelector == null)
            {
                Debug.LogWarning("[PlayerInteractor] No PlayerInteractionSelector assigned.", this);
                return;
            }

            NetworkInteractableTarget target = interactionSelector.CurrentTarget;

            if (target == null)
            {
                if (verboseLogging)
                {
                    Debug.Log("[PlayerInteractor] No current interactable target.", this);
                }

                return;
            }

            if (!target.TryGetTargetNetworkObject(out NetworkObject targetNetworkObject))
            {
                Debug.LogWarning($"[PlayerInteractor] Target '{target.name}' has no spawned NetworkObject.", target);
                return;
            }

            NetworkInteractionRequestor requestor =
                ClientSessionProxy.Local != null
                    ? ClientSessionProxy.Local.GetComponent<NetworkInteractionRequestor>()
                    : null;

            if (requestor == null)
            {
                Debug.LogWarning("[PlayerInteractor] No NetworkInteractionRequestor found on local session proxy.", this);
                return;
            }

            requestor.RequestInteract(targetNetworkObject.NetworkObjectId);

            if (verboseLogging)
            {
                Debug.Log($"[PlayerInteractor] Requested interaction with '{target.name}'.", target);
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
    }
}