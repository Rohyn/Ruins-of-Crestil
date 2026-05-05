using ROC.Game.Common;
using ROC.Game.ProgressFlags;
using ROC.Networking.Interactions.Data;
using ROC.Networking.ProgressFlags;
using UnityEngine;

namespace ROC.Networking.Interactions.Rules
{
    [CreateAssetMenu(
        fileName = "ProgressFlagRule",
        menuName = "ROC/Interactions/Rules/Progress Flag")]
    public sealed class ProgressFlagRuleDefinition : InteractionRuleDefinition
    {
        [Header("Progress Flag")]
        [SerializeField] private InteractionRuleRequirementMode requirementMode = InteractionRuleRequirementMode.MustHave;
        [SerializeField] private string flagId;

        public override InteractionRuleDependencyFlags DependencyFlags => InteractionRuleDependencyFlags.ProgressFlags;

        public override InteractionRuleResult EvaluateServer(InteractionContext context)
        {
            if (context == null)
            {
                return Fail(ServerActionErrorCode.InvalidRequest, "Interaction context is missing.");
            }

            if (string.IsNullOrWhiteSpace(flagId))
            {
                return Fail(ServerActionErrorCode.InvalidRequest, "Progress flag rule has no flag ID configured.");
            }

            if (ProgressFlagService.Instance == null)
            {
                return Fail(ServerActionErrorCode.InvalidState, "ProgressFlagService is unavailable.");
            }

            bool hasFlag = ProgressFlagService.Instance.HasFlagForClient(context.ClientId, flagId);
            if (!MatchesRequirement(requirementMode, hasFlag))
            {
                return Fail(
                    ServerActionErrorCode.InvalidState,
                    requirementMode == InteractionRuleRequirementMode.MustHave
                        ? $"Missing required progress flag: {flagId}"
                        : $"Forbidden progress flag is present: {flagId}");
            }

            return Pass();
        }

        public override InteractionRuleResult EvaluateClientPreview(InteractionContext context)
        {
            if (!EnableClientPreview)
            {
                return Pass();
            }

            if (string.IsNullOrWhiteSpace(flagId) || ClientProgressFlagState.Local == null)
            {
                return Pass();
            }

            bool hasFlag = ClientProgressFlagState.Local.HasFlag(flagId);
            if (!MatchesRequirement(requirementMode, hasFlag))
            {
                return Fail(
                    ServerActionErrorCode.InvalidState,
                    requirementMode == InteractionRuleRequirementMode.MustHave
                        ? $"Missing required progress flag: {flagId}"
                        : $"Forbidden progress flag is present: {flagId}");
            }

            return Pass();
        }
    }
}
