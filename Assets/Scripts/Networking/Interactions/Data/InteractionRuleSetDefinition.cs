using System.Collections.Generic;
using ROC.Game.Common;
using UnityEngine;

namespace ROC.Networking.Interactions.Data
{
    /// <summary>
    /// Data-driven set of interaction rules. Use one for object-level gates now and for branch-level gates later.
    /// </summary>
    [CreateAssetMenu(
        fileName = "InteractionRuleSet",
        menuName = "ROC/Rules/Rule Set")]
    public sealed class InteractionRuleSetDefinition : ScriptableObject
    {
        [Header("Evaluation")]
        [SerializeField] private InteractionRuleEvaluationMode evaluationMode = InteractionRuleEvaluationMode.All;
        [SerializeField] private List<InteractionRuleEntry> rules = new();

        [Header("Debug")]
        [SerializeField] private bool verboseLogging;

        public InteractionRuleEvaluationMode EvaluationMode => evaluationMode;

        public InteractionRuleResult EvaluateServer(InteractionContext context)
        {
            return Evaluate(context, clientPreview: false);
        }

        public InteractionRuleResult EvaluateClientPreview(InteractionContext context)
        {
            return Evaluate(context, clientPreview: true);
        }

        /// <summary>
        /// Aggregates only dependencies for rules that have client preview enabled. Server-only rules do not need
        /// local cache invalidation because they always pass during client preview.
        /// </summary>
        public InteractionRuleDependencyFlags GetClientPreviewDependencyFlags()
        {
            InteractionRuleDependencyFlags dependencies = InteractionRuleDependencyFlags.None;

            if (rules == null)
            {
                return dependencies;
            }

            for (int i = 0; i < rules.Count; i++)
            {
                InteractionRuleEntry entry = rules[i];
                if (!entry.Enabled || entry.Rule == null || !entry.Rule.EnableClientPreview)
                {
                    continue;
                }

                dependencies |= entry.Rule.DependencyFlags;
            }

            return dependencies;
        }

        private InteractionRuleResult Evaluate(InteractionContext context, bool clientPreview)
        {
            if (rules == null || rules.Count == 0)
            {
                return InteractionRuleResult.Pass();
            }

            bool sawEnabledRule = false;
            InteractionRuleResult firstFailure = InteractionRuleResult.Pass();

            for (int i = 0; i < rules.Count; i++)
            {
                InteractionRuleEntry entry = rules[i];
                if (!entry.Enabled)
                {
                    continue;
                }

                sawEnabledRule = true;
                InteractionRuleDefinition rule = entry.Rule;
                if (rule == null)
                {
                    InteractionRuleResult missingRule = InteractionRuleResult.Fail(
                        ServerActionErrorCode.InvalidState,
                        $"Interaction rule set '{name}' has an enabled empty rule slot at index {i}.");

                    if (evaluationMode == InteractionRuleEvaluationMode.All)
                    {
                        return missingRule;
                    }

                    if (firstFailure.Passed)
                    {
                        firstFailure = missingRule;
                    }

                    continue;
                }

                InteractionRuleResult result = clientPreview && rule.EnableClientPreview
                    ? rule.EvaluateClientPreview(context)
                    : clientPreview
                        ? InteractionRuleResult.Pass()
                        : rule.EvaluateServer(context);

                if (verboseLogging)
                {
                    string mode = clientPreview ? "ClientPreview" : "Server";
                    Debug.Log($"[InteractionRuleSetDefinition] {name} ({mode}): Rule '{rule.name}' => {result}", this);
                }

                if (evaluationMode == InteractionRuleEvaluationMode.All && !result.Passed)
                {
                    return result;
                }

                if (evaluationMode == InteractionRuleEvaluationMode.Any && result.Passed)
                {
                    return result;
                }

                if (!result.Passed && firstFailure.Passed)
                {
                    firstFailure = result;
                }
            }

            if (!sawEnabledRule)
            {
                return InteractionRuleResult.Pass();
            }

            if (evaluationMode == InteractionRuleEvaluationMode.All)
            {
                return InteractionRuleResult.Pass();
            }

            return !firstFailure.Passed
                ? firstFailure
                : InteractionRuleResult.Fail(
                    ServerActionErrorCode.InvalidState,
                    $"No rules passed in rule set '{name}'.");
        }
    }
}
