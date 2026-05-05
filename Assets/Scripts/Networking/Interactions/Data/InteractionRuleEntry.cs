using System;
using UnityEngine;

namespace ROC.Networking.Interactions.Data
{
    /// <summary>
    /// One rule slot in an InteractionRuleSetDefinition.
    /// </summary>
    [Serializable]
    public struct InteractionRuleEntry
    {
        [SerializeField] private bool enabled;
        [SerializeField] private InteractionRuleDefinition rule;

        public bool Enabled => enabled;
        public InteractionRuleDefinition Rule => rule;
    }
}
