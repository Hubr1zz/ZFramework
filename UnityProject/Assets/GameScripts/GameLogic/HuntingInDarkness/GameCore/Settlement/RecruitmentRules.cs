using System;
using System.Collections.Generic;

namespace HuntingInDarkness.GameCore.Settlement
{
    /// <summary>不依赖 Unity 的营地招募约束。</summary>
    public static class RecruitmentRules
    {
        public const int MaximumNameLength = 12;

        public static int GetCost(int aliveCount, int configuredCost) => aliveCount <= 0 ? 0 : Math.Max(0, configuredCost);

        public static int GetPopulationCost(int aliveCount, int configuredCost) => aliveCount <= 1 ? 0 : Math.Max(0, configuredCost);

        public static bool CanRecruit(int currentYear, int lastRecruitmentYear, int aliveCount, int maximumLivingHunters, int availableResource, int configuredCost, out string reason)
            => CanRecruit(currentYear, lastRecruitmentYear, aliveCount, maximumLivingHunters, availableResource, configuredCost, int.MaxValue, 0, out reason);

        public static bool CanRecruit(int currentYear, int lastRecruitmentYear, int aliveCount, int maximumLivingHunters, int availableResource, int configuredCost, int availablePopulation, int configuredPopulationCost, out string reason)
        {
            int safeAliveCount = Math.Max(0, aliveCount);
            if (safeAliveCount >= Math.Max(1, maximumLivingHunters))
            {
                reason = "营地已经没有容纳新猎人的位置。";
                return false;
            }

            if (safeAliveCount == 0)
            {
                reason = string.Empty;
                return true;
            }

            if (lastRecruitmentYear == currentYear)
            {
                reason = "本年已经接纳过一名新猎人。";
                return false;
            }

            int cost = GetCost(safeAliveCount, configuredCost);
            if (Math.Max(0, availableResource) < cost)
            {
                reason = "营地缺少接纳新人的口粮。";
                return false;
            }

            int populationCost = GetPopulationCost(safeAliveCount, configuredPopulationCost);
            if (Math.Max(0, availablePopulation) < populationCost)
            {
                reason = "营地缺少可供接纳新人的人口。";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public static bool TryNormalizeName(string input, IEnumerable<string> existingNames, out string normalizedName, out string reason)
        {
            normalizedName = input?.Trim() ?? string.Empty;
            if (normalizedName.Length == 0)
            {
                reason = "请先为新猎人取名。";
                return false;
            }

            if (normalizedName.Length > MaximumNameLength)
            {
                reason = $"名字不能超过 {MaximumNameLength} 个字符。";
                return false;
            }
            foreach (char character in normalizedName)
                if (char.IsControl(character))
                {
                    reason = "名字不能包含换行或控制字符。";
                    return false;
                }

            if (existingNames != null)
            {
                foreach (string existingName in existingNames)
                {
                    if (!string.Equals(existingName?.Trim(), normalizedName, StringComparison.OrdinalIgnoreCase)) continue;
                    reason = "这个名字已经属于营地的一段记忆。";
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }
    }

    /// <summary>为存档中的猎人选择稳定且不冲突的运行时 ID。</summary>
    public static class HunterIdentityRules
    {
        public const int FirstHunterId = 100;

        public static int NextAvailableId(IEnumerable<HunterState> hunters)
        {
            var usedIds = new HashSet<int>();
            if (hunters != null)
                foreach (HunterState hunter in hunters)
                    if (hunter != null)
                        usedIds.Add(hunter.InstanceId);

            int candidate = FirstHunterId;
            while (usedIds.Contains(candidate))
            {
                if (candidate == int.MaxValue) throw new InvalidOperationException("猎人身份编号已经耗尽。");
                candidate++;
            }
            return candidate;
        }
    }
}
