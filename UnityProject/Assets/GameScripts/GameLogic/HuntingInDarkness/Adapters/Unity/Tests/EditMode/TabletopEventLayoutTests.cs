using HuntingInDarkness.ViewLayer.Tabletop;
using NUnit.Framework;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class TabletopEventLayoutTests
    {
        [Test]
        public void GetChoiceLocalPosition_CentersFullRow()
        {
            var first = TabletopEventLayout.GetChoiceLocalPosition(0, 4);
            var last = TabletopEventLayout.GetChoiceLocalPosition(3, 4);

            Assert.That(first.x, Is.EqualTo(-last.x).Within(0.001f));
            Assert.That(first.z, Is.EqualTo(last.z).Within(0.001f));
        }

        [Test]
        public void GetChoiceLocalPosition_CentersPartialRow()
        {
            var fifth = TabletopEventLayout.GetChoiceLocalPosition(4, 6);
            var sixth = TabletopEventLayout.GetChoiceLocalPosition(5, 6);

            Assert.That(fifth.x, Is.EqualTo(-sixth.x).Within(0.001f));
            Assert.That(fifth.z, Is.EqualTo(sixth.z).Within(0.001f));
        }

        [Test]
        public void GetChoiceLocalPosition_PlacesLaterRowsFartherFromPrimaryCard()
        {
            var first = TabletopEventLayout.GetChoiceLocalPosition(0, 5);
            var fifth = TabletopEventLayout.GetChoiceLocalPosition(4, 5);

            Assert.That(fifth.z, Is.LessThan(first.z));
        }

        [TestCase(-1, 1)]
        [TestCase(1, 1)]
        [TestCase(0, 0)]
        public void GetChoiceLocalPosition_RejectsInvalidRange(int index, int count)
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => TabletopEventLayout.GetChoiceLocalPosition(index, count));
        }
    }
}
