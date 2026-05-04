using System.Collections.Generic;
using UnityEngine;

namespace ROC.Game.Conditions
{
    [CreateAssetMenu(
        fileName = "ConditionDefinition",
        menuName = "ROC/Conditions/Condition Definition")]
    public sealed class ConditionDefinition : ScriptableObject
    {
        [Header("Stable ID")]
        [SerializeField] private string conditionId;

        [Header("Display")]
        [SerializeField] private string displayName;
        [TextArea]
        [SerializeField] private string description;
        [SerializeField] private ConditionPolarity polarity = ConditionPolarity.Neutral;
        [SerializeField] private ConditionDisplayCategory displayCategory = ConditionDisplayCategory.Utility;
        [SerializeField] private List<string> tags = new();

        [Header("Duration")]
        [SerializeField] private ConditionDurationMode durationMode = ConditionDurationMode.IndefiniteUntilRemoved;
        [SerializeField, Min(0f)] private float baseDurationSeconds;

        [Header("Effects")]
        [SerializeField] private List<ConditionEffectDefinition> effects = new();

        public string ConditionId => conditionId;
        public string DisplayName => displayName;
        public string Description => description;
        public ConditionPolarity Polarity => polarity;
        public ConditionDisplayCategory DisplayCategory => displayCategory;
        public IReadOnlyList<string> Tags => tags;
        public ConditionDurationMode DurationMode => durationMode;
        public float BaseDurationSeconds => baseDurationSeconds;
        public IReadOnlyList<ConditionEffectDefinition> Effects => effects;

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(conditionId);
        }
    }
}