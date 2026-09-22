using HuntingInDarkness.GameCore.Settlement;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests
{
    public sealed class InventionEffectRulesTests
    {
        [Test]
        public void TryApply_WillpowerMaximumUsesSafeBoundsAndClampsCurrentValue()
        {
            var hunter = new HunterState { Willpower = 5, WillpowerMax = 5 };

            bool reduced = InventionEffectRules.TryApply(hunter, InventionEffectKind.ModifyWillpowerMaximum, -10, out int previous, out int current);

            Assert.That(reduced, Is.True);
            Assert.That(previous, Is.EqualTo(5));
            Assert.That(current, Is.Zero);
            Assert.That(hunter.Willpower, Is.Zero);

            bool increased = InventionEffectRules.TryApply(hunter, InventionEffectKind.ModifyWillpowerMaximum, int.MaxValue, out _, out current);

            Assert.That(increased, Is.True);
            Assert.That(current, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void IsEligible_DistinguishesAvailableAndLivingTargets()
        {
            var hunter = new HunterState { IsAlive = true, Availability = HunterAvailabilityState.Retired };

            Assert.That(InventionEffectRules.IsEligible(hunter, InventionEffectTarget.AvailableHunters), Is.False);
            Assert.That(InventionEffectRules.IsEligible(hunter, InventionEffectTarget.AliveHunters), Is.True);

            hunter.IsAlive = false;

            Assert.That(InventionEffectRules.IsEligible(hunter, InventionEffectTarget.AliveHunters), Is.False);
        }
    }
}
