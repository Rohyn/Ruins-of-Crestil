using System;
using UnityEngine;

namespace ROC.Game.ProgressFlags
{
    [Serializable]
    public struct ProgressFlagRequirement
    {
        [SerializeField] private ProgressFlagRequirementKind requirementKind;
        [SerializeField] private string flagId;
        [SerializeField] private string failureMessage;

        public ProgressFlagRequirementKind RequirementKind => requirementKind;
        public string FlagId => flagId;
        public string FailureMessage => failureMessage;
    }
}