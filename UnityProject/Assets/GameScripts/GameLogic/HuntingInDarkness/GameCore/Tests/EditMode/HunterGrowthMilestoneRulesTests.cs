using HuntingInDarkness.GameCore.Settlement;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests
{
    public sealed class HunterGrowthMilestoneRulesTests
    {
        [Test]
        public void TryClaim_AtThresholdAppliesRewardExactlyOnce()
        {
            var hunter = new HunterState { Courage = 5, Willpower = 1, WillpowerMax = 2 };
            var definition = new HunterGrowthMilestoneDefinition("courage_5", "不屈之心", "", HunterGrowthChoice.Courage, 5, "不屈之心", 1, new GrowthMilestoneStatModifiers(1, 0, 0, 0));

            Assert.That(HunterGrowthMilestoneRules.TryClaim(hunter, definition, out HunterGrowthMilestoneOutcome outcome), Is.True);
            Assert.That(outcome.Id, Is.EqualTo("courage_5"));
            Assert.That(hunter.Willpower, Is.EqualTo(2));
            Assert.That(hunter.WillpowerMax, Is.EqualTo(3));
            Assert.That(hunter.Stats.strength, Is.EqualTo(1));
            Assert.That(hunter.Traits, Is.EqualTo(new[] { "不屈之心" }));
            Assert.That(HunterGrowthMilestoneRules.TryClaim(hunter, definition, out _), Is.False);
            Assert.That(hunter.WillpowerMax, Is.EqualTo(3));
            Assert.That(hunter.Stats.strength, Is.EqualTo(1));
        }

        [Test]
        public void TryClaim_RejectsBelowThresholdAndDeadHunter()
        {
            var definition = new HunterGrowthMilestoneDefinition("knowledge_2", "辨识痕迹", "", HunterGrowthChoice.Understanding, 2, "辨识痕迹", 0, default);
            var inexperienced = new HunterState { Understanding = 1 };
            var dead = new HunterState { Understanding = 2, IsAlive = false };

            Assert.That(HunterGrowthMilestoneRules.TryClaim(inexperienced, definition, out _), Is.False);
            Assert.That(HunterGrowthMilestoneRules.TryClaim(dead, definition, out _), Is.False);
            Assert.That(inexperienced.ClaimedGrowthMilestoneIds, Is.Empty);
            Assert.That(dead.ClaimedGrowthMilestoneIds, Is.Empty);
        }
    }
}
