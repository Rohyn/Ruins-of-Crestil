using ROC.Game.Common;
using ROC.Game.Inventory;
using ROC.Networking.Interactions.Data;
using ROC.Networking.Inventory;
using UnityEngine;

namespace ROC.Networking.Interactions.Rules
{
    [CreateAssetMenu(
        fileName = "InventoryItemRule",
        menuName = "ROC/Interactions/Rules/Inventory Item")]
    public sealed class InventoryItemRuleDefinition : InteractionRuleDefinition
    {
        [Header("Inventory Item")]
        [SerializeField] private InteractionRuleRequirementMode requirementMode = InteractionRuleRequirementMode.MustHave;
        [SerializeField] private string itemDefinitionId;
        [SerializeField, Min(1)] private int minimumQuantity = 1;
        [SerializeField] private InventoryItemRuleLocationMode locationMode = InventoryItemRuleLocationMode.Any;

        public override InteractionRuleDependencyFlags DependencyFlags =>
            InteractionRuleDependencyFlags.Inventory | InteractionRuleDependencyFlags.Equipment;

        public override InteractionRuleResult EvaluateServer(InteractionContext context)
        {
            if (context == null)
            {
                return Fail(ServerActionErrorCode.InvalidRequest, "Interaction context is missing.");
            }

            if (string.IsNullOrWhiteSpace(itemDefinitionId))
            {
                return Fail(ServerActionErrorCode.InvalidRequest, "Inventory item rule has no item definition ID configured.");
            }

            if (InventoryService.Instance == null)
            {
                return Fail(ServerActionErrorCode.InvalidState, "InventoryService is unavailable.");
            }

            InventoryLocationKind? location = ResolveLocationFilter(locationMode);
            bool hasItem = InventoryService.Instance.HasItemByDefinitionForClient(
                context.ClientId,
                itemDefinitionId,
                Mathf.Max(1, minimumQuantity),
                location);

            if (!MatchesRequirement(requirementMode, hasItem))
            {
                return Fail(
                    ServerActionErrorCode.InvalidState,
                    requirementMode == InteractionRuleRequirementMode.MustHave
                        ? $"Missing required item: {itemDefinitionId}"
                        : $"Forbidden item is present: {itemDefinitionId}");
            }

            return Pass();
        }

        public override InteractionRuleResult EvaluateClientPreview(InteractionContext context)
        {
            if (!EnableClientPreview || string.IsNullOrWhiteSpace(itemDefinitionId) || ClientInventoryState.Local == null)
            {
                return Pass();
            }

            InventoryLocationKind? location = ResolveLocationFilter(locationMode);
            int count = 0;
            for (int i = 0; i < ClientInventoryState.Local.Items.Count; i++)
            {
                InventoryItemSnapshot item = ClientInventoryState.Local.Items[i];
                if (item.DefinitionId.ToString() != itemDefinitionId)
                {
                    continue;
                }

                if (location.HasValue && item.Location != location.Value)
                {
                    continue;
                }

                count += Mathf.Max(0, item.Quantity);
                if (count >= Mathf.Max(1, minimumQuantity))
                {
                    break;
                }
            }

            bool hasItem = count >= Mathf.Max(1, minimumQuantity);
            if (!MatchesRequirement(requirementMode, hasItem))
            {
                return Fail(
                    ServerActionErrorCode.InvalidState,
                    requirementMode == InteractionRuleRequirementMode.MustHave
                        ? $"Missing required item: {itemDefinitionId}"
                        : $"Forbidden item is present: {itemDefinitionId}");
            }

            return Pass();
        }

        private static InventoryLocationKind? ResolveLocationFilter(InventoryItemRuleLocationMode mode)
        {
            switch (mode)
            {
                case InventoryItemRuleLocationMode.Bag:
                    return InventoryLocationKind.Bag;
                case InventoryItemRuleLocationMode.Equipped:
                    return InventoryLocationKind.Equipped;
                case InventoryItemRuleLocationMode.Any:
                default:
                    return null;
            }
        }
    }
}
