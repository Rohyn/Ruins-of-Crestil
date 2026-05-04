using System.Text;

namespace ROC.Infrastructure.Ids
{
    public static class StableIdUtility
    {
        public static bool IsValid(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];

                bool valid =
                    (c >= 'a' && c <= 'z') ||
                    (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') ||
                    c == '_' ||
                    c == '-' ||
                    c == '.';

                if (!valid)
                {
                    return false;
                }
            }

            return true;
        }

        public static string Normalize(string value, string fallback = "unknown")
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            value = value.Trim().ToLowerInvariant();

            var builder = new StringBuilder(value.Length);

            bool lastWasSeparator = false;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];

                bool validLetterOrDigit =
                    (c >= 'a' && c <= 'z') ||
                    (c >= '0' && c <= '9');

                bool validSeparator = c == '_' || c == '-' || c == '.';

                if (validLetterOrDigit)
                {
                    builder.Append(c);
                    lastWasSeparator = false;
                }
                else if (validSeparator)
                {
                    if (!lastWasSeparator && builder.Length > 0)
                    {
                        builder.Append(c);
                        lastWasSeparator = true;
                    }
                }
                else
                {
                    if (!lastWasSeparator && builder.Length > 0)
                    {
                        builder.Append('-');
                        lastWasSeparator = true;
                    }
                }
            }

            string normalized = builder.ToString().Trim('-', '_', '.');
            return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
        }

        public static string Combine(string first, string second)
        {
            return $"{Normalize(first)}.{Normalize(second)}";
        }

        public static string Combine(string first, string second, string third)
        {
            return $"{Normalize(first)}.{Normalize(second)}.{Normalize(third)}";
        }
    }
}