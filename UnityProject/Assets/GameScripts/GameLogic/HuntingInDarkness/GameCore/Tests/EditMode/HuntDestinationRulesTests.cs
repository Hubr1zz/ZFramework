using HuntingInDarkness.GameCore.Hunt;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests
{
    public sealed class HuntDestinationRulesTests
    {
        [Test]
        public void CanSelect_AcceptsDestinationAtMinimumYear()
        {
            Assert.That(HuntDestinationRules.CanSelect("stone-forest", "石森林", 2, 2, out string reason), Is.True, reason);
        }

        [Test]
        public void CanSelect_RejectsIncompleteDestination()
        {
            Assert.That(HuntDestinationRules.CanSelect(string.Empty, "石森林", 1, 1, out string reason), Is.False);
            Assert.That(reason, Does.Contain("不完整"));
        }

        [Test]
        public void CanSelect_RejectsDestinationBeforeMinimumYear()
        {
            Assert.That(HuntDestinationRules.CanSelect("sunken-marsh", "沉陷菌沼", 1, 3, out string reason), Is.False);
            Assert.That(reason, Does.Contain("第 3 年"));
        }
    }
}
