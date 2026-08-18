using System.Collections.Generic;
using HuntingInDarkness.GameCore.Settlement;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests
{
    public sealed class RecruitmentRulesTests
    {
        [Test]
        public void CanRecruit_EmptyCampIgnoresCostAndSameYearCooldown()
        {
            bool canRecruit = RecruitmentRules.CanRecruit(3, 3, 0, 6, 0, 2, out string reason);

            Assert.That(canRecruit, Is.True);
            Assert.That(reason, Is.Empty);
            Assert.That(RecruitmentRules.GetCost(0, 2), Is.Zero);
        }

        [Test]
        public void CanRecruit_LivingRosterRequiresCostAndOnePerYear()
        {
            Assert.That(RecruitmentRules.CanRecruit(3, 2, 2, 6, 1, 1, out _), Is.True);
            Assert.That(RecruitmentRules.CanRecruit(3, 3, 2, 6, 1, 1, out string cooldownReason), Is.False);
            Assert.That(cooldownReason, Does.Contain("本年"));
            Assert.That(RecruitmentRules.CanRecruit(3, 2, 2, 6, 0, 1, out string costReason), Is.False);
            Assert.That(costReason, Does.Contain("口粮"));
            Assert.That(RecruitmentRules.CanRecruit(3, 2, 6, 6, 10, 1, out string capacityReason), Is.False);
            Assert.That(capacityReason, Does.Contain("位置"));
        }

        [Test]
        public void TryNormalizeName_TrimsAndRejectsMissingDuplicateOrLongNames()
        {
            var existingNames = new List<string> { "余烬" };

            Assert.That(RecruitmentRules.TryNormalizeName("  新火  ", existingNames, out string normalizedName, out _), Is.True);
            Assert.That(normalizedName, Is.EqualTo("新火"));
            Assert.That(RecruitmentRules.TryNormalizeName(" ", existingNames, out _, out string emptyReason), Is.False);
            Assert.That(emptyReason, Does.Contain("取名"));
            Assert.That(RecruitmentRules.TryNormalizeName("余烬", existingNames, out _, out string duplicateReason), Is.False);
            Assert.That(duplicateReason, Does.Contain("记忆"));
            Assert.That(RecruitmentRules.TryNormalizeName(new string('火', RecruitmentRules.MaximumNameLength + 1), existingNames, out _, out string lengthReason), Is.False);
            Assert.That(lengthReason, Does.Contain(RecruitmentRules.MaximumNameLength.ToString()));
        }

        [Test]
        public void NextAvailableId_ReusesFirstGapWithoutCollidingWithLoadedHunters()
        {
            var hunters = new List<HunterState>
            {
                new HunterState { InstanceId = 100 },
                new HunterState { InstanceId = 102 }
            };

            Assert.That(HunterIdentityRules.NextAvailableId(hunters), Is.EqualTo(101));
        }
    }
}
