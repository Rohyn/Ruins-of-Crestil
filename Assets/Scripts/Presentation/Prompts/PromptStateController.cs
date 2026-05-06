using System.Collections.Generic;
using ROC.Game.ProgressFlags;
using ROC.Networking.Conditions;
using ROC.Networking.Interactions.Data;
using ROC.Networking.Inventory;
using ROC.Networking.ProgressFlags;
using ROC.Networking.World;
using Unity.Netcode;
using UnityEngine;

namespace ROC.Presentation.Prompts
{
    /// <summary>
    /// Owner-local state observer that evaluates prompt definitions against locally mirrored gameplay state.
    /// It queues initial prompt lines when eligibility becomes true and reminder lines while eligibility remains true.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PromptStateController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerPromptController promptController;
        [SerializeField] private PromptCatalog promptCatalog;

        [Header("Startup / Binding")]
        [SerializeField, Min(0f)] private float startupDelaySeconds = 1.0f;

        [Tooltip("If true, prompt evaluation is suppressed until the local client has a streamed logical world scene. This prevents intro prompts from appearing on character select or during transfers.")]
        [SerializeField] private bool requireLocalWorldSceneReady = true;
        [SerializeField, Min(0.05f)] private float bindingSearchIntervalSeconds = 0.5f;
        [SerializeField, Min(0.05f)] private float fallbackEvaluationIntervalSeconds = 0.5f;

        [Header("Reminder Evaluation")]
        [SerializeField, Min(0.1f)] private float reminderTickIntervalSeconds = 0.5f;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = false;

        private readonly Dictionary<string, PromptRuntimeState> _runtimeByPromptId = new();

        private ClientInventoryState _inventoryState;
        private ClientProgressFlagState _progressFlagState;
        private NetworkPlayerConditionState _conditionState;

        private float _notBeforeRealtime;
        private float _nextBindingSearchTime;
        private float _nextFallbackEvaluationTime;
        private float _nextReminderTickTime;
        private bool _hasDoneInitialEvaluation;

        private void Awake()
        {
            ResolveReferences();
            _notBeforeRealtime = Time.realtimeSinceStartup + Mathf.Max(0f, startupDelaySeconds);
        }

        private void OnEnable()
        {
            ClientInventoryState.LocalInventoryReady += HandleLocalInventoryReady;
            ClientWorldSceneStreamer.LocalWorldSceneReady += HandleLocalWorldSceneReady;
            ClientWorldSceneStreamer.LocalWorldSceneCleared += HandleLocalWorldSceneCleared;
            ClientWorldSceneState.EnsureSubscribed();
            ClientWorldSceneState.StateChanged += HandleClientWorldSceneStateChanged;

            ResolveReferences();
            BindAvailableStateMirrors();
            ScheduleFullEvaluation();
        }

        private void OnDisable()
        {
            ClientInventoryState.LocalInventoryReady -= HandleLocalInventoryReady;
            ClientWorldSceneStreamer.LocalWorldSceneReady -= HandleLocalWorldSceneReady;
            ClientWorldSceneStreamer.LocalWorldSceneCleared -= HandleLocalWorldSceneCleared;
            ClientWorldSceneState.StateChanged -= HandleClientWorldSceneStateChanged;

            SubscribeInventoryState(null);
            SubscribeProgressFlagState(null);
            SubscribeConditionState(null);
        }

        private void Update()
        {
            float now = Time.realtimeSinceStartup;

            if (now >= _nextBindingSearchTime)
            {
                _nextBindingSearchTime = now + Mathf.Max(0.05f, bindingSearchIntervalSeconds);
                ResolveReferences();
                BindAvailableStateMirrors();
            }

            if (!IsReadyForPromptEvaluation())
            {
                return;
            }

            if (!_hasDoneInitialEvaluation)
            {
                _hasDoneInitialEvaluation = true;
                EvaluatePrompts(InteractionRuleDependencyFlags.All);
            }

            if (now >= _nextFallbackEvaluationTime)
            {
                _nextFallbackEvaluationTime = now + Mathf.Max(0.05f, fallbackEvaluationIntervalSeconds);
                EvaluatePrompts(InteractionRuleDependencyFlags.All);
            }

            if (now >= _nextReminderTickTime)
            {
                _nextReminderTickTime = now + Mathf.Max(0.1f, reminderTickIntervalSeconds);
                EvaluateReminderPrompts();
            }
        }

