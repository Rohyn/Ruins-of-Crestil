using System;
using ROC.Game.Common;
using ROC.Networking.Interactions.Data;
using ROC.Networking.World;
using UnityEngine;

namespace ROC.Networking.Rules
{
    /// <summary>
    /// Rule for requiring or blocking the player's current logical world scene/instance.
    /// Use scene IDs such as intro_arrival or dkeep_center, not Unity scene asset names.
    /// </summary>
    [CreateAssetMenu(
        fileName = "CurrentWorldSceneRule",
        menuName = "ROC/Rules/Current World Scene")]
    public sealed class CurrentWorldSceneRuleDefinition : InteractionRuleDefinition
    {
        [Header("Current World Scene")]
        [SerializeField] private InteractionRuleRequirementMode requirementMode = InteractionRuleRequirementMode.MustHave;

        [Tooltip("Logical world scene IDs. Example: intro_arrival. For MustHave, the current scene must match one. For MustNotHave, the current scene must match none.")]
        [SerializeField] private string[] sceneIds;

        [Tooltip("Optional logical instance IDs. Leave empty to ignore instance ID.")]
        [SerializeField] private string[] instanceIds;

        public override InteractionRuleDependencyFlags DependencyFlags => InteractionRuleDependencyFlags.SceneOrInstance;

        public override InteractionRuleResult EvaluateServer(InteractionContext context)
        {
            if (context == null)
            {
                return Fail(ServerActionErrorCode.InvalidRequest, "Interaction context is missing.");
            }

            return EvaluateResolved(context.SceneId, context.InstanceId, serverSide: true);
        }

        public override InteractionRuleResult EvaluateClientPreview(InteractionContext context)
        {
            if (!EnableClientPreview)
            {
                return Pass();
            }

            ClientWorldSceneState.EnsureSubscribed();
            return EvaluateResolved(
                ClientWorldSceneState.CurrentSceneId,
                ClientWorldSceneState.CurrentInstanceId,
                serverSide: false);
        }

        private InteractionRuleResult EvaluateResolved(string currentSceneId, string currentInstanceId, bool serverSide)
        {
            if (!HasAnyValidId(sceneIds))
            {
                return Fail(
                    ServerActionErrorCode.InvalidState,
                    $"Current world scene rule '{name}' has no scene IDs configured.");
            }

            string normalizedSceneId = NormalizeId(currentSceneId);
            string normalizedInstanceId = NormalizeId(currentInstanceId);

            bool sceneMatches = ContainsId(sceneIds, normalizedSceneId);
            bool instanceMatches = !HasAnyValidId(instanceIds) || ContainsId(instanceIds, normalizedInstanceId);
            bool matches = sceneMatches && instanceMatches;

            if (!MatchesRequirement(requirementMode, matches))
            {
                string modeText = requirementMode == InteractionRuleRequirementMode.MustHave ? "be in" : "not be in";
                string side = serverSide ? "server" : "client preview";
                return Fail(
                    ServerActionErrorCode.InvalidState,
                    $"Current world scene rule '{name}' failed on {side}. Current scene='{normalizedSceneId}', instance='{normalizedInstanceId}'. Expected to {modeText} configured scene/instance.");
            }

            return Pass();
        }

        private static bool ContainsId(string[] values, string target)
        {
            if (values == null || string.IsNullOrWhiteSpace(target))
            {
                return false;
            }

            string normalizedTarget = NormalizeId(target);

            for (int i = 0; i < values.Length; i++)
            {
                string normalizedValue = NormalizeId(values[i]);
                if (string.IsNullOrWhiteSpace(normalizedValue))
                {
                    continue;
                }

                if (string.Equals(normalizedValue, normalizedTarget, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasAnyValidId(string[] values)
        {
            if (values == null)
            {
                return false;
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
