using ROC.Networking.Interactions.Data;
using UnityEngine;

namespace ROC.Presentation.Prompts
{
    /// <summary>
    /// Data asset for one player-facing prompt/bark. Prompts are presentation guidance only;
    /// gameplay truth remains in flags, inventory, conditions, quests, interactions, etc.
    /// </summary>
    [CreateAssetMenu(
        fileName = "PromptDefinition",
        menuName = "ROC/Prompts/Prompt Definition")]
    public sealed class PromptDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string promptId = "prompt.new";
        [SerializeField] private PromptChannel channel = PromptChannel.General;

        [Header("Speaker / Text")]
        [SerializeField] private string speakerName = "Aidan";

        [TextArea(2, 6)]
        [SerializeField] private string initialText = "Prompt text.";

        [Tooltip("Optional reminder text used while this prompt remains eligible. If empty, the initial text is reused.")]
        [TextArea(2, 6)]
        [SerializeField] private string reminderText;

        [Header("Rules")]
        [Tooltip("Optional client-preview rule set. The prompt is eligible while these rules pass.")]
        [SerializeField] private InteractionRuleSetDefinition eligibilityRules;

        [Tooltip("Local state changes that should cause this prompt to re-evaluate. If empty, the prompt only evaluates during all-prompts refreshes and reminders.")]
        [SerializeField] private InteractionRuleDependencyFlags evaluationDependencies =
            InteractionRuleDependencyFlags.ProgressFlags |
            InteractionRuleDependencyFlags.Inventory |
            InteractionRuleDependencyFlags.Equipment |
            InteractionRuleDependencyFlags.Conditions |
            InteractionRuleDependencyFlags.SceneOrInstance;

        [Header("Queue / Display")]
        [SerializeField] private int priority = 300;
        [SerializeField] private int reminderPriorityOffset = -100;
        [SerializeField, Min(0.25f)] private float displaySeconds = 4f;
        [SerializeField, Min(0f)] private float cooldownSeconds = 0f;
        [SerializeField] private PromptRepeatMode repeatMode = PromptRepeatMode.CooldownOnly;

        [Header("Reminder")]
        [SerializeField] private bool enableReminders = true;
        [SerializeField, Min(0.5f)] private float firstReminderDelaySeconds = 20f;
        [SerializeField, Min(0.5f)] private float reminderIntervalSeconds = 30f;
        [SerializeField] private int maxRemindersPerEligibilityWindow = 0;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = false;

        public string PromptId => promptId;
        public PromptChannel Channel => channel;
        public string SpeakerName => speakerName;
        public string InitialText => initialText;
        public string ReminderText => string.IsNullOrWhiteSpace(reminderText) ? initialText : reminderText;
        public InteractionRuleSetDefinition EligibilityRules => eligibilityRules;
        public InteractionRuleDependencyFlags EvaluationDependencies => evaluationDependencies;
        public int Priority => priority;
        public int ReminderPriority => priority + reminderPriorityOffset;
        public float DisplaySeconds => Mathf.Max(0.25f, displaySeconds);
        public float CooldownSeconds => Mathf.Max(0f, cooldownSeconds);
        public PromptRepeatMode RepeatMode => repeatMode;
        public bool EnableReminders => enableReminders;
        public float FirstReminderDelaySeconds => Mathf.Max(0.5f, firstReminderDelaySeconds);
        public float ReminderIntervalSeconds => Mathf.Max(0.5f, reminderIntervalSeconds);
        public int MaxRemindersPerEligibilityWindow => maxRemindersPerEligibilityWindow;
        public bool VerboseLogging => verboseLogging;

        public bool HasUsableText(PromptLineKind lineKind)
        {
            return !string.IsNullOrWhiteSpace(GetText(lineKind));
        }

        public string GetText(PromptLineKind lineKind)
        {
            return lineKind == PromptLineKind.Reminder ? ReminderText : InitialText;
        }

        public bool ShouldEvaluateFor(InteractionRuleDependencyFlags changedDependencies)
        {
            if (changedDependencies == InteractionRuleDependencyFlags.All)
            {
                return true;
            }

            if (evaluationDependencies == InteractionRuleDependencyFlags.None)
            {
                return false;
            }

            return (evaluationDependencies & changedDependencies) != 0;
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(promptId))
            {
                promptId = name;
            }

            if (displaySeconds < 0.25f)
            {
                displaySeconds = 0.25f;
            }

            if (cooldownSeconds < 0f)
            {
                cooldownSeconds = 0f;
            }

            if (firstReminderDelaySeconds < 0.5f)
            {
                firstReminderDelaySeconds = 0.5f;
            }

            if (reminderIntervalSeconds < 0.5f)
            {
                reminderIntervalSeconds = 0.5f;
            }
        }
    }
}