        public void EvaluateAllNow()
        {
            EvaluatePrompts(InteractionRuleDependencyFlags.All);
        }

        private bool IsReadyForPromptEvaluation()
        {
            if (Time.realtimeSinceStartup < _notBeforeRealtime)
            {
                return false;
            }

            if (promptController == null || promptCatalog == null)
            {
                return false;
            }

            if (requireLocalWorldSceneReady && !ClientWorldSceneState.HasCurrentWorldScene)
            {
                return false;
            }

            return NetworkManager.Singleton == null || NetworkManager.Singleton.IsListening;
        }

        private void ResolveReferences()
        {
            if (promptController == null)
            {
                promptController = GetComponent<PlayerPromptController>();
            }

            if (promptController == null)
            {
                promptController = FindFirstObjectByType<PlayerPromptController>();
            }

            if (promptCatalog == null && promptController != null)
            {
                promptCatalog = promptController.PromptCatalog;
            }
        }

        private void BindAvailableStateMirrors()
        {
            SubscribeInventoryState(ClientInventoryState.Local);
            SubscribeProgressFlagState(ClientProgressFlagState.Local);
            SubscribeConditionState(NetworkPlayerConditionState.Local);
        }

        private void ScheduleFullEvaluation()
        {
            _hasDoneInitialEvaluation = false;
            _nextFallbackEvaluationTime = Time.realtimeSinceStartup + Mathf.Max(0.05f, fallbackEvaluationIntervalSeconds);
            _nextReminderTickTime = Time.realtimeSinceStartup + Mathf.Max(0.1f, reminderTickIntervalSeconds);
        }

        private void EvaluatePrompts(InteractionRuleDependencyFlags changedDependencies)
        {
            if (!IsReadyForPromptEvaluation() || promptCatalog.Prompts == null)
            {
                return;
            }

            for (int i = 0; i < promptCatalog.Prompts.Count; i++)
            {
                PromptDefinition prompt = promptCatalog.Prompts[i];
                if (prompt == null || !prompt.ShouldEvaluateFor(changedDependencies))
                {
                    continue;
                }

                EvaluatePrompt(prompt, allowInitialQueue: true, allowReminderQueue: false);
            }
        }

        private void EvaluateReminderPrompts()
        {
            if (!IsReadyForPromptEvaluation() || promptCatalog.Prompts == null)
            {
                return;
            }

            for (int i = 0; i < promptCatalog.Prompts.Count; i++)
            {
                PromptDefinition prompt = promptCatalog.Prompts[i];
                if (prompt == null || !prompt.EnableReminders)
                {
                    continue;
                }

                EvaluatePrompt(prompt, allowInitialQueue: false, allowReminderQueue: true);
            }
        }

