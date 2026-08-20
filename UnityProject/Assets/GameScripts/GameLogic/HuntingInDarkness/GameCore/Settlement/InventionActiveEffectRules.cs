using System;
using System.Collections.Generic;

namespace HuntingInDarkness.GameCore.Settlement
{
    [Serializable]
    public sealed class InventionActiveEffectUsage
    {
        public string EffectId;
        public int Year;
        public int UseCount;
    }

    /// <summary>发明主动效果的年度次数规则；不持有事件、Runner 或场景对象。</summary>
    public static class InventionActiveEffectRules
    {
        public static bool CanActivate(bool inventionUnlocked, int currentYear, string effectId, string eventId, int maxUsesPerYear, IReadOnlyList<InventionActiveEffectUsage> usage, bool eventAvailable, out string reason)
        {
            if (!inventionUnlocked)
            {
                reason = "尚未掌握该发明。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(effectId) || string.IsNullOrWhiteSpace(eventId) || maxUsesPerYear < 0)
            {
                reason = "主动效果内容无效。";
                return false;
            }
            if (!eventAvailable)
            {
                reason = "主动效果对应的事件内容不可用。";
                return false;
            }
            if (maxUsesPerYear == 0)
            {
                reason = string.Empty;
                return true;
            }

            int used = GetUseCount(usage, effectId, currentYear);
            if (used < maxUsesPerYear)
            {
                reason = string.Empty;
                return true;
            }
            reason = $"本年第 {maxUsesPerYear} 次使用机会已经耗尽。";
            return false;
        }

        public static int GetUseCount(IReadOnlyList<InventionActiveEffectUsage> usage, string effectId, int year)
        {
            if (usage == null || string.IsNullOrWhiteSpace(effectId))
                return 0;
            int total = 0;
            foreach (InventionActiveEffectUsage state in usage)
            {
                if (state == null || state.Year != year || !string.Equals(state.EffectId, effectId, StringComparison.Ordinal) || state.UseCount <= 0)
                    continue;
                total = (int)Math.Min(int.MaxValue, (long)total + state.UseCount);
            }
            return total;
        }

        public static void RecordUse(List<InventionActiveEffectUsage> usage, string effectId, int year)
        {
            if (usage == null)
                throw new ArgumentNullException(nameof(usage));
            string normalizedId = effectId?.Trim() ?? string.Empty;
            if (normalizedId.Length == 0)
                throw new ArgumentException("效果 ID 不能为空。", nameof(effectId));
            InventionActiveEffectUsage state = usage.Find(candidate => candidate != null && candidate.Year == year && string.Equals(candidate.EffectId, normalizedId, StringComparison.Ordinal));
            if (state == null)
            {
                usage.Add(new InventionActiveEffectUsage { EffectId = normalizedId, Year = year, UseCount = 1 });
                return;
            }
            state.UseCount = state.UseCount == int.MaxValue ? int.MaxValue : Math.Max(0, state.UseCount) + 1;
        }
    }
}
