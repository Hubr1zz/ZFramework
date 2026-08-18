using System;

namespace HuntingInDarkness.GameCore.Hunt
{
    /// <summary>狩猎目的地只负责长期可用性，不承担 Unity 内容资产或阶段切换。</summary>
    public static class HuntDestinationRules
    {
        public static bool CanSelect(string destinationId, string displayName, int currentYear, int minimumYear, out string reason)
        {
            if (string.IsNullOrWhiteSpace(destinationId) || string.IsNullOrWhiteSpace(displayName))
            {
                reason = "这个目的地的记录不完整。";
                return false;
            }

            int safeCurrentYear = Math.Max(1, currentYear);
            int safeMinimumYear = Math.Max(1, minimumYear);
            if (safeCurrentYear < safeMinimumYear)
            {
                reason = $"第 {safeMinimumYear} 年后才能前往。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
