using System;
using System.Collections.Generic;

namespace ROC.Game.ObjectUsage
{
    [Serializable]
    public sealed class ObjectUsageRecord
    {
        public string UsageKey;
        public string SceneId;
        public string InstanceId;
        public string StableObjectId;
        public string CleanupGroup;
        public string CreatedUtc;
        public string UpdatedUtc;
        public int GlobalUseCount;
        public List<ObjectUsageCharacterUseRecord> CharacterUses = new();
    }

    [Serializable]
    public sealed class ObjectUsageCharacterUseRecord
    {
        public string CharacterId;
        public int UseCount;
        public string FirstUsedUtc;
        public string LastUsedUtc;
        public string Source;
    }
}
