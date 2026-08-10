using System;
using System.Collections.Generic;
using HuntingInDarkness.GameCore.Combat;
using HuntingInDarkness.GameCore.Foundation;

namespace HuntingInDarkness.GameCore.Hunt
{
    public static class HuntResourceRules
    {
        public static List<ResourcePointDefinition> SpawnPoints(
            IReadOnlyList<ResourcePointDefinition> pool,
            int maximumPoints,
            IRandomSource random)
        {
            var result = new List<ResourcePointDefinition>();
            if (pool == null || pool.Count == 0) return result;
            var placed = new Dictionary<string, int>();

            for (int attempt = 0;
                 attempt < maximumPoints * 3 && result.Count < maximumPoints;
                 attempt++)
            {
                List<ResourcePointDefinition> draw = WeightedSelection.DrawWithoutReplacement(
                    pool, 1, item => Math.Max(1, item?.SpawnWeight ?? 0), random);
                if (draw.Count == 0 || draw[0] == null || string.IsNullOrEmpty(draw[0].ResourceId))
                    continue;
                ResourcePointDefinition point = draw[0];
                placed.TryGetValue(point.ResourceId, out int existing);
                if (point.MaxPerTile > 0 && existing >= point.MaxPerTile)
                    continue;
                result.Add(point);
                placed[point.ResourceId] = existing + 1;
            }
            return result;
        }

        public static int ResolveHarvest(int drawCount, double hitChance, IRandomSource random)
        {
            int obtained = 0;
            for (int i = 0; i < drawCount; i++)
                if (random.NextDouble() < hitChance) obtained++;
            return obtained;
        }
    }

    public static class HuntEventRules
    {
        public static bool ShouldTrigger(double probability, IRandomSource random) =>
            random.NextDouble() < probability;

        public static T PickWeighted<T>(
            IReadOnlyList<T> pool,
            Func<T, int> getWeight,
            IRandomSource random)
        {
            if (pool == null || pool.Count == 0) return default;
            List<T> result = WeightedSelection.DrawWithoutReplacement(
                pool, 1, item => Math.Max(1, getWeight(item)), random);
            return result.Count > 0 ? result[0] : default;
        }
    }
}
