using System;
using UnityEngine;

namespace ROC.Networking.Interactions.Data
{
    /// <summary>
    /// One ordered branch slot on an InteractionBranchSetDefinition.
    /// The first enabled branch whose rules pass is selected.
    /// </summary>
    [Serializable]
    public struct InteractionBranchEntry
    {
        [SerializeField] private bool enabled;
        [SerializeField] private InteractionBranchDefinition branch;

        public bool Enabled => enabled;
        public InteractionBranchDefinition Branch => branch;
    }
}
