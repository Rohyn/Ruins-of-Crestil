using Unity.Netcode;

namespace ROC.Networking.Interactions
{
    /// <summary>
    /// Optional non-action component that can make a target unavailable for selection or server interaction.
    /// Components implementing this should not also implement IServerInteractable unless they are the actual action.
    /// </summary>
    public interface IInteractionAvailabilityRule
    {
        bool CanSelect(ulong clientId, out string reason);

        bool CanInteract(ulong clientId, NetworkObject actor, out string reason);
    }
}
