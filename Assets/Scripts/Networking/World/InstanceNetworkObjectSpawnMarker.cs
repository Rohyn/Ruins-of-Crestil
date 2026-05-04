using Unity.Netcode;
using UnityEngine;

namespace ROC.Networking.World
{
    [DisallowMultipleComponent]
    public sealed class InstanceNetworkObjectSpawnMarker : MonoBehaviour
    {
        [SerializeField] private NetworkObject networkPrefab;
        [SerializeField] private string stableObjectId;

        [Header("Transform")]
        [SerializeField] private bool applyMarkerScale = true;

        public NetworkObject NetworkPrefab => networkPrefab;
        public string StableObjectId => stableObjectId;
        public bool ApplyMarkerScale => applyMarkerScale;
    }
}