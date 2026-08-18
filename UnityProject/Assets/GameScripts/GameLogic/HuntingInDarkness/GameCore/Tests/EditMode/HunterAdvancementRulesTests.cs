using HuntingInDarkness.GameCore.Settlement;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests
{
    public sealed class HunterAdvancementRulesTests
    {
        [Test]
        public void AdvanceAfterHunt_IncreasesAgeAndAddsGrowthPoint()
        {
            var hunter = new HunterState { Age = 1, IsAlive = true };

            HunterAdvancementOutcome outcome = HunterAdvancementRules.AdvanceAfterHunt(hunter);

            Assert.That(outcome.Advanced, Is.True);
            Assert.That(outcome.ReachedMilestone, Is.True);
            Assert.That(hunter.Age, Is.EqualTo(2));
            Assert.That(hunter.UnspentGrowth, Is.EqualTo(1));
        }

        [Test]
        public void AdvanceAfterHunt_RejectsDeadAndRetiresMaximumAgeHunter()
        {
            var deadHunter = new HunterState { Age = 4, IsAlive = false };
            var maximumAgeHunter = new HunterState { Age = HunterAdvancementRules.MaximumAge, IsAlive = true };

            Assert.That(HunterAdvancementRules.AdvanceAfterHunt(deadHunter).Advanced, Is.False);
            HunterAdvancementOutcome retirement = HunterAdvancementRules.AdvanceAfterHunt(maximumAgeHunter);

            Assert.That(retirement.Advanced, Is.False);
            Assert.That(retirement.Retired, Is.True);
            Assert.That(maximumAgeHunter.Availability, Is.EqualTo(HunterAvailabilityState.Retired));
            Assert.That(deadHunter.UnspentGrowth, Is.Zero);
            Assert.That(maximumAgeHunter.UnspentGrowth, Is.Zero);
        }

        [Test]
        public void TrySpendGrowth_AppliesChosenAttributeOnly()
        {
            var hunter = new HunterState { IsAlive = true, UnspentGrowth = 2, Courage = 1, Understanding = 3 };

            bool courageSpent = HunterAdvancementRules.TrySpendGrowth(hunter, HunterGrowthChoice.Courage);
            bool understandingSpent = HunterAdvancementRules.TrySpendGrowth(hunter, HunterGrowthChoice.Understanding);

            Assert.That(courageSpent, Is.True);
            Assert.That(understandingSpent, Is.True);
            Assert.That(hunter.Courage, Is.EqualTo(2));
            Assert.That(hunter.Understanding, Is.EqualTo(4));
            Assert.That(hunter.UnspentGrowth, Is.Zero);
        }

        [Test]
        public void TrySpendGrowth_PreservesPointWhenChosenAttributeIsCapped()
        {
            var hunter = new HunterState { IsAlive = true, UnspentGrowth = 1, Courage = HunterAdvancementRules.MaximumGrowthAttribute };

            bool spent = HunterAdvancementRules.TrySpendGrowth(hunter, HunterGrowthChoice.Courage);

            Assert.That(spent, Is.False);
            Assert.That(hunter.Courage, Is.EqualTo(HunterAdvancementRules.MaximumGrowthAttribute));
            Assert.That(hunter.UnspentGrowth, Is.EqualTo(1));
        }

        [Test]
        public void TrySpendGrowth_RejectsUnknownChoiceWithoutMutation()
        {
            var hunter = new HunterState { IsAlive = true, UnspentGrowth = 1, Understanding = 2 };

            bool spent = HunterAdvancementRules.TrySpendGrowth(hunter, (HunterGrowthChoice)999);

            Assert.That(spent, Is.False);
            Assert.That(hunter.Understanding, Is.EqualTo(2));
            Assert.That(hunter.UnspentGrowth, Is.EqualTo(1));
        }
    }
}
