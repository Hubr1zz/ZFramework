using Core;
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

        [Test]
        public void TryKill_RejectsForeignHunterAndPersistsEventCause()
        {
            var settlement = new SettlementInstance { CurrentYear = 5 };
            var victim = new HunterInstance(null, 11) { Name = "守火者", Age = 3 };
            var foreign = new HunterInstance(null, 12) { Name = "外来者" };
            settlement.Hunters.Add(victim);
            var management = new HunterManagementSystem(settlement, new SystemRandomSource(2));
            HunterDiedEvent diedEvent = default;
            int deathEventCount = 0;
            System.Action<HunterDiedEvent> handler = evt =>
            {
                diedEvent = evt;
                deathEventCount++;
            };
            EventBus.Subscribe(handler);
            try
            {
                Assert.That(management.TryKill(foreign, "foreign", "不应生效", out string foreignReason), Is.False);
                Assert.That(foreignReason, Does.Contain("不属于"));

                Assert.That(management.TryKill(victim, "dark_bargain", "履行了黑暗交易", out string reason), Is.True, reason);
                Assert.That(management.TryKill(victim, "second_cause", "不应覆盖首次死因", out string repeatedReason), Is.True, repeatedReason);

                Assert.That(victim.IsAlive, Is.False);
                Assert.That(settlement.Timeline[0].EventName, Does.Contain("履行了黑暗交易"));
                Assert.That(diedEvent.CauseId, Is.EqualTo("dark_bargain"));
                Assert.That(diedEvent.CauseText, Is.EqualTo("履行了黑暗交易"));
                Assert.That(deathEventCount, Is.EqualTo(1));
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }
    }
}
