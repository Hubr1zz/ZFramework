using System.Collections.Generic;
using Core;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Tests
{
    public sealed class PlayableHunterAdvancementAdapterTests
    {
        [Test]
        public void ApplyAfterHunt_DeduplicatesRosterAndSkipsDeadHunters()
        {
            var survivor = new HunterInstance(null, 101) { Age = 1, IsAlive = true };
            var deadHunter = new HunterInstance(null, 102) { Age = 1, IsAlive = false };
            var settlement = new SettlementInstance();
            var management = new HunterManagementSystem(settlement, new SystemRandomSource(1));

            List<HunterAdvancementOutcome> outcomes = PlayableHunterAdvancementAdapter.ApplyAfterHunt(new[] { survivor, survivor, deadHunter }, management);

            Assert.That(outcomes, Has.Count.EqualTo(1));
            Assert.That(survivor.Age, Is.EqualTo(2));
            Assert.That(survivor.UnspentGrowth, Is.EqualTo(1));
            Assert.That(deadHunter.Age, Is.EqualTo(1));
        }

        [Test]
        public void UnspentGrowth_SurvivesJsonRoundTrip()
        {
            var source = new SettlementInstance();
            source.Hunters.Add(new HunterInstance(null, 103) { UnspentGrowth = 2, Courage = 3, Understanding = 4 });

            string json = JsonUtility.ToJson(source);
            SettlementInstance restored = JsonUtility.FromJson<SettlementInstance>(json);

            Assert.That(restored.Hunters[0].UnspentGrowth, Is.EqualTo(2));
            Assert.That(restored.Hunters[0].Courage, Is.EqualTo(3));
            Assert.That(restored.Hunters[0].Understanding, Is.EqualTo(4));
        }

        [Test]
        public void ApplyAfterHunt_RetiresOnceReturnsEquipmentAndPublishesReadableFact()
        {
            var settlement = new SettlementInstance { CurrentYear = 4 };
            var hunter = new HunterInstance(null, 104) { Name = "归盐者", Age = HunterAdvancementRules.MaximumAge, IsAlive = true };
            hunter.EquippedItemIds.Add("salt_ward");
            settlement.Hunters.Add(hunter);
            var management = new HunterManagementSystem(settlement, new SystemRandomSource(1));
            var retiredEvents = new List<HunterRetiredEvent>();
            int rosterChangedCount = 0;
            System.Action<HunterRetiredEvent> retiredHandler = retiredEvents.Add;
            System.Action<HunterRosterChangedEvent> rosterHandler = _ => rosterChangedCount++;
            EventBus.Subscribe(retiredHandler);
            EventBus.Subscribe(rosterHandler);
            try
            {
                List<HunterAdvancementOutcome> first = PlayableHunterAdvancementAdapter.ApplyAfterHunt(new[] { hunter }, management);
                List<HunterAdvancementOutcome> repeated = PlayableHunterAdvancementAdapter.ApplyAfterHunt(new[] { hunter }, management);

                Assert.That(first, Has.Count.EqualTo(1));
                Assert.That(first[0].Retired, Is.True);
                Assert.That(repeated, Is.Empty);
                Assert.That(hunter.Availability, Is.EqualTo(HunterAvailabilityState.Retired));
                Assert.That(hunter.EquippedItemIds, Is.Empty);
                Assert.That(settlement.GetStoredEquipment("salt_ward"), Is.EqualTo(1));
                Assert.That(settlement.Timeline.FindAll(entry => entry.EventId == "retirement:104:4"), Has.Count.EqualTo(1));
                Assert.That(retiredEvents, Has.Count.EqualTo(1));
                Assert.That(retiredEvents[0].HunterName, Is.EqualTo("归盐者"));
                Assert.That(retiredEvents[0].Year, Is.EqualTo(4));
                Assert.That(retiredEvents[0].ReturnedEquipmentCount, Is.EqualTo(1));
                Assert.That(rosterChangedCount, Is.EqualTo(1));
            }
            finally
            {
                EventBus.Unsubscribe(retiredHandler);
                EventBus.Unsubscribe(rosterHandler);
            }
        }
    }
}
