using System.Collections.Generic;
using UnityEngine;

namespace ROC.Game.Conditions
{
    [CreateAssetMenu(
        fileName = "ConditionCatalog",
        menuName = "ROC/Conditions/Condition Catalog")]
    public sealed class ConditionCatalog : ScriptableObject
    {
        [SerializeField] private List<ConditionDefinition> conditions = new();

        private readonly Dictionary<string, ConditionDefinition> _byId = new();

        private void OnEnable()
        {
            RebuildLookup();
        }

        public bool TryGet(string conditionId, out ConditionDefinition definition)
        {
            if (_byId.Count == 0)
            {
                RebuildLookup();
            }

            if (string.IsNullOrWhiteSpace(conditionId))
            {
                definition = null;
                return false;
            }

            return _byId.TryGetValue(conditionId, out definition);
        }

        private void RebuildLookup()
        {
            _byId.Clear();

            for (int i = 0; i < conditions.Count; i++)
            {
                ConditionDefinition condition = conditions[i];

                if (condition == null || !condition.IsValid())
                {
                    continue;
                }

                if (_byId.ContainsKey(condition.ConditionId))
                {
                    Debug.LogWarning($"[ConditionCatalog] Duplicate condition ignored: {condition.ConditionId}");
                    continue;
                }

                _byId.Add(condition.ConditionId, condition);
            }
        }
    }
}