using ROC.Networking.Sessions;
using ROC.Networking.World;
using Unity.Netcode;
using UnityEngine;

namespace ROC.Networking.Interactions.Data
{
    /// <summary>
    /// Runtime context for one interaction evaluation/execution.
    /// Server-created contexts are authoritative. Client-preview contexts are only for local prompt/selection hints.
    /// </summary>
    public sealed class InteractionContext
    {
        public InteractionContext(
            ulong clientId,
            NetworkObject actor,
            NetworkObject targetObject,
            NetworkInteractableTarget target,
            InteractionExecutor executor,
            PlayerSessionData session,
            NetworkInstanceObject targetInstanceObject)
            : this(
                clientId,
                actor,
                targetObject,
                target,
                executor,
                session,
                targetInstanceObject,
                isClientPreview: false)
        {
        }

        private InteractionContext(
            ulong clientId,
            NetworkObject actor,
            NetworkObject targetObject,
            NetworkInteractableTarget target,
            InteractionExecutor executor,
            PlayerSessionData session,
            NetworkInstanceObject targetInstanceObject,
            bool isClientPreview)
        {
            ClientId = clientId;
            Actor = actor;
            TargetObject = targetObject;
            Target = target;
            Executor = executor;
            Session = session;
            TargetInstanceObject = targetInstanceObject;
            IsClientPreview = isClientPreview;
        }

        public ulong ClientId { get; }
        public NetworkObject Actor { get; }
        public NetworkObject TargetObject { get; }
        public NetworkInteractableTarget Target { get; }
        public InteractionExecutor Executor { get; }
        public PlayerSessionData Session { get; }
        public NetworkInstanceObject TargetInstanceObject { get; }
        public bool IsClientPreview { get; }

        public Transform ActorTransform => Actor != null ? Actor.transform : null;
        public Transform TargetTransform => TargetObject != null ? TargetObject.transform : null;

        public string TargetStableObjectId =>
            TargetInstanceObject != null ? TargetInstanceObject.StableObjectId.Value.ToString() : string.Empty;

        public string TargetInstanceId =>
            TargetInstanceObject != null ? TargetInstanceObject.InstanceIdString : string.Empty;

        public string SceneId => Session != null ? Session.SceneId ?? string.Empty : string.Empty;
        public string InstanceId => Session != null ? Session.InstanceId ?? string.Empty : TargetInstanceId;
        public string CharacterId => Session != null ? Session.CharacterId ?? string.Empty : string.Empty;

        public string BuildDefaultSourceId(string suffix)
        {
            string stableId = TargetStableObjectId;
            if (string.IsNullOrWhiteSpace(stableId))
            {
                stableId = TargetObject != null ? TargetObject.name : "interaction.target";
            }

            if (string.IsNullOrWhiteSpace(suffix))
            {
                return stableId;
            }

            return stableId + "." + suffix;
        }

        public static InteractionContext CreateClientPreview(
            ulong clientId,
            NetworkObject targetObject,
            NetworkInteractableTarget target,
            InteractionExecutor executor,
            NetworkInstanceObject targetInstanceObject)
        {
            NetworkObject actor = null;
            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.LocalClient != null &&
                NetworkManager.Singleton.LocalClient.PlayerObject != null)
            {
                actor = NetworkManager.Singleton.LocalClient.PlayerObject;
            }

            return new InteractionContext(
                clientId,
                actor,
                targetObject,
                target,
                executor,
                null,
                targetInstanceObject,
                isClientPreview: true);
        }
    }
}
