using HuntingInDarkness.GameCore.Settlement;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests
{
    public sealed class EventRerollRulesTests
    {
        [Test]
        public void TryReroll_RejectsInvalidPreparedValueWithoutSpendingWillpower()
        {
            var hunter = new HunterState { Willpower = 1, WillpowerMax = 1 };

            RerollOutcome result = EventRules.TryReroll(hunter, 4, 11);

            Assert.That(result.Success, Is.False);
            Assert.That(result.FinalRoll, Is.EqualTo(4));
            Assert.That(hunter.Willpower, Is.EqualTo(1));
            Assert.That(hunter.Luck, Is.Zero);
        }

        [Test]
        public void TryReroll_SpendsWillpowerAndIncreasesFateExactlyOnce()
        {
            var hunter = new HunterState { Willpower = 2, WillpowerMax = 2, Luck = 3 };

            RerollOutcome result = EventRules.TryReroll(hunter, 4, 8);

            Assert.That(result.Success, Is.True);
            Assert.That(result.FinalRoll, Is.EqualTo(8));
            Assert.That(hunter.Willpower, Is.EqualTo(1));
            Assert.That(hunter.Luck, Is.EqualTo(4));
        }

        [Test]
        public void TryReroll_WithoutWillpowerDoesNotIncreaseFate()
        {
            var hunter = new HunterState { Willpower = 0, WillpowerMax = 1, Luck = 3 };

            RerollOutcome result = EventRules.TryReroll(hunter, 4, 8);

            Assert.That(result.Success, Is.False);
            Assert.That(result.FinalRoll, Is.EqualTo(4));
            Assert.That(hunter.Willpower, Is.Zero);
            Assert.That(hunter.Luck, Is.EqualTo(3));
        }

        [Test]
        public void TrySpendWillpower_NonRerollCostDoesNotIncreaseFateOrAcceptNegativeCost()
        {
            var hunter = new HunterState { Willpower = 3, WillpowerMax = 3, Luck = 2 };

            Assert.That(hunter.TrySpendWillpower(2), Is.True);
            Assert.That(hunter.TrySpendWillpower(-1), Is.False);
            Assert.That(hunter.Willpower, Is.EqualTo(1));
            Assert.That(hunter.Luck, Is.EqualTo(2));
        }

        [Test]
        public void TryReroll_SaturatesFateAtMaximumValue()
        {
            var hunter = new HunterState { Willpower = 1, WillpowerMax = 1, Luck = int.MaxValue };

            RerollOutcome result = EventRules.TryReroll(hunter, 4, 8);

            Assert.That(result.Success, Is.True);
            Assert.That(hunter.Willpower, Is.Zero);
            Assert.That(hunter.Luck, Is.EqualTo(int.MaxValue));
        }
    }
}
