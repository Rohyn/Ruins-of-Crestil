using System.Collections.Generic;
using ROC.Game.Common;
using UnityEngine;

namespace ROC.Networking.Interactions.Data
{
    /// <summary>
    /// One data-driven interaction branch.
    /// Branch rules decide whether this branch is eligible. Branch actions are run only when this branch is selected.
    /// </summary>
    [CreateAssetMenu(
        fileName = "InteractionBranch",
        menuName = "ROC/Interactions/Branches/Branch")]
    public sealed class InteractionBranchDefinition : ScriptableObject
    {
        [Header("Rules")]
        [Tooltip("Optional rules for this branch. If empty, the branch is always eligible. Use carefully, usually as a final fallback branch.")]
        [SerializeField] private InteractionRuleSetDefinition rules;

        [Header("Actions")]
        [Tooltip("Actions run in this order when this branch is selected.")]
        [SerializeField] private List<InteractionActionEntry> actions = new();

        [Header("Debug")]
        [SerializeField] private bool verboseLogging;

        public InteractionRuleSetDefinition Rules => rules;
        public IReadOnlyList<InteractionActionEntry> Actions => actions;

        public InteractionRuleResult EvaluateRulesServer(InteractionContext context)
        {
            InteractionRuleResult result = rules != null
                ? rules.EvaluateServer(context)
                : InteractionRuleResult.Pass();

            if (verboseLogging)
            {
                string state = result.Passed ? "PASS" : "FAIL";
                Debug.Log($"[InteractionBranchDefinition] Branch '{name}' rules => {state}: {result}", this);
            }

            return result;
        }

        public bool HasEnabledActions()
        {
            if (actions == null)
            {
                return false;
            }

            for (int i = 0; i < actions.Count; i++)
            {
                if (actions[i].Enabled)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
