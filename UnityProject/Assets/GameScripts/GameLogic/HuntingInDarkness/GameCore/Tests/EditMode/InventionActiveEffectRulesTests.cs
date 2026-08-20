using System.Collections.Generic;
using HuntingInDarkness.GameCore.Settlement;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests
{
    public sealed class InventionActiveEffectRulesTests
    {
        [Test]
        public void AnnualLimit_ResetsByYearAndCountsDuplicateLegacyRows()
        {
            var usage = new List<InventionActiveEffectUsage>
            {
                new() { EffectId = "prayer:vigil", Year = 2, UseCount = 1 },
                new() { EffectId = "prayer:vigil", Year = 2, UseCount = 1 }
            };

            bool sameYear = InventionActiveEffectRules.CanActivate(true, 2, "prayer:vigil", "active_prayer", 2, usage, true, out string sameYearReason);
            bool nextYear = InventionActiveEffectRules.CanActivate(true, 3, "prayer:vigil", "active_prayer", 2, usage, true, out string nextYearReason);

            Assert.That(sameYear, Is.False);
            Assert.That(sameYearReason, Does.Contain("耗尽"));
            Assert.That(nextYear, Is.True, nextYearReason);
        }

        [Test]
        public void RecordUse_IsStableAndSaturatesAtIntegerMaximum()
        {
            var usage = new List<InventionActiveEffectUsage> { new() { EffectId = "prayer:vigil", Year = 4, UseCount = int.MaxValue } };

            InventionActiveEffectRules.RecordUse(usage, " prayer:vigil ", 4);

            Assert.That(usage, Has.Count.EqualTo(1));
            Assert.That(usage[0].UseCount, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void InvalidOrUnavailableContent_FailsClosed()
        {
            Assert.That(InventionActiveEffectRules.CanActivate(false, 1, "effect", "event", 1, null, true, out _), Is.False);
            Assert.That(InventionActiveEffectRules.CanActivate(true, 1, string.Empty, "event", 1, null, true, out _), Is.False);
            Assert.That(InventionActiveEffectRules.CanActivate(true, 1, "effect", "event", -1, null, true, out _), Is.False);
            Assert.That(InventionActiveEffectRules.CanActivate(true, 1, "effect", "event", 1, null, false, out _), Is.False);
        }
    }
}
