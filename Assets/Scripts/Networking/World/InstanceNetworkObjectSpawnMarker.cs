using Unity.Netcode;
using UnityEngine;

namespace ROC.Networking.World
{
    [DisallowMultipleComponent]
    public sealed class InstanceNetworkObjectSpawnMarker : MonoBehaviour
    {
        [SerializeField] private NetworkObject networkPrefab;
        [SerializeField] private string stableObjectId;

        public NetworkObject NetworkPrefab => networkPrefab;
        public string StableObjectId => stableObjectId;
    }
}