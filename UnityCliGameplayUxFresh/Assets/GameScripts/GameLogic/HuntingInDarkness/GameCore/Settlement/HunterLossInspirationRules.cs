using System;
using System.Collections.Generic;

namespace HuntingInDarkness.GameCore.Settlement
{
    /// <summary>猎人永久死亡后，对仍可出征猎人的最小补偿计划。</summary>
    public sealed class HunterLossInspirationPlan
    {
        public int GrowthPerHunter { get; }
        public IReadOnlyList<int> HunterIds { get; }

        public HunterLossInspirationPlan(int growthPerHunter, IReadOnlyList<int> hunterIds)
        {
            GrowthPerHunter = Math.Max(0, growthPerHunter);
            HunterIds = hunterIds ?? Array.Empty<int>();
        }
    }

    public static class HunterLossInspirationRules
    {
        public static HunterLossInspirationPlan CreatePlan(HunterState deceased, IReadOnlyList<HunterState> roster, int growthPerHunter, int minimumDeceasedAge)
        {
            if (deceased == null || deceased.IsAlive || growthPerHunter <= 0 || deceased.Age < Math.Max(1, minimumDeceasedAge))
                return new HunterLossInspirationPlan(0, Array.Empty<int>());
            if (roster == null || roster.Count == 0)
                return new HunterLossInspirationPlan(0, Array.Empty<int>());

            var hunterIds = new List<int>();
            var seenIds = new HashSet<int>();
            foreach (HunterState hunter in roster)
            {
                if (hunter == null || hunter.InstanceId == deceased.InstanceId || !hunter.IsAvailable || !seenIds.Add(hunter.InstanceId))
                    continue;
                hunterIds.Add(hunter.InstanceId);
            }
            return new HunterLossInspirationPlan(hunterIds.Count > 0 ? growthPerHunter : 0, hunterIds);
        }
    }
}
