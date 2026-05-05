using ROC.Game.Common;
using ROC.Game.Conditions;
using ROC.Networking.Interactions.Data;
using ROC.Networking.World;
using UnityEngine;

namespace ROC.Networking.Interactions.Actions
{
    /// <summary>
    /// Data-driven action that begins a server-authoritative anchor.
    /// Optional Condition Id While Anchored is passed through AnchoringService so it is removed when the actor releases the anchor.
    /// </summary>
    [CreateAssetMenu(
        fileName = "AnchorAction",
        menuName = "ROC/Interactions/Actions/Anchor")]
    public sealed class AnchorActionDefinition : InteractionActionDefinition
    {
        [Header("Anchor Resolution")]
        [SerializeField] private InteractionAnchorResolutionMode resolutionMode = InteractionAnchorResolutionMode.TargetAnchorProvider;

        [Tooltip("Anchor ID on NetworkArrivalAnchorProvider. Used when Resolution Mode is Target Anchor Provider.")]
        [SerializeField] private string anchorId = "rest";

        [Tooltip("Optional exit anchor ID on NetworkArrivalAnchorProvider. If empty or unresolved, Anchor Id is used.")]
        [SerializeField] private string exitAnchorId = "exit";

        [Tooltip("If true, searches children for NetworkArrivalAnchorProvider when one is not found on the target root.")]
        [SerializeField] private bool searchChildrenForAnchorProvider = true;

        [Header("Anchor State")]
        [Tooltip("Source ID recorded on the anchor and optional condition. If empty, a source is built from the target stable object ID.")]
        [SerializeField] private string sourceId = "anchor_action";

        [SerializeField] private string exitPrompt = "Release";

        [Header("Optional Condition While Anchored")]
        [Tooltip("Optional condition applied through AnchoringService and automatically removed when the anchor is released. For the bed, use condition.resting.")]
        [SerializeField] private string conditionIdWhileAnchored;

        [SerializeField] private ConditionSourceType conditionSourceType = ConditionSourceType.Interaction;

        public override ServerActionResult CanExecute(InteractionContext context)
        {
            if (context == null)
            {
                return ServerActionResult.Fail(ServerActionErrorCode.InvalidRequest, "Interaction context is missing.");
            }

            if (context.Actor == null)
            {
                return ServerActionResult.Fail(ServerActionErrorCode.InvalidTarget, "Cannot anchor because the actor is missing.");
            }

            if (AnchoringService.Instance == null)
            {
                return ServerActionResult.Fail(ServerActionErrorCode.InvalidState, "AnchoringService is unavailable.");
            }

            if (AnchoringService.Instance.IsAnchored(context.ClientId))
            {
                return ServerActionResult.Fail(ServerActionErrorCode.InvalidState, "Actor is already anchored.");
            }

            if (!TryResolveAnchors(context, out Transform anchor, out Transform exitAnchor, out string reason))
            {
                return ServerActionResult.Fail(ServerActionErrorCode.InvalidTarget, reason);
            }

            if (anchor == null)
            {
                return ServerActionResult.Fail(ServerActionErrorCode.InvalidTarget, "Anchor transform could not be resolved.");
            }

            if (!string.IsNullOrWhiteSpace(conditionIdWhileAnchored) && ConditionService.Instance == null)
            {
                return ServerActionResult.Fail(ServerActionErrorCode.InvalidState, "ConditionService is unavailable.");
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

            if (!TryResolveAnchors(context, out Transform anchor, out Transform exitAnchor, out string reason))
            {
                return ServerActionResult.Fail(ServerActionErrorCode.InvalidTarget, reason);
            }

            string resolvedSourceId = string.IsNullOrWhiteSpace(sourceId)
                ? context.BuildDefaultSourceId("anchor")
                : sourceId;

            ServerActionResult result = AnchoringService.Instance.BeginAnchor(
                context.ClientId,
                context.Actor,
                anchor,
                exitAnchor,
                resolvedSourceId,
                exitPrompt,
                conditionIdWhileAnchored,
                conditionSourceType);

            if (!result.Success)
            {
                return result;
            }

            Log($"Anchored client {context.ClientId} at '{anchor.name}'.");
            return ServerActionResult.Ok();
        }

        private bool TryResolveAnchors(
            InteractionContext context,
            out Transform anchor,
            out Transform exitAnchor,
            out string reason)
        {
            anchor = null;
            exitAnchor = null;
            reason = string.Empty;

            if (context == null || context.TargetObject == null)
            {
                reason = "Target object is missing.";
                return false;
            }

            switch (resolutionMode)
            {
                case InteractionAnchorResolutionMode.TargetTransform:
                    anchor = context.TargetObject.transform;
                    exitAnchor = anchor;
                    return true;

                case InteractionAnchorResolutionMode.TargetAnchorProvider:
                    return TryResolveFromTargetProvider(context, out anchor, out exitAnchor, out reason);

                default:
                    reason = $"Unsupported anchor resolution mode: {resolutionMode}";
                    return false;
            }
        }

        private bool TryResolveFromTargetProvider(
            InteractionContext context,
            out Transform anchor,
            out Transform exitAnchor,
            out string reason)
        {
            anchor = null;
            exitAnchor = null;
            reason = string.Empty;

            NetworkArrivalAnchorProvider provider = context.TargetObject.GetComponent<NetworkArrivalAnchorProvider>();
            if (provider == null && searchChildrenForAnchorProvider)
            {
                provider = context.TargetObject.GetComponentInChildren<NetworkArrivalAnchorProvider>(includeInactive: true);
            }

            if (provider == null)
            {
                reason = "Target has no NetworkArrivalAnchorProvider.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(anchorId))
            {
                reason = "Anchor Id is empty.";
                return false;
            }

            if (!provider.TryGetAnchor(anchorId, out anchor) || anchor == null)
            {
                reason = $"Target anchor provider does not contain anchor id '{anchorId}'.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(exitAnchorId) && provider.TryGetAnchor(exitAnchorId, out Transform resolvedExit))
            {
                exitAnchor = resolvedExit;
            }
            else
            {
                exitAnchor = anchor;
            }

            return true;
        }
    }
}
