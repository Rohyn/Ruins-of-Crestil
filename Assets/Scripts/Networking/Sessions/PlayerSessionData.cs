using ROC.Game.World;
using Unity.Netcode;

namespace ROC.Networking.Sessions
{
    public sealed class PlayerSessionData
    {
        public ulong ClientId { get; internal set; }

        public string AccountId { get; internal set; } = string.Empty;
        public string CharacterId { get; internal set; } = string.Empty;
        public string DisplayName { get; internal set; } = string.Empty;

        public string SceneId { get; internal set; } = string.Empty;
        public string InstanceId { get; internal set; } = string.Empty;

        public PlayerSessionState State { get; internal set; } = PlayerSessionState.Disconnected;

        public NetworkObject SessionProxyObject { get; internal set; }
        public NetworkObject AvatarObject { get; internal set; }

        public WorldLocation CurrentLocation { get; internal set; }

        public bool HasAccount => !string.IsNullOrWhiteSpace(AccountId);
        public bool HasSelectedCharacter => !string.IsNullOrWhiteSpace(CharacterId);

        public bool IsInWorld =>
            State == PlayerSessionState.InWorld &&
            AvatarObject != null &&
            AvatarObject.IsSpawned;

        internal PlayerSessionData(ulong clientId)
        {
            ClientId = clientId;
            State = PlayerSessionState.Connected;
        }
    }
}