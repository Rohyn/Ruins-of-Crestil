using System.Collections.Generic;
using ROC.Game.Common;
using ROC.Networking.Interactions.Data;
using ROC.Networking.Sessions;
using ROC.Networking.World;
using Unity.Netcode;
using UnityEngine;

namespace ROC.Networking.Interactions
{
    /// <summary>
    /// The single server-executed interactable for a target object.
    /// It evaluates data-driven object rules, then either:
    /// - runs its direct ordered actions, or
    /// - selects the first passing branch from a branch set and runs that branch's ordered actions.
    /// Stateful object gates such as InteractionUsageGate can live beside this component as availability rules/observers.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkInstanceObject))]
    [RequireComponent(typeof(NetworkInteractableTarget))]
    public sealed class InteractionExecutor : NetworkBehaviour, IServerInteractable, IInteractionAvailabilityRule, IInteractionAvailabilityDependencyProvider
    {
        [Header("Interaction")]
        [SerializeField, Min(0.1f)] private float maxInteractDistance = 3f;

        [Header("Object Rules")]
        [Tooltip("Optional data-driven rule set that must pass before any direct action or branch executes.")]
        [SerializeField] private InteractionRuleSetDefinition objectRules;

        [Header("Branching")]
        [Tooltip("Optional ordered branch set. If assigned, direct Actions below are ignored and the first passing branch is executed instead.")]
        [SerializeField] private InteractionBranchSetDefinition branchSet;

        [Header("Direct Actions")]
        [Tooltip("Actions run in this order only when no Branch Set is assigned. Required actions must validate and execute successfully. Optional action failures are logged but do not fail the whole interaction.")]
        [SerializeField] private List<InteractionActionEntry> actions = new();

        [Header("Validation")]
        [Tooltip("If true, all required actions in the selected path are validated before any action executes. Keep enabled to avoid common partial-success cases.")]
        [SerializeField] private bool validateRequiredActionsBeforeExecution = true;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging;

        private NetworkObject _networkObject;
        private NetworkInteractableTarget _target;
        private NetworkInstanceObject _instanceObject;

        public float MaxInteractDistance => maxInteractDistance;

        /// <summary>
        /// Client-preview availability only considers object-level rules. Branch selection remains authoritative server logic.
        /// </summary>
        public InteractionRuleDependencyFlags LocalPreviewDependencies =>
            objectRules != null ? objectRules.GetClientPreviewDependencyFlags() : InteractionRuleDependencyFlags.None;

        private void Awake()
        {
            _networkObject = GetComponent<NetworkObject>();
            _target = GetComponent<NetworkInteractableTarget>();
            _instanceObject = GetComponent<NetworkInstanceObject>();
        }

        public bool CanSelect(ulong clientId, out string reason)
        {
            reason = string.Empty;

            if (objectRules == null)
            {
                return true;
            }

            InteractionContext context = InteractionContext.CreateClientPreview(
                clientId,
                _networkObject,
                _target,
                this,
                _instanceObject);

            InteractionRuleResult result = objectRules.EvaluateClientPreview(context);
            reason = result.DebugMessage;
            return result.Passed;
        }

        public bool CanInteract(ulong clientId, NetworkObject actor, out string reason)
        {
            ServerActionResult result = ValidateInteraction(clientId, actor);
            reason = result.Message;
            return result.Success;
        }

        public void Interact(ulong clientId, NetworkObject actor)
        {
            ServerActionResult result = ExecuteInteraction(clientId, actor);
            if (!result.Success)
            {
                Debug.LogWarning($"[InteractionExecutor] Interaction failed: {result}", this);
            }
        }

        public ServerActionResult ValidateInteraction(ulong clientId, NetworkObject actor)
        {
            if (!IsServer)
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.PermissionDenied,
                    "Interactions can only be validated by the server.");
            }

            if (!TryBuildContext(clientId, actor, out InteractionContext context, out ServerActionResult contextResult))
            {
                return contextResult;
            }

            ServerActionResult objectRuleResult = ValidateObjectRules(context);
            if (!objectRuleResult.Success)
            {
                return objectRuleResult;
            }

