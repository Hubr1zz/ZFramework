using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.Settlement;
using NUnit.Framework;

namespace HuntingInDarkness.Tests
{
    public sealed class HunterDeathAftermathTests
    {
        [Test]
        public void KillHunter_CommitsEquipmentAnnalAndInspirationOnce()
        {
            var settlement = new SettlementInstance { CurrentYear = 4 };
            var deceased = new HunterInstance(null, 1) { Name = "先行者", Age = 3, IsAlive = true };
            var survivor = new HunterInstance(null, 2) { Name = "守望者", Age = 2, IsAlive = true };
            deceased.EquippedItemNames.Add("骨矛");
            settlement.Hunters.Add(deceased);
            settlement.Hunters.Add(survivor);
            var management = new HunterManagementSystem(settlement, new SystemRandomSource(1));

            management.KillHunter(deceased);
            management.KillHunter(deceased);

            Assert.That(deceased.IsAlive, Is.False);
            Assert.That(deceased.EquippedItemNames, Is.Empty);
            Assert.That(settlement.GetStoredEquipment("骨矛"), Is.EqualTo(1));
            Assert.That(settlement.Timeline.FindAll(entry => entry.EventId == "death:1"), Has.Count.EqualTo(1));
            Assert.That(settlement.Timeline[0].Year, Is.EqualTo(4));
            Assert.That(survivor.UnspentGrowth, Is.EqualTo(1));
        }
    }
}
