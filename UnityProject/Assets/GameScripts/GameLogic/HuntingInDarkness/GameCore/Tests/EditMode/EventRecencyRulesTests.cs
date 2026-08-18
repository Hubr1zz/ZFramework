using HuntingInDarkness.GameCore.Settlement;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests
{
    public sealed class EventRecencyRulesTests
    {
        [Test]
        public void ShouldExcludeMostRecent_WithAlternative_ExcludesMatchingEvent()
        {
            Assert.That(EventRecencyRules.ShouldExcludeMostRecent("AshMarket", "AshMarket", true), Is.True);
            Assert.That(EventRecencyRules.ShouldExcludeMostRecent("ColdWatch", "AshMarket", true), Is.False);
        }

        [Test]
        public void ShouldExcludeMostRecent_OnlyAvailableEvent_AllowsRepeat()
        {
            Assert.That(EventRecencyRules.ShouldExcludeMostRecent("AshMarket", "AshMarket", false), Is.False);
        }
    }
}
