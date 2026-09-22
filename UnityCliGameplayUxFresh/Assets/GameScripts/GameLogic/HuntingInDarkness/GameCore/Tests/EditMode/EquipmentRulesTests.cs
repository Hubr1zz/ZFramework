using HuntingInDarkness.GameCore.Settlement;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests
{
    public sealed class EquipmentRulesTests
    {
        [Test]
        public void CanEquip_AcceptsItemWithinCapacity()
        {
            Assert.That(EquipmentRules.CanEquip(0, 0, true, out string reason), Is.True);
            Assert.That(reason, Is.Empty);
        }

        [Test]
        public void CanEquip_RejectsTenthEquipment()
        {
            Assert.That(EquipmentRules.CanEquip(EquipmentRules.MaximumEquipmentCount, 0, false, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo("装备栏已满"));
        }

        [Test]
        public void CanEquip_RejectsThirdWeapon()
        {
            Assert.That(EquipmentRules.CanEquip(2, EquipmentRules.MaximumWeaponCount, true, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo("武器数量已达上限"));
            Assert.That(EquipmentRules.CanEquip(2, EquipmentRules.MaximumWeaponCount, false, out _), Is.True);
        }

        [Test]
        public void CanEquip_RejectsInvalidCounts()
        {
            Assert.That(EquipmentRules.CanEquip(-1, 0, false, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo("装备数量状态无效"));
        }
    }
}
