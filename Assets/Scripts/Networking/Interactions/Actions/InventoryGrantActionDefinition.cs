using ROC.Game.Common;
using ROC.Networking.Inventory;
using ROC.Networking.Interactions.Data;
using UnityEngine;

namespace ROC.Networking.Interactions.Actions
{
    /// <summary>
    /// Data-driven action that grants one or more inventory items.
    /// Does not evaluate progress requirements, mutate progress flags, or own once-only behavior.
    /// </summary>
    [CreateAssetMenu(
        fileName = "InventoryGrantAction",
        menuName = "ROC/Interactions/Actions/Inventory Grant")]
    public sealed class InventoryGrantActionDefinition : InteractionActionDefinition
    {
        [Header("Inventory Grants")]
        [SerializeField] private InventoryGrantEntry[] grants;

        [Header("Source")]
        [Tooltip("Source label written into inventory change records. If empty, the target stable object ID is used.")]
        [SerializeField] private string source = "inventory_grant";

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

            if (grants == null || grants.Length == 0)
            {
                return ServerActionResult.Fail(ServerActionErrorCode.InvalidState, "Inventory grant action has no grants configured.");
            }

            for (int i = 0; i < grants.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(grants[i].ItemDefinitionId))
                {
                    return ServerActionResult.Fail(
                        ServerActionErrorCode.InvalidRequest,
                        $"Inventory grant action has an empty item definition ID at index {i}.");
                }
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
                ? context.BuildDefaultSourceId("inventory_grant")
                : source;

            for (int i = 0; i < grants.Length; i++)
            {
                InventoryGrantEntry grant = grants[i];
                ServerActionResult result = InventoryService.Instance.GrantItemForClient(
                    context.ClientId,
                    grant.ItemDefinitionId,
                    grant.Quantity,
                    resolvedSource);

                if (!result.Success)
                {
                    return result;
                }
            }

            Log($"Granted {grants.Length} inventory grant(s) to client {context.ClientId}.");
            return ServerActionResult.Ok();
        }
    }
}
