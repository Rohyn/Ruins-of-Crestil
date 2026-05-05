using ROC.Game.Common;
using ROC.Game.Inventory;
using ROC.Networking.Interactions.Data;
using ROC.Networking.Inventory;
using UnityEngine;

namespace ROC.Networking.Interactions.Rules
{
    [CreateAssetMenu(
        fileName = "EquippedItemRule",
        menuName = "ROC/Interactions/Rules/Equipped Item")]
    public sealed class EquippedItemRuleDefinition : InteractionRuleDefinition
    {
        [Header("Equipped Item")]
        [SerializeField] private InteractionRuleRequirementMode requirementMode = InteractionRuleRequirementMode.MustHave;
        [SerializeField] private string itemDefinitionId;

        public override InteractionRuleDependencyFlags DependencyFlags => InteractionRuleDependencyFlags.Equipment;

        public override InteractionRuleResult EvaluateServer(InteractionContext context)
        {
            if (context == null)
            {
                return Fail(ServerActionErrorCode.InvalidRequest, "Interaction context is missing.");
            }

            if (string.IsNullOrWhiteSpace(itemDefinitionId))
            {
                return Fail(ServerActionErrorCode.InvalidRequest, "Equipped item rule has no item definition ID configured.");
            }

            if (InventoryService.Instance == null)
            {
                return Fail(ServerActionErrorCode.InvalidState, "InventoryService is unavailable.");
            }

            bool equipped = InventoryService.Instance.IsEquippedByDefinitionForClient(context.ClientId, itemDefinitionId);
            if (!MatchesRequirement(requirementMode, equipped))
            {
                return Fail(
                    ServerActionErrorCode.InvalidState,
                    requirementMode == InteractionRuleRequirementMode.MustHave
                        ? $"Required item is not equipped: {itemDefinitionId}"
                        : $"Forbidden item is equipped: {itemDefinitionId}");
            }

            return Pass();
        }

        public override InteractionRuleResult EvaluateClientPreview(InteractionContext context)
        {
            if (!EnableClientPreview || string.IsNullOrWhiteSpace(itemDefinitionId) || ClientInventoryState.Local == null)
            {
                return Pass();
            }

            bool equipped = false;
            for (int i = 0; i < ClientInventoryState.Local.Items.Count; i++)
            {
                InventoryItemSnapshot item = ClientInventoryState.Local.Items[i];
                if (item.DefinitionId.ToString() == itemDefinitionId && item.Location == InventoryLocationKind.Equipped)
                {
                    equipped = true;
                    break;
                }
            }

            if (!MatchesRequirement(requirementMode, equipped))
            {
                return Fail(
                    ServerActionErrorCode.InvalidState,
                    requirementMode == InteractionRuleRequirementMode.MustHave
                        ? $"Required item is not equipped: {itemDefinitionId}"
                        : $"Forbidden item is equipped: {itemDefinitionId}");
            }

            return Pass();
        }
    }
}
