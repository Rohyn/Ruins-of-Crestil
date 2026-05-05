using ROC.Game.Common;
using UnityEngine;

namespace ROC.Networking.Interactions.Data
{
    /// <summary>
    /// Base ScriptableObject for data-driven interaction actions.
    /// Create concrete assets, then assign them to an InteractionExecutor in execution order.
    /// </summary>
    public abstract class InteractionActionDefinition : ScriptableObject
    {
        [Header("Debug")]
        [SerializeField] private bool verboseLogging;

        public bool VerboseLogging => verboseLogging;

        /// <summary>
        /// Validate that this action can execute. The executor validates all required actions before executing any action.
        /// </summary>
        public abstract ServerActionResult CanExecute(InteractionContext context);

        /// <summary>
        /// Executes the action on the server. Called only after validation passes for required actions.
        /// </summary>
        public abstract ServerActionResult Execute(InteractionContext context);

        protected void Log(string message, Object contextObject = null)
        {
            if (!verboseLogging)
            {
                return;
            }

            Debug.Log($"[{GetType().Name}] {message}", contextObject != null ? contextObject : this);
        }
    }
}
