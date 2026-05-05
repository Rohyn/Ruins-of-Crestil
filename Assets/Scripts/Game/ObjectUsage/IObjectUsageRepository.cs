namespace ROC.Game.ObjectUsage
{
    public interface IObjectUsageRepository
    {
        bool TryGetRecord(string usageKey, out ObjectUsageRecord record);

        bool HasCharacterUse(string usageKey, string characterId);

        int GetCharacterUseCount(string usageKey, string characterId);

        int GetGlobalUseCount(string usageKey);

        bool RecordUse(
            string usageKey,
            string sceneId,
            string instanceId,
            string stableObjectId,
            string cleanupGroup,
            string characterId,
            bool recordCharacterUse,
            bool incrementGlobalUse,
            string source);

        bool ResetCharacterUses(string usageKey);

        bool ResetGlobalUses(string usageKey);

        bool DeleteUsage(string usageKey);

        int DeleteUsagesByCleanupGroup(string cleanupGroup);

        int DeleteUsagesByInstance(string instanceId);
    }
}
