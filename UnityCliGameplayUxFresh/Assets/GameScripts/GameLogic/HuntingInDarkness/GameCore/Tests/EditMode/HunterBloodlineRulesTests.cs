using System.Collections.Generic;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Settlement;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests
{
    public sealed class HunterBloodlineRulesTests
    {
        private static readonly IReadOnlyList<HunterBloodlineDefinition> Definitions = new[]
        {
            new HunterBloodlineDefinition("common", "常见血脉", string.Empty, string.Empty, 3),
            new HunterBloodlineDefinition("rare", "稀有血脉", string.Empty, string.Empty, 1)
        };

        [Test]
        public void TryAssign_UsesConfiguredWeightsAndPersistsStableIdentity()
        {
            var hunter = new HunterState();

            bool assigned = HunterBloodlineRules.TryAssign(hunter, Definitions, new LastRandom(), out HunterBloodlineDefinition definition, out string reason);

            Assert.That(assigned, Is.True, reason);
            Assert.That(definition.Id, Is.EqualTo("rare"));
            Assert.That(hunter.BloodlineId, Is.EqualTo("rare"));
            Assert.That(hunter.BloodlineName, Is.EqualTo("稀有血脉"));
            Assert.That(hunter.IsBloodlineActivated, Is.False);
        }

        [Test]
        public void TryAssign_ExistingIdentityIsReconciledWithoutReroll()
        {
            var hunter = new HunterState { BloodlineId = "common", BloodlineName = "旧名称", IsBloodlineActivated = true };

            bool assigned = HunterBloodlineRules.TryAssign(hunter, Definitions, new LastRandom(), out HunterBloodlineDefinition definition, out string reason);

            Assert.That(assigned, Is.True, reason);
            Assert.That(definition.Id, Is.EqualTo("common"));
            Assert.That(hunter.BloodlineName, Is.EqualTo("常见血脉"));
            Assert.That(hunter.IsBloodlineActivated, Is.True);
        }

        [Test]
        public void TryActivate_RequiresMatchingStableIdentity()
        {
            var hunter = new HunterState { BloodlineId = "common" };

            Assert.That(HunterBloodlineRules.TryActivate(hunter, "rare", out _), Is.False);
            Assert.That(HunterBloodlineRules.TryActivate(hunter, "common", out string reason), Is.True, reason);
            Assert.That(hunter.IsBloodlineActivated, Is.True);
            Assert.That(HunterBloodlineRules.TryActivate(hunter, "common", out reason), Is.False);
            Assert.That(reason, Does.Contain("已经激活"));
        }

        private sealed class LastRandom : IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => maxExclusive - 1;
            public double NextDouble() => 1d;
        }
    }
}
