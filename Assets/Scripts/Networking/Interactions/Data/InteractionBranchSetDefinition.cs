using System.Collections.Generic;
using ROC.Game.Common;
using UnityEngine;

namespace ROC.Networking.Interactions.Data
{
    /// <summary>
    /// Ordered list of interaction branches. The first enabled branch whose rules pass is selected.
    /// Use this for doors, chests, altars, seasonal gathering nodes, and other interactions with mutually exclusive outcomes.
    /// </summary>
    [CreateAssetMenu(
        fileName = "InteractionBranchSet",
        menuName = "ROC/Interactions/Branches/Branch Set")]
    public sealed class InteractionBranchSetDefinition : ScriptableObject
    {
        [Header("Branches")]
        [SerializeField] private List<InteractionBranchEntry> branches = new();

        [Header("Debug")]
        [SerializeField] private bool verboseLogging;

        public IReadOnlyList<InteractionBranchEntry> Branches => branches;

        public bool TrySelectFirstPassingBranch(
            InteractionContext context,
            out InteractionBranchDefinition selectedBranch,
            out InteractionRuleResult selectedRuleResult)
        {
            selectedBranch = null;
            selectedRuleResult = InteractionRuleResult.Fail(
                ServerActionErrorCode.InvalidState,
                $"No interaction branch matched in branch set '{name}'.");

            if (branches == null || branches.Count == 0)
            {
                selectedRuleResult = InteractionRuleResult.Fail(
                    ServerActionErrorCode.InvalidState,
                    $"Interaction branch set '{name}' has no branches configured.");
                return false;
            }

            bool sawEnabledBranch = false;
            InteractionRuleResult firstFailure = selectedRuleResult;

            for (int i = 0; i < branches.Count; i++)
            {
                InteractionBranchEntry entry = branches[i];
                if (!entry.Enabled)
                {
                    continue;
                }

                sawEnabledBranch = true;
                InteractionBranchDefinition branch = entry.Branch;
                if (branch == null)
                {
                    InteractionRuleResult missingBranch = InteractionRuleResult.Fail(
                        ServerActionErrorCode.InvalidState,
                        $"Interaction branch set '{name}' has an enabled empty branch slot at index {i}.");

                    if (firstFailure.Passed || firstFailure.ErrorCode == ServerActionErrorCode.InvalidState)
                    {
                        firstFailure = missingBranch;
                    }

                    if (verboseLogging)
                    {
                        Debug.LogWarning($"[InteractionBranchSetDefinition] {missingBranch.DebugMessage}", this);
                    }

                    continue;
                }

                InteractionRuleResult ruleResult = branch.EvaluateRulesServer(context);
                if (verboseLogging)
                {
                    string state = ruleResult.Passed ? "PASS" : "FAIL";
                    Debug.Log($"[InteractionBranchSetDefinition] Branch '{branch.name}' => {state}: {ruleResult}", this);
                }

                if (ruleResult.Passed)
                {
                    selectedBranch = branch;
                    selectedRuleResult = ruleResult;
                    return true;
                }

                if (firstFailure.Passed || firstFailure.ErrorCode == ServerActionErrorCode.InvalidState)
                {
                    firstFailure = ruleResult;
                }
            }

            selectedRuleResult = sawEnabledBranch
                ? firstFailure
                : InteractionRuleResult.Fail(
                    ServerActionErrorCode.InvalidState,
                    $"Interaction branch set '{name}' has no enabled branches.");

            return false;
        }
    }
}
