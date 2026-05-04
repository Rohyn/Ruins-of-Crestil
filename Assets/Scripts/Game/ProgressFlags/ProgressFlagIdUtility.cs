using ROC.Infrastructure.Ids;

namespace ROC.Game.ProgressFlags
{
    public static class ProgressFlagIdUtility
    {
        public static string NormalizeFlagId(string flagId)
        {
            return StableIdUtility.Normalize(flagId);
        }

        public static string NormalizePrefix(string prefix)
        {
            string normalized = StableIdUtility.Normalize(prefix);

            if (!normalized.EndsWith("."))
            {
                normalized += ".";
            }

            return normalized;
        }

        public static bool IsValidFlagId(string flagId)
        {
            if (string.IsNullOrWhiteSpace(flagId))
            {
                return false;
            }

            string normalized = NormalizeFlagId(flagId);

            if (!StableIdUtility.IsValid(normalized))
            {
                return false;
            }

            int dotIndex = normalized.IndexOf('.');

            return dotIndex > 0 && dotIndex < normalized.Length - 1;
        }

        public static bool IsValidPrefix(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                return false;
            }

            string normalized = NormalizePrefix(prefix);

            if (!StableIdUtility.IsValid(normalized.TrimEnd('.')))
            {
                return false;
            }

            return normalized.Length > 1;
        }

        public static bool HasPrefix(string flagId, string prefix)
        {
            string normalizedFlag = NormalizeFlagId(flagId);
            string normalizedPrefix = NormalizePrefix(prefix);

            return normalizedFlag.StartsWith(normalizedPrefix);
        }
    }
}