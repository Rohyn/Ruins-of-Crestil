using System;

namespace ROC.Game.ProgressFlags
{
    [Serializable]
    public sealed class ProgressFlagRecord
    {
        public string FlagId;
        public ProgressFlagLifetime Lifetime;
        public string Source;
        public string SetUtc;
    }
}