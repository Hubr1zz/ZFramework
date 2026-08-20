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
        }
    }
}
