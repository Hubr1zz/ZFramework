using System;
using System.Collections.Generic;

namespace HuntingInDarkness.GameCore.Foundation
{
    /// <summary>规则层使用的稳定关键词比较；内容来源负责提供关键词集合。</summary>
    public static class KeywordRules
    {
        public static string Normalize(string keyword)
        {
            return string.IsNullOrWhiteSpace(keyword) ? string.Empty : keyword.Trim().ToLowerInvariant();
        }

        public static bool Contains(IReadOnlyCollection<string> keywords, string expected)
        {
            string normalizedExpected = Normalize(expected);
            if (keywords == null || normalizedExpected.Length == 0) return false;
            foreach (string keyword in keywords)
                if (string.Equals(Normalize(keyword), normalizedExpected, StringComparison.Ordinal))
                    return true;
            return false;
        }

        public static bool TryAdd(ISet<string> keywords, string value)
        {
            if (keywords == null) throw new ArgumentNullException(nameof(keywords));
            string normalized = Normalize(value);
            return normalized.Length > 0 && keywords.Add(normalized);
        }
    }
}
