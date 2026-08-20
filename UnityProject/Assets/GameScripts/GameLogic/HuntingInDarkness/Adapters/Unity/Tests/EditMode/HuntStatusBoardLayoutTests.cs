using NUnit.Framework;
using UI.Hunt;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class HuntStatusBoardLayoutTests
    {
        [Test]
        public void HunterCards_FormTwoCenteredColumns()
        {
            var first = HuntStatusBoardLayout.GetHunterCardLocalPosition(0);
            var second = HuntStatusBoardLayout.GetHunterCardLocalPosition(1);
            var third = HuntStatusBoardLayout.GetHunterCardLocalPosition(2);

            Assert.That(first.x, Is.EqualTo(-second.x).Within(0.001f));
            Assert.That(first.z, Is.EqualTo(second.z).Within(0.001f));
            Assert.That(third.z, Is.LessThan(first.z));
        }

        [TestCase(-1)]
        [TestCase(HuntStatusBoardLayout.MaximumHunterCards)]
        public void HunterCardPosition_RejectsOutsideSquadCapacity(int index)
        {
            Assert.That(() => HuntStatusBoardLayout.GetHunterCardLocalPosition(index), Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }
    }
}