        private void EvaluatePrompt(
            PromptDefinition prompt,
            bool allowInitialQueue,
            bool allowReminderQueue)
        {
            if (prompt == null)
            {
                return;
            }

            string promptId = NormalizeId(prompt.PromptId);
            if (string.IsNullOrWhiteSpace(promptId))
            {
                return;
            }

            PromptRuntimeState runtime = GetOrCreateRuntimeState(promptId);
            bool eligible = IsPromptEligible(prompt);
            float now = Time.realtimeSinceStartup;

            if (!eligible)
            {
                if (runtime.IsEligible)
                {
                    runtime.ResetEligibilityWindow();

                    if (verboseLogging || prompt.VerboseLogging)
                    {
                        Debug.Log($"[PromptStateController] Prompt '{promptId}' became ineligible.", this);
                    }
                }

                return;
            }

            if (!runtime.IsEligible)
            {
                runtime.BeginEligibilityWindow(now, prompt.FirstReminderDelaySeconds);

                if (verboseLogging || prompt.VerboseLogging)
                {
                    Debug.Log($"[PromptStateController] Prompt '{promptId}' became eligible.", this);
                }
            }

            if (allowInitialQueue && !runtime.InitialQueuedThisWindow && prompt.HasUsableText(PromptLineKind.Initial))
            {
                bool queued = promptController.TryShowPrompt(
                    prompt,
                    PromptLineKind.Initial,
                    force: false,
                    bypassRepeat: false);

                if (queued)
                {
                    runtime.MarkInitialQueued(now, prompt.FirstReminderDelaySeconds);
                }
            }

            if (!allowReminderQueue || !prompt.EnableReminders || !runtime.InitialQueuedThisWindow)
            {
                return;
            }

            if (prompt.MaxRemindersPerEligibilityWindow > 0 &&
                runtime.RemindersQueuedThisWindow >= prompt.MaxRemindersPerEligibilityWindow)
            {
                return;
            }

            if (now < runtime.NextReminderRealtime)
            {
                return;
            }

            bool reminderQueued = promptController.TryShowPrompt(
                prompt,
                PromptLineKind.Reminder,
                force: false,
                bypassRepeat: true);

            if (reminderQueued)
            {
                runtime.MarkReminderQueued(now, prompt.ReminderIntervalSeconds);
            }
            else
            {
                // Avoid hammering the queue if another prompt/cooldown blocks this reminder.
                runtime.NextReminderRealtime = now + Mathf.Min(5f, Mathf.Max(0.5f, prompt.ReminderIntervalSeconds * 0.25f));
            }
        }

        private bool IsPromptEligible(PromptDefinition prompt)
        {
            if (prompt == null)
            {
                return false;
            }

            if (prompt.EligibilityRules == null)
            {
                return true;
            }

            InteractionContext context = CreateClientPreviewContext();
            InteractionRuleResult result = prompt.EligibilityRules.EvaluateClientPreview(context);
            return result.Passed;
        }

        private static InteractionContext CreateClientPreviewContext()
        {
            ulong clientId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0UL;
            return InteractionContext.CreateClientPreview(
                clientId,
                targetObject: null,
                target: null,
                executor: null,
                targetInstanceObject: null);
        }

        private PromptRuntimeState GetOrCreateRuntimeState(string promptId)
        {
            string normalizedPromptId = NormalizeId(promptId);

            if (!_runtimeByPromptId.TryGetValue(normalizedPromptId, out PromptRuntimeState runtime) || runtime == null)
            {
                runtime = new PromptRuntimeState();
                _runtimeByPromptId[normalizedPromptId] = runtime;
            }

            return runtime;
        }

        private void SubscribeInventoryState(ClientInventoryState state)
        {
            if (_inventoryState == state)
            {
                return;
            }

            if (_inventoryState != null)
            {
                _inventoryState.InventorySnapshotChanged -= HandleInventorySnapshotChanged;
            }

            _inventoryState = state;

            if (_inventoryState != null)
            {
                _inventoryState.InventorySnapshotChanged += HandleInventorySnapshotChanged;
                EvaluatePrompts(InteractionRuleDependencyFlags.Inventory | InteractionRuleDependencyFlags.Equipment);
            }
        }

        private void SubscribeProgressFlagState(ClientProgressFlagState state)
        {
            if (_progressFlagState == state)
            {
                return;
            }

            if (_progressFlagState != null)
            {
                _progressFlagState.FlagSet -= HandleProgressFlagSet;
                _progressFlagState.FlagRemoved -= HandleProgressFlagRemoved;
                _progressFlagState.PrefixCleared -= HandleProgressFlagPrefixCleared;
            }

            _progressFlagState = state;

            if (_progressFlagState != null)
            {
                _progressFlagState.FlagSet += HandleProgressFlagSet;
                _progressFlagState.FlagRemoved += HandleProgressFlagRemoved;
                _progressFlagState.PrefixCleared += HandleProgressFlagPrefixCleared;
                EvaluatePrompts(InteractionRuleDependencyFlags.ProgressFlags);
            }
        }

