using HuntingInDarkness.GameCore.Hunt;
using NUnit.Framework;
using UI.Hunt;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class HuntHarvestPanel3DTests
    {
        [TestCase(-5, 0)]
        [TestCase(2, 2)]
        [TestCase(999, HarvestDrawPlan.MaximumCardCount)]
        public void ClampCardCount_UsesDomainLimit(int configuredCount, int expected)
        {
            Assert.That(HuntHarvestLayout.ClampCardCount(configuredCount), Is.EqualTo(expected));
        }

        [Test]
        public void GetCardLocalPosition_CentersEveryRow()
        {
            var first = HuntHarvestLayout.GetCardLocalPosition(0, 8);
            var sixth = HuntHarvestLayout.GetCardLocalPosition(5, 8);
            var seventh = HuntHarvestLayout.GetCardLocalPosition(6, 8);
            var eighth = HuntHarvestLayout.GetCardLocalPosition(7, 8);

            Assert.That(first.x, Is.EqualTo(-sixth.x).Within(0.001f));
            Assert.That(seventh.x, Is.EqualTo(-eighth.x).Within(0.001f));
            Assert.That(first.z, Is.EqualTo(sixth.z).Within(0.001f));
            Assert.That(seventh.z, Is.LessThan(first.z));
        }

        [Test]
        public void GetCloseCardLocalPosition_StaysBelowLastRow()
        {
            var lastCard = HuntHarvestLayout.GetCardLocalPosition(31, HarvestDrawPlan.MaximumCardCount);
            var closeCard = HuntHarvestLayout.GetCloseCardLocalPosition(HarvestDrawPlan.MaximumCardCount);

            Assert.That(closeCard.z, Is.LessThan(lastCard.z));
            Assert.That(closeCard.x, Is.Zero);
        }
    }
}
