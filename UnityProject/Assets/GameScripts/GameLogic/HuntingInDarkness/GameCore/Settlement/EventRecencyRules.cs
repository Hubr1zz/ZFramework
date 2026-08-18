using System;

namespace HuntingInDarkness.GameCore.Settlement
{
    /// <summary>年度事件的短期重复保护，不依赖具体内容资产。</summary>
    public static class EventRecencyRules
    {
        public static bool ShouldExcludeMostRecent(string candidateId, string mostRecentEventId, bool hasAlternative)
        {
            if (!hasAlternative || string.IsNullOrWhiteSpace(mostRecentEventId)) return false;
            return string.Equals(candidateId, mostRecentEventId, StringComparison.Ordinal);
        }
    }
}
