using System.Collections.Generic;
using HuntingInDarkness.GameCore.Combat;
using HuntingInDarkness.GameCore.Foundation;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests
{
    public sealed class BossTargetRulesTests
    {
        [Test]
        public void PlayerChoice_ReturnsEveryUniqueValidCandidate()
        {
            var candidates = new List<BossTargetCandidate>
            {
                new BossTargetCandidate(3, 2, 0),
                new BossTargetCandidate(-1, 1, 9),
                new BossTargetCandidate(3, 0, 7),
                new BossTargetCandidate(8, 4, 2)
            };

            List<int> targets = BossTargetRules.GetPriorityTargets(candidates, BossTargetPolicy.PlayerChoice, new FirstRandom());

            Assert.That(targets, Is.EqualTo(new[] { 3, 8 }));
        }

        [Test]
        public void Nearest_ReturnsAllTiedNearestCandidates()
        {
            var candidates = new List<BossTargetCandidate>
            {
                new BossTargetCandidate(1, 2, 0),
                new BossTargetCandidate(2, 1, 0),
                new BossTargetCandidate(3, 1, 0)
            };

            List<int> targets = BossTargetRules.GetPriorityTargets(candidates, BossTargetPolicy.Nearest, new FirstRandom());

            Assert.That(targets, Is.EqualTo(new[] { 2, 3 }));
        }

        [Test]
        public void MostInjured_ReturnsCandidatesWithMostAccumulatedDamage()
        {
            var candidates = new List<BossTargetCandidate>
            {
                new BossTargetCandidate(1, 0, 1),
                new BossTargetCandidate(2, 0, 4),
                new BossTargetCandidate(3, 0, 2)
            };

            List<int> targets = BossTargetRules.GetPriorityTargets(candidates, BossTargetPolicy.MostInjured, new FirstRandom());

            Assert.That(targets, Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void Random_UsesInjectedRandomSource()
        {
            var candidates = new List<BossTargetCandidate>
            {
                new BossTargetCandidate(4, 0, 0),
                new BossTargetCandidate(9, 0, 0)
            };

            List<int> targets = BossTargetRules.GetPriorityTargets(candidates, BossTargetPolicy.Random, new LastRandom());

            Assert.That(targets, Is.EqualTo(new[] { 9 }));
        }

        [Test]
        public void EmptyCandidates_ReturnsEmptyWithoutUsingRandom()
        {
            List<int> targets = BossTargetRules.GetPriorityTargets(null, BossTargetPolicy.Random, null);

            Assert.That(targets, Is.Empty);
            Assert.That(BossTargetRules.SelectFallback(targets, null), Is.EqualTo(-1));
        }

        [Test]
        public void UnknownPolicy_FallsBackToPlayerChoice()
        {
            var candidates = new List<BossTargetCandidate>
            {
                new BossTargetCandidate(2, 8, 0),
                new BossTargetCandidate(5, 1, 7)
            };

            List<int> targets = BossTargetRules.GetPriorityTargets(candidates, (BossTargetPolicy)999, null);

            Assert.That(targets, Is.EqualTo(new[] { 2, 5 }));
        }

        private sealed class FirstRandom : IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
        }

        private sealed class LastRandom : IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => maxExclusive - 1;
            public double NextDouble() => 1d;
        }
    }
}
