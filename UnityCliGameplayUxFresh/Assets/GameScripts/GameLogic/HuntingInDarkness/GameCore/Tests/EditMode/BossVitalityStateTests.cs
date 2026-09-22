using HuntingInDarkness.GameCore.Combat;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests
{
    public sealed class BossVitalityStateTests
    {
        [Test]
        public void Constructor_ClampsInvalidMaximumToOne()
        {
            var vitality = new BossVitalityState(0);

            Assert.That(vitality.MaxHealth, Is.EqualTo(1));
            Assert.That(vitality.CurrentHealth, Is.EqualTo(1));
        }

        [Test]
        public void ApplyDamage_ClampsAtZeroAndReportsTransition()
        {
            var vitality = new BossVitalityState(3);

            BossVitalityDamageResult result = vitality.ApplyDamage(5);

            Assert.That(result.AppliedDamage, Is.EqualTo(3));
            Assert.That(result.PreviousHealth, Is.EqualTo(3));
            Assert.That(result.CurrentHealth, Is.Zero);
            Assert.That(result.WasDefeated, Is.False);
            Assert.That(result.IsDefeated, Is.True);
        }

        [Test]
        public void ApplyDamage_AfterDefeatIsIdempotent()
        {
            var vitality = new BossVitalityState(1);
            vitality.ApplyDamage(1);

            BossVitalityDamageResult result = vitality.ApplyDamage(1);

            Assert.That(result.AppliedDamage, Is.Zero);
            Assert.That(result.WasDefeated, Is.True);
            Assert.That(result.IsDefeated, Is.True);
        }

        [Test]
        public void ApplyDamage_IgnoresNegativeDamage()
        {
            var vitality = new BossVitalityState(2);

            BossVitalityDamageResult result = vitality.ApplyDamage(-3);

            Assert.That(result.AppliedDamage, Is.Zero);
            Assert.That(vitality.CurrentHealth, Is.EqualTo(2));
        }

        [Test]
        public void TryClaimDefeat_SucceedsOnlyOnceAfterDefeat()
        {
            var vitality = new BossVitalityState(1);

            Assert.That(vitality.TryClaimDefeat(), Is.False);
            vitality.ApplyDamage(1);
            Assert.That(vitality.TryClaimDefeat(), Is.True);
            Assert.That(vitality.TryClaimDefeat(), Is.False);
        }
    }
}
