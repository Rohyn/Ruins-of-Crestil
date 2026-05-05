using System.Text;

namespace ROC.Game.ObjectUsage
{
    public static class ObjectUsageKeyUtility
    {
        public static string NormalizePart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            value = value.Trim();
            var builder = new StringBuilder(value.Length);

            for (int i = 0; i < value.Length; i++)
            {
                char c = char.ToLowerInvariant(value[i]);
                if ((c >= 'a' && c <= 'z') ||
                    (c >= '0' && c <= '9') ||
                    c == '.' ||
                    c == '_' ||
                    c == '-')
                {
                    builder.Append(c);
                }
                else if (char.IsWhiteSpace(c) || c == '/' || c == '\\' || c == ':' || c == '|')
                {
                    builder.Append('_');
                }
            }

            return builder.ToString().Trim('_');
        }

        public static string NormalizeKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            value = value.Trim();
            var builder = new StringBuilder(value.Length);

            for (int i = 0; i < value.Length; i++)
            {
                char c = char.ToLowerInvariant(value[i]);
                if ((c >= 'a' && c <= 'z') ||
                    (c >= '0' && c <= '9') ||
                    c == '.' ||
                    c == '_' ||
                    c == '-' ||
                    c == ':' )
                {
                    builder.Append(c);
                }
                else if (char.IsWhiteSpace(c) || c == '/' || c == '\\' || c == '|')
                {
                    builder.Append('_');
                }
            }

            return builder.ToString().Trim('_', ':');
        }

        public static bool IsValidKey(string value)
        {
            return !string.IsNullOrWhiteSpace(NormalizeKey(value));
        }

        public static string BuildKey(
            ObjectUsageKeyScope scope,
            string explicitOverride,
            string sceneId,
            string instanceId,
            string stableObjectId)
        {
            string normalizedOverride = NormalizeKey(explicitOverride);
            if (!string.IsNullOrWhiteSpace(normalizedOverride))
            {
                return normalizedOverride;
            }

            string normalizedSceneId = NormalizePart(sceneId);
            string normalizedInstanceId = NormalizePart(instanceId);
            string normalizedStableObjectId = NormalizePart(stableObjectId);

            if (string.IsNullOrWhiteSpace(normalizedStableObjectId))
            {
                return string.Empty;
            }

            switch (scope)
            {
                case ObjectUsageKeyScope.StableObjectOnly:
                    return $"object:{normalizedStableObjectId}";

                case ObjectUsageKeyScope.SceneAndStableObject:
                    if (string.IsNullOrWhiteSpace(normalizedSceneId))
                    {
                        return string.Empty;
                    }
                    return $"scene:{normalizedSceneId}:object:{normalizedStableObjectId}";

                case ObjectUsageKeyScope.InstanceAndStableObject:
                    if (string.IsNullOrWhiteSpace(normalizedInstanceId))
                    {
                        return string.Empty;
                    }
                    return $"instance:{normalizedInstanceId}:object:{normalizedStableObjectId}";

                case ObjectUsageKeyScope.CustomOnly:
                default:
                    return string.Empty;
            }
        }
    }
}
