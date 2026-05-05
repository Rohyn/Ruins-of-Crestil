using ROC.Game.Common;
using UnityEngine;

namespace ROC.Networking.Interactions.Data
{
    /// <summary>
    /// Base ScriptableObject for data-driven interaction rules.
    /// Server evaluation is authoritative. Client preview is intentionally conservative and should only be
    /// used for presentation/selection hints; it must never be trusted for gameplay.
    /// </summary>
    public abstract class InteractionRuleDefinition : ScriptableObject
    {
        [Header("Failure Metadata")]
        [Tooltip("Stable failure identifier for future prompts/notifications, such as intro.need_key.")]
        [SerializeField] private string failureReasonId;

        [Tooltip("Stable user-facing message identifier for future localization/prompt systems.")]
        [SerializeField] private string userMessageId;

        [Tooltip("Fallback debug message used until prompt/notification systems consume IDs.")]
        [TextArea]
        [SerializeField] private string failureDebugMessage;

        [Header("Client Preview")]
        [Tooltip("If false, client-side selection preview always passes this rule. Server validation remains authoritative.")]
        [SerializeField] private bool enableClientPreview;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging;

        public string FailureReasonId => failureReasonId;
        public string UserMessageId => userMessageId;
        public string FailureDebugMessage => failureDebugMessage;
        public bool EnableClientPreview => enableClientPreview;
        public bool VerboseLogging => verboseLogging;

        /// <summary>
        /// Local mirrored state that can affect this rule's client-preview result when EnableClientPreview is true.
        /// Used by NetworkInteractableTarget to cache preview results and invalidate only when relevant gameplay state changes.
        /// </summary>
        public virtual InteractionRuleDependencyFlags DependencyFlags => InteractionRuleDependencyFlags.Custom;

        public abstract InteractionRuleResult EvaluateServer(InteractionContext context);

        public virtual InteractionRuleResult EvaluateClientPreview(InteractionContext context)
        {
            return InteractionRuleResult.Pass();
        }

        protected InteractionRuleResult Pass()
        {
            return InteractionRuleResult.Pass();
        }

        protected InteractionRuleResult Fail(ServerActionErrorCode errorCode, string fallbackMessage)
        {
            string message = !string.IsNullOrWhiteSpace(failureDebugMessage)
                ? failureDebugMessage
                : fallbackMessage;

            return InteractionRuleResult.Fail(
                errorCode,
                message,
                failureReasonId,
                userMessageId);
        }

        protected static bool MatchesRequirement(InteractionRuleRequirementMode mode, bool actualHasValue)
        {
            return mode == InteractionRuleRequirementMode.MustHave ? actualHasValue : !actualHasValue;
        }

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
