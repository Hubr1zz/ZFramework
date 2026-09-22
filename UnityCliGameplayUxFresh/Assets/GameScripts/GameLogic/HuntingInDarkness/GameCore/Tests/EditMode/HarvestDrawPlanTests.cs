using System;
using System.Collections.Generic;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Hunt;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests.EditMode
{
    public sealed class HarvestDrawPlanTests
    {
        [Test]
        public void CreateHarvestPlan_PreservesEveryCardResultInOrder()
        {
            var random = new SequenceRandom(0.1, 0.9, 0.4);

            HarvestDrawPlan plan = HuntResourceRules.CreateHarvestPlan(3, 0.5, random);

            Assert.AreEqual(3, plan.CardCount);
            Assert.AreEqual(2, plan.HitCount);
            Assert.IsTrue(plan.Cards[0].IsHit);
            Assert.IsFalse(plan.Cards[1].IsHit);
            Assert.IsTrue(plan.Cards[2].IsHit);
        }

        [TestCase(-1, 0)]
        [TestCase(0, 0)]
        [TestCase(999, HarvestDrawPlan.MaximumCardCount)]
        public void CreateHarvestPlan_ClampsUnsafeCardCounts(int drawCount, int expected)
        {
            HarvestDrawPlan plan = HuntResourceRules.CreateHarvestPlan(drawCount, 1d, new SequenceRandom(0d));

            Assert.AreEqual(expected, plan.CardCount);
        }

        [TestCase(-1d, 0)]
        [TestCase(2d, 3)]
        public void CreateHarvestPlan_ClampsHitChance(double chance, int expectedHits)
        {
            HarvestDrawPlan plan = HuntResourceRules.CreateHarvestPlan(3, chance, new SequenceRandom(0.5));

            Assert.AreEqual(expectedHits, plan.HitCount);
        }

        [Test]
        public void CreateHarvestPlan_RejectsMissingRandom()
        {
            Assert.Throws<ArgumentNullException>(() => HuntResourceRules.CreateHarvestPlan(1, 0.5, null));
        }

        [Test]
        public void CreateMaterialPoolPlan_PreservesMaterialIdentityAndLimitsSelection()
        {
            var materials = new[]
            {
                new HarvestMaterialDefinition("stone", "碎石", 1d),
                new HarvestMaterialDefinition("organ", "柔软器官", 0d),
                new HarvestMaterialDefinition("fungus", "菌肉", 1d)
            };

            HarvestDrawPlan plan = HuntResourceRules.CreateMaterialPoolPlan(materials, 2, new SequenceRandom(0d));

            Assert.That(plan.CardCount, Is.EqualTo(3));
            Assert.That(plan.RevealLimit, Is.EqualTo(2));
            Assert.That(plan.Cards[0].MaterialId, Is.EqualTo("stone"));
            Assert.That(plan.Cards[1].MaterialId, Is.EqualTo("organ"));
            Assert.That(plan.Cards[2].MaterialId, Is.EqualTo("fungus"));
            Assert.That(plan.Cards[0].IsHit, Is.True);
            Assert.That(plan.Cards[1].IsHit, Is.False);
        }

        private sealed class SequenceRandom : IRandomSource
        {
            private readonly Queue<double> values;
            private double lastValue;

            public SequenceRandom(params double[] values)
            {
                this.values = new Queue<double>(values);
                lastValue = values.Length > 0 ? values[values.Length - 1] : 0d;
            }

            public int Next(int minInclusive, int maxExclusive) => maxExclusive - 1;

            public double NextDouble()
            {
                if (values.Count > 0)
                    lastValue = values.Dequeue();
                return lastValue;
            }
        }
    }
}
