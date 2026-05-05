using System;
using UnityEngine;

namespace ROC.Networking.Interactions.Data
{
    /// <summary>
    /// One ordered action slot on an InteractionExecutor.
    /// The action definition is data-driven; the slot controls whether failure blocks the whole interaction.
    /// </summary>
    [Serializable]
    public struct InteractionActionEntry
    {
        [SerializeField] private bool enabled;
        [SerializeField] private InteractionActionDefinition action;
        [SerializeField] private InteractionActionFailureBehavior failureBehavior;

        public bool Enabled => enabled;
        public InteractionActionDefinition Action => action;
        public InteractionActionFailureBehavior FailureBehavior => failureBehavior;
        public bool IsRequired => failureBehavior == InteractionActionFailureBehavior.Required;
    }
}
