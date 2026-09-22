using System.Collections.Generic;
using System.Linq;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Hunt;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests
{
    public sealed class HuntResourceRulesTests
    {
        [Test]
        public void ResolveHarvest_CountsOnlyRollsBelowHitChance()
        {
            var random = new SequenceRandom(0.1, 0.9, 0.4);

            int obtained = HuntResourceRules.ResolveHarvest(3, 0.5, random);

            Assert.That(obtained, Is.EqualTo(2));
        }

        [Test]
        public void SpawnPoints_RespectsPerTileLimit()
        {
            var pool = new[] { new ResourcePointDefinition("Stone", 1, 2, 1) };

            List<ResourcePointDefinition> spawned = HuntResourceRules.SpawnPoints(pool, 3, new SequenceRandom(0.1));

            Assert.That(spawned, Has.Count.EqualTo(1));
            Assert.That(spawned[0].ResourceId, Is.EqualTo("Stone"));
        }

        [Test]
        public void SpawnPoints_EmptyPoolReturnsEmptyResult()
        {
            List<ResourcePointDefinition> spawned = HuntResourceRules.SpawnPoints(new ResourcePointDefinition[0], 2, new SequenceRandom(0.1));

            Assert.That(spawned, Is.Empty);
        }

        [Test]
        public void SpawnPoints_FillsCapacityAfterHighWeightDefinitionReachesLimit()
        {
            var pool = new[]
            {
                new ResourcePointDefinition("HighWeight", 100, 1, 1),
                new ResourcePointDefinition("LegalFallback", 1, 1, 1)
            };

            List<ResourcePointDefinition> spawned = HuntResourceRules.SpawnPoints(pool, 3, new SequenceRandom(0.1));

            Assert.That(spawned.Select(point => point.ResourceId), Is.EqualTo(new[] { "HighWeight", "LegalFallback" }));
        }

        [Test]
        public void SpawnPoints_AllowsRepeatedDefinitionUntilItsPerTileLimit()
        {
            var pool = new[] { new ResourcePointDefinition("Repeatable", 1, 1, 2) };

            List<ResourcePointDefinition> spawned = HuntResourceRules.SpawnPoints(pool, 3, new SequenceRandom(0.1));

            Assert.That(spawned, Has.Count.EqualTo(2));
            Assert.That(spawned.All(point => point.ResourceId == "Repeatable"), Is.True);
        }

        [Test]
        public void SpawnPoints_ExcludesBlankAndInvalidIdsWithoutRetryLoop()
        {
            var pool = new ResourcePointDefinition[]
            {
                null,
                new ResourcePointDefinition(" ", 100, 1, 0),
                new ResourcePointDefinition("Valid", 1, 1, 1)
            };

            List<ResourcePointDefinition> spawned = HuntResourceRules.SpawnPoints(pool, 3, new SequenceRandom(0.1));

            Assert.That(spawned, Has.Count.EqualTo(1));
            Assert.That(spawned[0].ResourceId, Is.EqualTo("Valid"));
        }

        private sealed class SequenceRandom : IRandomSource
        {
            private readonly Queue<double> values;
            private double lastValue;

            public SequenceRandom(params double[] sequence)
            {
                values = new Queue<double>(sequence);
                lastValue = sequence.Length > 0 ? sequence[sequence.Length - 1] : 0d;
            }

            public int Next(int minInclusive, int maxExclusive)
            {
                return minInclusive;
            }

            public double NextDouble()
            {
                if (values.Count > 0)
                    lastValue = values.Dequeue();
                return lastValue;
            }
        }
    }
}
