using ROC.Game.Common;
using ROC.Game.Sessions;
using ROC.Networking.Interactions.Data;
using UnityEngine;

namespace ROC.Networking.Interactions.Actions
{
    /// <summary>
    /// Temporary-but-data-driven action for the current intro door.
    /// It uses GameSessionManager's existing intro-completion path so the persistent character record is marked complete
    /// and the player is routed to the shared world location configured in CharacterRoutingService.
    /// </summary>
    [CreateAssetMenu(
        fileName = "CompleteIntroAndEnterSharedWorldAction",
        menuName = "ROC/Interactions/Actions/Complete Intro And Enter Shared World")]
    public sealed class CompleteIntroAndEnterSharedWorldActionDefinition : InteractionActionDefinition
    {
        public override ServerActionResult CanExecute(InteractionContext context)
        {
            if (context == null)
            {
                return ServerActionResult.Fail(ServerActionErrorCode.InvalidRequest, "Interaction context is missing.");
            }

            if (GameSessionManager.Instance == null)
            {
                return ServerActionResult.Fail(ServerActionErrorCode.InvalidState, "GameSessionManager is unavailable.");
            }

            if (string.IsNullOrWhiteSpace(context.CharacterId))
            {
                return ServerActionResult.Fail(ServerActionErrorCode.NoCharacterSelected, "No selected character is available for intro completion.");
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

            GameSessionManager.Instance.CompleteIntroAndEnterSharedWorld(
                context.ClientId,
                context.CharacterId);

            Log($"Requested intro completion and shared-world transfer for client {context.ClientId}, character {context.CharacterId}.");
            return ServerActionResult.Ok();
        }
    }
}
