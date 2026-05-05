using ROC.Game.Common;
using ROC.Game.Conditions;
using ROC.Networking.Interactions.Data;
using UnityEngine;

namespace ROC.Networking.Interactions.Rules
{
    [CreateAssetMenu(
        fileName = "ConditionRule",
        menuName = "ROC/Interactions/Rules/Condition")]
    public sealed class ConditionRuleDefinition : InteractionRuleDefinition
    {
        [Header("Condition")]
        [SerializeField] private InteractionRuleRequirementMode requirementMode = InteractionRuleRequirementMode.MustHave;
        [SerializeField] private string conditionId;

        public override InteractionRuleDependencyFlags DependencyFlags => InteractionRuleDependencyFlags.Conditions;

        public override InteractionRuleResult EvaluateServer(InteractionContext context)
        {
            if (context == null)
            {
                return Fail(ServerActionErrorCode.InvalidRequest, "Interaction context is missing.");
            }

            if (string.IsNullOrWhiteSpace(conditionId))
            {
                return Fail(ServerActionErrorCode.InvalidRequest, "Condition rule has no condition ID configured.");
            }

            if (ConditionService.Instance == null)
            {
                return Fail(ServerActionErrorCode.InvalidState, "ConditionService is unavailable.");
            }

            bool hasCondition = ConditionService.Instance.HasConditionForClient(context.ClientId, conditionId);
            if (!MatchesRequirement(requirementMode, hasCondition))
            {
                return Fail(
                    ServerActionErrorCode.InvalidState,
                    requirementMode == InteractionRuleRequirementMode.MustHave
                        ? $"Missing required condition: {conditionId}"
                        : $"Forbidden condition is present: {conditionId}");
            }

            return Pass();
        }
    }
}
