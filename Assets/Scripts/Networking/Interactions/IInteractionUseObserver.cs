using Unity.Netcode;

namespace ROC.Networking.Interactions
{
    /// <summary>
    /// Optional non-action component that reacts after an IServerInteractable has executed.
    /// Used by reusable gates such as InteractionUsageGate.
    /// </summary>
    public interface IInteractionUseObserver
    {
        void HandleInteractionSucceeded(ulong clientId, NetworkObject actor, IServerInteractable executedInteractable);
    }
}
