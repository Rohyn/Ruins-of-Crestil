using ROC.Game.World;
using Unity.Netcode;
using UnityEngine;

namespace ROC.Networking.World
{
    [DisallowMultipleComponent]
    public sealed class ServerPlayerLocationTracker : NetworkBehaviour
    {
        private string _accountId;
        private string _characterId;

        public string AccountId => _accountId;
        public string CharacterId => _characterId;

        public void InitializeServer(string accountId, string characterId)
        {
            if (!NetworkManager.Singleton.IsServer)
            {
                Debug.LogError("[ServerPlayerLocationTracker] InitializeServer called outside server context.");
                return;
            }

            _accountId = accountId;
            _characterId = characterId;
        }

        public WorldLocation CaptureLocation(WorldLocation currentLocation)
        {
            return WorldLocation.AtTransform(
                currentLocation.SceneId,
                currentLocation.InstanceId,
                transform.position,
                transform.rotation);
        }
    }
}