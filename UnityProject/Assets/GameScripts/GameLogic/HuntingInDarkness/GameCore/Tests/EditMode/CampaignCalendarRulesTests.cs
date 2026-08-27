using NUnit.Framework;
using HuntingInDarkness.GameCore.Settlement;

namespace HuntingInDarkness.GameCore.Tests
{
    public sealed class CampaignCalendarRulesTests
    {
        [Test]
        public void ThreeSeasonCalendarAdvancesWithoutHardcodedSeasonCount()
        {
            var calendar = new CampaignCalendarDefinition("three_season_test", new[]
            {
                new SeasonDefinition("season_a", "A", 0),
                new SeasonDefinition("season_b", "B", 1),
                new SeasonDefinition("season_c", "C", 2)
            });

            Assert.That(CampaignCalendarRules.TryCreateAdvancePlan(calendar, 4, 0, out CampaignCalendarAdvancePlan first, out string reason), Is.True, reason);
            Assert.That(first.NextYear, Is.EqualTo(4));
            Assert.That(first.NextSeasonIndex, Is.EqualTo(1));
            Assert.That(first.YearAdvanced, Is.False);
            Assert.That(CampaignCalendarRules.TryCreateAdvancePlan(calendar, first.NextYear, first.NextSeasonIndex, out CampaignCalendarAdvancePlan second, out reason), Is.True, reason);
            Assert.That(second.NextSeasonIndex, Is.EqualTo(2));
            Assert.That(CampaignCalendarRules.TryCreateAdvancePlan(calendar, second.NextYear, second.NextSeasonIndex, out CampaignCalendarAdvancePlan third, out reason), Is.True, reason);
            Assert.That(third.NextYear, Is.EqualTo(5));
            Assert.That(third.NextSeasonIndex, Is.Zero);
            Assert.That(third.YearAdvanced, Is.True);
        }

        [Test]
        public void InvalidCalendarIdentityOrderAndOverflowFailClosed()
        {
            var duplicate = new CampaignCalendarDefinition("duplicate", new[]
            {
                new SeasonDefinition("same", "A", 0),
                new SeasonDefinition("same", "B", 1)
            });
            Assert.That(CampaignCalendarRules.TryValidateDefinition(duplicate, out _), Is.False);

            var calendar = new CampaignCalendarDefinition("two_season_test", new[]
            {
                new SeasonDefinition("season_a", "A", 0),
                new SeasonDefinition("season_b", "B", 1)
            });
            var nonFirstDefault = new CampaignCalendarDefinition("non_first_default", calendar.Seasons, 1);
            Assert.That(CampaignCalendarRules.TryValidateDefinition(nonFirstDefault, out string defaultReason), Is.False);
            Assert.That(defaultReason, Does.Contain("列表首项"));
            Assert.That(CampaignCalendarRules.TryCreateAdvancePlan(calendar, int.MaxValue, 1, out _, out _), Is.False);
            Assert.That(CampaignCalendarRules.TryCreateAdvancePlan(calendar, 1, 2, out _, out _), Is.False);
        }
    }
}
