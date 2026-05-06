using ROC.Game.Common;
using ROC.Game.Conditions;
using ROC.Networking.Conditions;
using ROC.Networking.Interactions.Data;
using UnityEngine;

namespace ROC.Networking.Rules
{
    /// <summary>
    /// Rule for whether the local/server player is currently anchored. Useful for prompts and later interactions.
    /// </summary>
    [CreateAssetMenu(
        fileName = "AnchorStateRule",
        menuName = "ROC/Rules/Anchor State")]
    public sealed class AnchorStateRuleDefinition : InteractionRuleDefinition
    {
        [Header("Anchor State")]
        [SerializeField] private InteractionRuleRequirementMode requirementMode = InteractionRuleRequirementMode.MustHave;

        public override InteractionRuleDependencyFlags DependencyFlags => InteractionRuleDependencyFlags.Conditions;

        public override InteractionRuleResult EvaluateServer(InteractionContext context)
        {
            if (context == null)
            {
                return Fail(ServerActionErrorCode.InvalidRequest, "Interaction context is missing.");
            }

            if (AnchoringService.Instance == null)
            {
                return Fail(ServerActionErrorCode.InvalidState, "AnchoringService is unavailable.");
            }

            bool anchored = AnchoringService.Instance.IsAnchored(context.ClientId);
            if (!MatchesRequirement(requirementMode, anchored))
            {
                return Fail(
                    ServerActionErrorCode.InvalidState,
                    requirementMode == InteractionRuleRequirementMode.MustHave
                        ? "Player is not anchored."
                        : "Player is anchored.");
            }

            return Pass();
        }

        public override InteractionRuleResult EvaluateClientPreview(InteractionContext context)
        {
            if (!EnableClientPreview || NetworkPlayerConditionState.Local == null)
            {
                return Pass();
            }

            bool anchored = NetworkPlayerConditionState.Local.IsAnchored.Value;
            if (!MatchesRequirement(requirementMode, anchored))
            {
                return Fail(
                    ServerActionErrorCode.InvalidState,
                    requirementMode == InteractionRuleRequirementMode.MustHave
                        ? "Player is not anchored."
                        : "Player is anchored.");
            }

            return Pass();
        }
    }
}
