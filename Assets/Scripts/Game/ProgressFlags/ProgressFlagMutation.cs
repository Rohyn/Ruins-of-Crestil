using System;
using UnityEngine;

namespace ROC.Game.ProgressFlags
{
    [Serializable]
    public struct ProgressFlagMutation
    {
        [SerializeField] private ProgressFlagMutationKind mutationKind;

        [Tooltip("For SetFlag/RemoveFlag, use a full flag ID. For ClearPrefix, use a prefix such as intro.")]
        [SerializeField] private string flagIdOrPrefix;

        [SerializeField] private ProgressFlagLifetime lifetime;

        [Tooltip("Optional source label for debugging/persistence, such as intro.door or ritual1.star_altar.")]
        [SerializeField] private string source;

        public ProgressFlagMutationKind MutationKind => mutationKind;
        public string FlagIdOrPrefix => flagIdOrPrefix;
        public ProgressFlagLifetime Lifetime => lifetime;
        public string Source => source;
    }
}