            if (branchSet != null)
            {
                if (!TrySelectBranch(context, out InteractionBranchDefinition branch, out ServerActionResult branchResult))
                {
                    return branchResult;
                }

                return ValidateActionEntries(context, branch.Actions, $"branch '{branch.name}'");
            }

            return ValidateActionEntries(context, actions, "direct action list");
        }

        public ServerActionResult ExecuteInteraction(ulong clientId, NetworkObject actor)
        {
            if (!IsServer)
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.PermissionDenied,
                    "Interactions can only be executed by the server.");
            }

            if (!TryBuildContext(clientId, actor, out InteractionContext context, out ServerActionResult contextResult))
            {
                return contextResult;
            }

            ServerActionResult objectRuleResult = ValidateObjectRules(context);
            if (!objectRuleResult.Success)
            {
                return objectRuleResult;
            }

            IReadOnlyList<InteractionActionEntry> selectedActions;
            string selectedPathName;

            if (branchSet != null)
            {
                if (!TrySelectBranch(context, out InteractionBranchDefinition branch, out ServerActionResult branchResult))
                {
                    return branchResult;
                }

                selectedActions = branch.Actions;
                selectedPathName = $"branch '{branch.name}'";
            }
            else
            {
                selectedActions = actions;
                selectedPathName = "direct action list";
            }

            if (validateRequiredActionsBeforeExecution)
            {
                ServerActionResult validation = ValidateActionEntries(context, selectedActions, selectedPathName);
                if (!validation.Success)
                {
                    return validation;
                }
            }

            ServerActionResult execution = ExecuteActionEntries(context, selectedActions, selectedPathName);
            if (!execution.Success)
            {
                return execution;
            }

            if (verboseLogging)
            {
                Debug.Log($"[InteractionExecutor] Executed {selectedPathName} on '{name}' for client {clientId}.", this);
            }

            return ServerActionResult.Ok();
        }

        private ServerActionResult ValidateObjectRules(InteractionContext context)
        {
            if (objectRules == null)
            {
                return ServerActionResult.Ok();
            }

            InteractionRuleResult ruleResult = objectRules.EvaluateServer(context);
            if (!ruleResult.Passed && verboseLogging)
            {
                Debug.LogWarning($"[InteractionExecutor] Object rule set rejected interaction: {ruleResult}", this);
            }

            return ruleResult.ToServerActionResult();
        }

        private bool TrySelectBranch(
            InteractionContext context,
            out InteractionBranchDefinition branch,
            out ServerActionResult result)
        {
            branch = null;

            if (branchSet == null)
            {
                result = ServerActionResult.Fail(ServerActionErrorCode.InvalidState, "No branch set is assigned.");
                return false;
            }

            if (!branchSet.TrySelectFirstPassingBranch(context, out branch, out InteractionRuleResult ruleResult))
            {
                result = ruleResult.ToServerActionResult();
                if (verboseLogging)
                {
                    Debug.LogWarning($"[InteractionExecutor] No branch matched: {result}", this);
                }

                return false;
            }

            if (branch == null)
            {
                result = ServerActionResult.Fail(ServerActionErrorCode.InvalidState, "Selected branch is null.");
                return false;
            }

            if (!branch.HasEnabledActions())
            {
                result = ServerActionResult.Fail(
                    ServerActionErrorCode.InvalidState,
                    $"Selected branch '{branch.name}' has no enabled actions.");
                return false;
            }

            result = ServerActionResult.Ok();
            return true;
        }

        private ServerActionResult ValidateActionEntries(
            InteractionContext context,
            IReadOnlyList<InteractionActionEntry> entries,
            string pathName)
        {
            bool hasEnabledAction = false;

            if (context == null)
            {
                return ServerActionResult.Fail(ServerActionErrorCode.InvalidRequest, "Interaction context is missing.");
            }

            if (entries == null)
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.InvalidState,
                    $"Interaction {pathName} has no action list.");
            }

            for (int i = 0; i < entries.Count; i++)
            {
                InteractionActionEntry entry = entries[i];
                if (!entry.Enabled)
                {
                    continue;
                }

                hasEnabledAction = true;
                InteractionActionDefinition action = entry.Action;
                if (action == null)
                {
                    if (entry.IsRequired)
                    {
                        return ServerActionResult.Fail(
                            ServerActionErrorCode.InvalidState,
                            $"Required interaction action slot {i} in {pathName} has no action definition.");
                    }

                    continue;
                }

                ServerActionResult result = action.CanExecute(context);
                if (!result.Success && entry.IsRequired)
                {
                    return result;
                }

                if (!result.Success)
                {
                    LogOptionalFailure(action, result);
                }
            }

            return hasEnabledAction
                ? ServerActionResult.Ok()
                : ServerActionResult.Fail(ServerActionErrorCode.InvalidState, $"Interaction {pathName} has no enabled actions.");
        }

        private ServerActionResult ExecuteActionEntries(
            InteractionContext context,
            IReadOnlyList<InteractionActionEntry> entries,
            string pathName)
        {
            bool anyActionExecuted = false;

            if (context == null)
            {
                return ServerActionResult.Fail(ServerActionErrorCode.InvalidRequest, "Interaction context is missing.");
            }

            if (entries == null)
            {
                return ServerActionResult.Fail(
                    ServerActionErrorCode.InvalidState,
                    $"Interaction {pathName} has no action list.");
            }

            for (int i = 0; i < entries.Count; i++)
            {
                InteractionActionEntry entry = entries[i];
                if (!entry.Enabled)
                {
                    continue;
                }

                InteractionActionDefinition action = entry.Action;
                if (action == null)
                {
                    if (entry.IsRequired)
                    {
                        return ServerActionResult.Fail(
                            ServerActionErrorCode.InvalidState,
                            $"Required interaction action slot {i} in {pathName} has no action definition.");
                    }

                    continue;
                }

                if (!validateRequiredActionsBeforeExecution)
                {
                    ServerActionResult canExecute = action.CanExecute(context);
                    if (!canExecute.Success)
                    {
                        if (entry.IsRequired)
                        {
                            return canExecute;
                        }

                        LogOptionalFailure(action, canExecute);
                        continue;
                    }
                }

                ServerActionResult executed = action.Execute(context);
                if (!executed.Success)
                {
                    if (entry.IsRequired)
                    {
                        return executed;
                    }

                    LogOptionalFailure(action, executed);
                    continue;
                }

                anyActionExecuted = true;
            }

            return anyActionExecuted
                ? ServerActionResult.Ok()
                : ServerActionResult.Fail(ServerActionErrorCode.InvalidState, $"Interaction {pathName} has no enabled executable actions.");
        }

        private bool TryBuildContext(
            ulong clientId,
            NetworkObject actor,
            out InteractionContext context,
            out ServerActionResult result)
        {
            context = null;

            if (_networkObject == null)
            {
                result = ServerActionResult.Fail(ServerActionErrorCode.InvalidTarget, "Executor has no NetworkObject.");
                return false;
            }

            if (_target == null)
            {
                result = ServerActionResult.Fail(ServerActionErrorCode.InvalidTarget, "Executor has no NetworkInteractableTarget.");
                return false;
            }

            if (_instanceObject == null)
            {
                result = ServerActionResult.Fail(ServerActionErrorCode.InvalidTarget, "Executor has no NetworkInstanceObject.");
                return false;
            }

            if (actor == null)
            {
                result = ServerActionResult.Fail(ServerActionErrorCode.InvalidTarget, "No actor is available for the interaction.");
                return false;
            }

            PlayerSessionRegistry registry = PlayerSessionRegistry.Instance;
            if (registry == null || !registry.TryGet(clientId, out PlayerSessionData session))
            {
                result = ServerActionResult.Fail(ServerActionErrorCode.NoSession, "No server session exists for this client.");
                return false;
            }

            context = new InteractionContext(
                clientId,
                actor,
                _networkObject,
                _target,
                this,
                session,
                _instanceObject);

            result = ServerActionResult.Ok();
            return true;
        }

        private void LogOptionalFailure(InteractionActionDefinition action, ServerActionResult result)
        {
            if (!verboseLogging)
            {
                return;
            }

            string actionName = action != null ? action.name : "null";
            Debug.LogWarning($"[InteractionExecutor] Optional action '{actionName}' failed: {result}", this);
        }
    }
}
