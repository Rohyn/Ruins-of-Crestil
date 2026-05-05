using ROC.Game.Common;
using ROC.Game.Inventory;
using ROC.Networking.Interactions.Data;
using ROC.Networking.Inventory;
using ROC.Networking.Interactions.Rules;
using UnityEngine;

namespace ROC.Networking.Interactions.Actions
{
    /// <summary>
    /// Data-driven action that removes/consumes inventory items by definition ID.
    /// Use for keys, offerings, crafting inputs, container costs, and similar consumed resources.
    /// </summary>
    [CreateAssetMenu(
        fileName = "InventoryRemoveAction",
        menuName = "ROC/Interactions/Actions/Inventory Remove")]
    public sealed class InventoryRemoveActionDefinition : InteractionActionDefinition
    {
        [Header("Inventory Remove")]
        [SerializeField] private string itemDefinitionId;
        [SerializeField, Min(1)] private int quantity = 1;
        [SerializeField] private InventoryItemRuleLocationMode location = InventoryItemRuleLocationMode.Any;

        [Header("Source")]
        [Tooltip("Source label written into inventory change records. If empty, the target stable object ID is used.")]
        [SerializeField] private string source = "inventory_remove";

        public override ServerActionResult CanExecute(InteractionContext context)
        {
            if (context == null)
            {
                return ServerActionResult.Fail(ServerActionErrorCode.InvalidRequest, "Interaction context is missing.");
            }

            if (InventoryService.Instance == null)
            {
                return ServerActionResult.Fail(ServerActionErrorCode.InvalidState, "InventoryService is unavailable.");
            }

            if (string.IsNullOrWhiteSpace(itemDefinitionId))
            {
                return ServerActionResult.Fail(ServerActionErrorCode.InvalidRequest, "Inventory remove action has no item definition ID configured.");
            }

            if (quantity <= 0)
            {
                return ServerActionResult.Fail(ServerActionErrorCode.InvalidRequest, "Inventory remove quantity must be greater than zero.");
            }

            InventoryLocationKind? resolvedLocation = ResolveLocation(location);
            if (!InventoryService.Instance.HasItemByDefinitionForClient(
                    context.ClientId,
                    itemDefinitionId,
                    quantity,
                    resolvedLocation))
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.InvalidState,
                    $"Client does not have required item '{itemDefinitionId}' x{quantity}.");
            }

            return ServerActionResult.Ok();
        }

        public override ServerActionResult Execute(InteractionContext context)
        {
            ServerActionResult canExecute = CanExecute(context);
            if (!canExecute.Success)
            {
                return canExecute;
            }

            string resolvedSource = string.IsNullOrWhiteSpace(source)
                ? context.BuildDefaultSourceId("inventory_remove")
                : source;

            ServerActionResult result = InventoryService.Instance.RemoveItemsByDefinitionForClient(
                context.ClientId,
                itemDefinitionId,
                quantity,
                ResolveLocation(location),
                resolvedSource);

            if (!result.Success)
            {
                return result;
            }

            Log($"Removed '{itemDefinitionId}' x{quantity} from client {context.ClientId}.");
            return ServerActionResult.Ok();
        }

        private static InventoryLocationKind? ResolveLocation(InventoryItemRuleLocationMode mode)
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
