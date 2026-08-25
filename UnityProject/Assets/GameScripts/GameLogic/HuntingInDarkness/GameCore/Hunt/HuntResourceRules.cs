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
            => CreateHarvestPlan(drawCount, hitChance, random).HitCount;

        public static HarvestDrawPlan CreateHarvestPlan(int drawCount, double hitChance, IRandomSource random)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            int safeDrawCount = Math.Max(0, Math.Min(drawCount, HarvestDrawPlan.MaximumCardCount));
            double safeHitChance = Math.Max(0d, Math.Min(hitChance, 1d));
            var cards = new List<HarvestCardResult>(safeDrawCount);
            for (int i = 0; i < safeDrawCount; i++)
                cards.Add(new HarvestCardResult(i, random.NextDouble() < safeHitChance));
            return new HarvestDrawPlan(cards.AsReadOnly(), safeHitChance);
        }

        public static HarvestDrawPlan CreateMaterialPoolPlan(IReadOnlyList<HarvestMaterialDefinition> materials, int revealLimit, IRandomSource random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            var shuffled = new List<HarvestMaterialDefinition>();
            if (materials != null)
                foreach (HarvestMaterialDefinition material in materials)
                    if (!string.IsNullOrWhiteSpace(material.MaterialId) && shuffled.Count < HarvestDrawPlan.MaximumCardCount)
                        shuffled.Add(material);

            for (int index = shuffled.Count - 1; index > 0; index--)
            {
                int target = random.Next(0, index + 1);
                HarvestMaterialDefinition swap = shuffled[index];
                shuffled[index] = shuffled[target];
                shuffled[target] = swap;
            }

            var cards = new List<HarvestCardResult>(shuffled.Count);
            double totalChance = 0d;
            for (int index = 0; index < shuffled.Count; index++)
            {
                HarvestMaterialDefinition material = shuffled[index];
                cards.Add(new HarvestCardResult(index, random.NextDouble() < material.HitChance, material.MaterialId, material.DisplayName));
                totalChance += material.HitChance;
            }
            double averageChance = shuffled.Count > 0 ? totalChance / shuffled.Count : 0d;
            return new HarvestDrawPlan(cards.AsReadOnly(), averageChance, revealLimit);
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
