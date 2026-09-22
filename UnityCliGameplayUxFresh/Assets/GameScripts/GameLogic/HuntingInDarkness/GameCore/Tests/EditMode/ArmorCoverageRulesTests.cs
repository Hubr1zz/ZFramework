using HuntingInDarkness.GameCore.Settlement;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests
{
    public sealed class ArmorCoverageRulesTests
    {
        [Test]
        public void CanEquip_AcceptsUnoccupiedCoverage()
        {
            bool accepted = ArmorCoverageRules.CanEquip(ArmorCoverage.Head, ArmorCoverage.Torso | ArmorCoverage.Arms, out string reason);

            Assert.That(accepted, Is.True);
            Assert.That(reason, Is.Empty);
        }

        [Test]
        public void CanEquip_RejectsAnyOverlappingBodyPart()
        {
            bool accepted = ArmorCoverageRules.CanEquip(ArmorCoverage.Torso, ArmorCoverage.Torso | ArmorCoverage.Arms, out string reason);

            Assert.That(accepted, Is.False);
            Assert.That(reason, Is.EqualTo("对应部位已经装备防具"));
        }

        [Test]
        public void CanEquip_RejectsArmorWithoutCoverage()
        {
            bool accepted = ArmorCoverageRules.CanEquip(ArmorCoverage.None, ArmorCoverage.None, out string reason);

            Assert.That(accepted, Is.False);
            Assert.That(reason, Is.EqualTo("防具未配置保护部位"));
        }
    }
}
