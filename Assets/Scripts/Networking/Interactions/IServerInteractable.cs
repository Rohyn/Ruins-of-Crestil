using Unity.Netcode;

namespace ROC.Networking.Interactions
{
    public interface IServerInteractable
    {
        float MaxInteractDistance { get; }

        bool CanInteract(ulong clientId, NetworkObject actor, out string reason);

        void Interact(ulong clientId, NetworkObject actor);
    }
}