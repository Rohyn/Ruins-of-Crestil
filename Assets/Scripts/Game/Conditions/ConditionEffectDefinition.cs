using System;
using UnityEngine;

namespace ROC.Game.Conditions
{
    [Serializable]
    public struct ConditionEffectDefinition
    {
        [SerializeField] private ConditionEffectType effectType;
        [SerializeField] private float magnitude;

        public ConditionEffectType EffectType => effectType;
        public float Magnitude => magnitude;
    }
}