using ROC.Game.Common;
using ROC.Game.ProgressFlags;
using ROC.Networking.Interactions.Data;
using UnityEngine;

namespace ROC.Networking.Interactions.Actions
{
    /// <summary>
    /// Data-driven action that applies progress flag mutations.
    /// Requirements/gates should be separate; this action only mutates facts on success.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ProgressFlagMutationAction",
        menuName = "ROC/Interactions/Actions/Progress Flag Mutations")]
    public sealed class ProgressFlagMutationActionDefinition : InteractionActionDefinition
    {
        [Header("Progress Mutations")]
        [SerializeField] private ProgressFlagMutation[] mutations;

        public override ServerActionResult CanExecute(InteractionContext context)
        {
            if (context == null)
            {
                return ServerActionResult.Fail(ServerActionErrorCode.InvalidRequest, "Interaction context is missing.");
            }

            if (mutations == null || mutations.Length == 0)
            {
                return ServerActionResult.Fail(ServerActionErrorCode.InvalidState, "Progress flag mutation action has no mutations configured.");
            }

            if (ProgressFlagService.Instance == null)
            {
                return ServerActionResult.Fail(ServerActionErrorCode.InvalidState, "ProgressFlagService is unavailable.");
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

            ServerActionResult result = ProgressFlagService.Instance.ApplyMutationsForClient(
                context.ClientId,
                mutations);

            if (!result.Success)
            {
                return result;
            }

            Log($"Applied {mutations.Length} progress mutation(s) to client {context.ClientId}.");
            return ServerActionResult.Ok();
        }
    }
}