        private void SubscribeConditionState(NetworkPlayerConditionState state)
        {
            if (_conditionState == state)
            {
                return;
            }

            if (_conditionState != null)
            {
                _conditionState.ControlBlocks.OnValueChanged -= HandleControlBlocksChanged;
                _conditionState.IsAnchored.OnValueChanged -= HandleAnchoredChanged;
            }

            _conditionState = state;

            if (_conditionState != null)
            {
                _conditionState.ControlBlocks.OnValueChanged += HandleControlBlocksChanged;
                _conditionState.IsAnchored.OnValueChanged += HandleAnchoredChanged;
                EvaluatePrompts(InteractionRuleDependencyFlags.Conditions);
            }
        }

        private void HandleLocalInventoryReady(ClientInventoryState state)
        {
            SubscribeInventoryState(state);
            EvaluatePrompts(InteractionRuleDependencyFlags.Inventory | InteractionRuleDependencyFlags.Equipment);
        }

        private void HandleInventorySnapshotChanged()
        {
            EvaluatePrompts(InteractionRuleDependencyFlags.Inventory | InteractionRuleDependencyFlags.Equipment);
        }

        private void HandleProgressFlagSet(string flagId, ProgressFlagLifetime lifetime)
        {
            EvaluatePrompts(InteractionRuleDependencyFlags.ProgressFlags);
        }

        private void HandleProgressFlagRemoved(string flagId)
        {
            EvaluatePrompts(InteractionRuleDependencyFlags.ProgressFlags);
        }

        private void HandleProgressFlagPrefixCleared(string prefix)
        {
            EvaluatePrompts(InteractionRuleDependencyFlags.ProgressFlags);
        }

        private void HandleControlBlocksChanged(PlayerControlBlockFlags previousValue, PlayerControlBlockFlags newValue)
        {
            EvaluatePrompts(InteractionRuleDependencyFlags.Conditions);
        }

        private void HandleAnchoredChanged(bool previousValue, bool newValue)
        {
            EvaluatePrompts(InteractionRuleDependencyFlags.Conditions);
        }

        private void HandleLocalWorldSceneReady(string sceneId, string instanceId)
        {
            ClientWorldSceneState.SetLocalWorldScene(sceneId, instanceId);
            EvaluatePrompts(InteractionRuleDependencyFlags.SceneOrInstance);
        }

        private void HandleLocalWorldSceneCleared()
        {
            ClientWorldSceneState.ClearLocalWorldScene();
            EvaluatePrompts(InteractionRuleDependencyFlags.SceneOrInstance);
        }

        private void HandleClientWorldSceneStateChanged()
        {
            EvaluatePrompts(InteractionRuleDependencyFlags.SceneOrInstance);
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private sealed class PromptRuntimeState
        {
            public bool IsEligible { get; private set; }
            public bool InitialQueuedThisWindow { get; private set; }
            public int RemindersQueuedThisWindow { get; private set; }
            public float NextReminderRealtime { get; set; }

            public void BeginEligibilityWindow(float now, float firstReminderDelaySeconds)
            {
                IsEligible = true;
                InitialQueuedThisWindow = false;
                RemindersQueuedThisWindow = 0;
                NextReminderRealtime = now + Mathf.Max(0.5f, firstReminderDelaySeconds);
            }

            public void ResetEligibilityWindow()
            {
                IsEligible = false;
                InitialQueuedThisWindow = false;
                RemindersQueuedThisWindow = 0;
                NextReminderRealtime = 0f;
            }

            public void MarkInitialQueued(float now, float firstReminderDelaySeconds)
            {
                InitialQueuedThisWindow = true;
                RemindersQueuedThisWindow = 0;
                NextReminderRealtime = now + Mathf.Max(0.5f, firstReminderDelaySeconds);
            }

            public void MarkReminderQueued(float now, float reminderIntervalSeconds)
            {
                RemindersQueuedThisWindow++;
                NextReminderRealtime = now + Mathf.Max(0.5f, reminderIntervalSeconds);
            }
        }
    }
}
