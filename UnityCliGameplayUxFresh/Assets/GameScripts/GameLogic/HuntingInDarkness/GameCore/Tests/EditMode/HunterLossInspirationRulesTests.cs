using HuntingInDarkness.GameCore.Settlement;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests
{
    public sealed class HunterLossInspirationRulesTests
    {
        [Test]
        public void CreatePlan_ChoosesUniqueAvailableSurvivors()
        {
            var deceased = new HunterState { InstanceId = 1, Age = 3, IsAlive = false };
            var active = new HunterState { InstanceId = 2, IsAlive = true };
            var retired = new HunterState { InstanceId = 3, IsAlive = true, Availability = HunterAvailabilityState.Retired };

            HunterLossInspirationPlan plan = HunterLossInspirationRules.CreatePlan(deceased, new[] { deceased, active, active, retired }, 2, 2);

            Assert.That(plan.GrowthPerHunter, Is.EqualTo(2));
            Assert.That(plan.HunterIds, Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void CreatePlan_RejectsNewRecruitLoss()
        {
            var deceased = new HunterState { InstanceId = 1, Age = 1, IsAlive = false };
            var survivor = new HunterState { InstanceId = 2, IsAlive = true };

            HunterLossInspirationPlan plan = HunterLossInspirationRules.CreatePlan(deceased, new[] { deceased, survivor }, 1, 2);

            Assert.That(plan.GrowthPerHunter, Is.Zero);
            Assert.That(plan.HunterIds, Is.Empty);
        }
    }
}
