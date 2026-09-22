using HuntingInDarkness.GameCore.Settlement;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests
{
    public sealed class DepartureRulesTests
    {
        [Test]
        public void CanDepart_RejectsEmptySquad()
        {
            bool canDepart = DepartureRules.CanDepart(new int[0], out string reason);

            Assert.That(canDepart, Is.False);
            Assert.That(reason, Is.Not.Empty);
        }

        [Test]
        public void CanDepart_AcceptsFourHunters()
        {
            bool canDepart = DepartureRules.CanDepart(new[] { 1, 2, 3, 4 }, out string reason);

            Assert.That(canDepart, Is.True);
            Assert.That(reason, Is.Empty);
        }

        [Test]
        public void CanDepart_RejectsMoreThanFourHunters()
        {
            bool canDepart = DepartureRules.CanDepart(new[] { 1, 2, 3, 4, 5 }, out string reason);

            Assert.That(canDepart, Is.False);
            Assert.That(reason, Does.Contain("4"));
        }
    }
}
