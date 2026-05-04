using System.Collections.Generic;

namespace ROC.Game.ProgressFlags
{
    public interface IProgressFlagRepository
    {
        IReadOnlyList<ProgressFlagRecord> GetFlags(string characterId);

        bool HasFlag(string characterId, string flagId);

        bool SetFlag(
            string characterId,
            string flagId,
            ProgressFlagLifetime lifetime,
            string source);

        bool RemoveFlag(string characterId, string flagId);

        int ClearFlagsWithPrefix(string characterId, string prefix);
    }
}