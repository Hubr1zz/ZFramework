using HuntingInDarkness.GameCore.Settlement;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests
{
    public sealed class WeaponTrainingRulesTests
    {
        [Test]
        public void CanTrain_ValidUnlockedPlan_ReturnsTrue()
        {
            Assert.That(WeaponTrainingRules.CanTrain(true, true, 1, 1, "mastery_blade", 1, out string reason), Is.True);
            Assert.That(reason, Is.Empty);
        }

        [TestCase(false, true, 1, "该猎人当前无法训练")]
        [TestCase(true, false, 1, "需要先掌握武器训练")]
        [TestCase(true, true, 0, "训练资源不足")]
        public void CanTrain_InvalidState_ReportsReason(bool available, bool unlocked, int resources, string expectedReason)
        {
            Assert.That(WeaponTrainingRules.CanTrain(available, unlocked, resources, 1, "mastery_blade", 1, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo(expectedReason));
        }

        [Test]
        public void TryGain_AtIntegerMaximumDoesNotClaimMilestoneOrTrait()
        {
            var hunter = new HunterState
            {
                WeaponMasteries = new System.Collections.Generic.List<WeaponMasteryState>
                {
                    new WeaponMasteryState { MasteryId = "mastery_blade", DisplayName = "刃术", Experience = int.MaxValue }
                }
            };
            var family = new WeaponMasteryFamilyDefinition("mastery_blade", "刃术", new[]
            {
                new WeaponMasteryMilestoneDefinition("late", "迟来的里程碑", 1, "trait_should_not_gain")
            });

            bool gained = WeaponMasteryRules.TryGain(hunter, family, 1, out _);

            Assert.That(gained, Is.False);
            Assert.That(hunter.WeaponMasteries[0].ClaimedMilestoneIds, Is.Empty);
            Assert.That(hunter.Traits, Does.Not.Contain("trait_should_not_gain"));
        }
    }
}
