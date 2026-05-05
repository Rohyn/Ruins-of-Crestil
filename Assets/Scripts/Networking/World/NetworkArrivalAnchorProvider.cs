using System;
using System.Collections.Generic;
using UnityEngine;

namespace ROC.Networking.World
{
    /// <summary>
    /// Provides named arrival anchors from a spawned network prefab.
    ///
    /// Use this on runtime-spawned instance objects when a scene-level NetworkArrivalProfile
    /// needs to resolve anchor transforms that do not exist until the prefab is spawned.
    ///
    /// Example on an infirmary bed prefab:
    /// - rest -> RestAnchor
    /// - exit -> ExitAnchor
    ///
    /// The scene spawn marker supplies the StableObjectId, and NetworkArrivalProfile uses that
    /// stable object ID plus these anchor IDs to resolve the final transforms at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkArrivalAnchorProvider : MonoBehaviour
    {
        [SerializeField] private AnchorBinding[] anchors = Array.Empty<AnchorBinding>();

        public IReadOnlyList<AnchorBinding> Anchors => anchors;

        public bool TryGetAnchor(string anchorId, out Transform anchor)
        {
            anchor = null;

            if (string.IsNullOrWhiteSpace(anchorId) || anchors == null)
            {
                return false;
            }

            for (int i = 0; i < anchors.Length; i++)
            {
                AnchorBinding binding = anchors[i];

                if (binding.Anchor == null)
                {
                    continue;
                }

                if (string.Equals(binding.AnchorId, anchorId, StringComparison.OrdinalIgnoreCase))
                {
                    anchor = binding.Anchor;
                    return true;
                }
            }

            return false;
        }

        [Serializable]
        public struct AnchorBinding
        {
            [SerializeField] private string anchorId;
            [SerializeField] private Transform anchor;

            public string AnchorId => anchorId;
            public Transform Anchor => anchor;
        }
    }
}
