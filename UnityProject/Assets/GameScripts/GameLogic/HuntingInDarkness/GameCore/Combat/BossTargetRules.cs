using System;
using System.Collections.Generic;
using HuntingInDarkness.GameCore.Foundation;

namespace HuntingInDarkness.GameCore.Combat
{
    public enum BossTargetPolicy
    {
        PlayerChoice,
        Nearest,
        MostInjured,
        Random
    }

    public readonly struct BossTargetCandidate
    {
        public int EntityId { get; }
        public int Distance { get; }
        public int DamageTaken { get; }

        public BossTargetCandidate(int entityId, int distance, int damageTaken)
        {
            EntityId = entityId;
            Distance = Math.Max(0, distance);
            DamageTaken = Math.Max(0, damageTaken);
        }
    }

    /// <summary>根据行动卡声明的策略缩小 Boss 合法目标集合，不读取 Unity 或战斗运行图。</summary>
    public static class BossTargetRules
    {
        public static List<int> GetPriorityTargets(IReadOnlyList<BossTargetCandidate> candidates, BossTargetPolicy policy, IRandomSource random)
        {
            var unique = GetUniqueCandidates(candidates);
            if (unique.Count == 0)
                return new List<int>();
            if (!Enum.IsDefined(typeof(BossTargetPolicy), policy))
                policy = BossTargetPolicy.PlayerChoice;
            if (policy == BossTargetPolicy.PlayerChoice)
                return GetIds(unique);
            if (policy == BossTargetPolicy.Random)
                return new List<int> { unique[RequireRandom(random).Next(0, unique.Count)].EntityId };

            int priority = policy == BossTargetPolicy.Nearest ? int.MaxValue : int.MinValue;
            foreach (BossTargetCandidate candidate in unique)
            {
                if (policy == BossTargetPolicy.Nearest)
                    priority = Math.Min(priority, candidate.Distance);
                else
                    priority = Math.Max(priority, candidate.DamageTaken);
            }

            var result = new List<int>();
            foreach (BossTargetCandidate candidate in unique)
            {
                bool matches = policy == BossTargetPolicy.Nearest ? candidate.Distance == priority : candidate.DamageTaken == priority;
                if (matches)
                    result.Add(candidate.EntityId);
            }
            return result;
        }

        public static int SelectFallback(IReadOnlyList<int> targetIds, IRandomSource random)
        {
            if (targetIds == null || targetIds.Count == 0)
                return -1;
            return targetIds[RequireRandom(random).Next(0, targetIds.Count)];
        }

        private static List<BossTargetCandidate> GetUniqueCandidates(IReadOnlyList<BossTargetCandidate> candidates)
        {
            var result = new List<BossTargetCandidate>();
            if (candidates == null)
                return result;

            var ids = new HashSet<int>();
            foreach (BossTargetCandidate candidate in candidates)
                if (candidate.EntityId >= 0 && ids.Add(candidate.EntityId))
                    result.Add(candidate);
            return result;
        }

        private static List<int> GetIds(IReadOnlyList<BossTargetCandidate> candidates)
        {
            var result = new List<int>(candidates.Count);
            foreach (BossTargetCandidate candidate in candidates)
                result.Add(candidate.EntityId);
            return result;
        }

        private static IRandomSource RequireRandom(IRandomSource random) => random ?? throw new ArgumentNullException(nameof(random));
    }
}
