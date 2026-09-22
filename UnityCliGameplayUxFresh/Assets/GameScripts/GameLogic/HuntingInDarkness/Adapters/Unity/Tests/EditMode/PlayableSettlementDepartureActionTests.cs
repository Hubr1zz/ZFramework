using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using NUnit.Framework;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableSettlementDepartureActionTests
    {
        [Test]
        public async Task PrepareDepartureAsync_CommitsRosterThenPublishesPreparedFact()
        {
            SettlementInstance settlement = CreateSettlement(3);
            int[] publishedIds = null;
            Action<SettlementDeparturePreparedEvent> handler = evt =>
            {
                publishedIds = (int[])evt.HunterIds.Clone();
                evt.HunterIds[0] = 999;
            };
            EventBus.Subscribe(handler);
            try
            {
                using var session = new PlayableSettlementActionSession(settlement, EmptyWeaponTrainingContent.Instance);
                int[] requested = { settlement.Hunters[2].InstanceId, settlement.Hunters[0].InstanceId };

                SettlementDepartureCommandResult result = await session.PrepareDepartureAsync(requested);

                Assert.That(result.Succeeded, Is.True, result.Reason);
                Assert.That(settlement.DepartingHunterIds, Is.EqualTo(requested));
                Assert.That(publishedIds, Is.EqualTo(requested));
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task PrepareDepartureAsync_DuplicateHunterDoesNotMutateRosterOrPublish()
        {
            SettlementInstance settlement = CreateSettlement(1);
            settlement.DepartingHunterIds.Add(999);
            int eventCount = 0;
            Action<SettlementDeparturePreparedEvent> handler = _ => eventCount++;
            EventBus.Subscribe(handler);
            try
            {
                using var session = new PlayableSettlementActionSession(settlement, EmptyWeaponTrainingContent.Instance);
                int hunterId = settlement.Hunters[0].InstanceId;

                SettlementDepartureCommandResult result = await session.PrepareDepartureAsync(new[] { hunterId, hunterId });

                Assert.That(result.Succeeded, Is.False);
                Assert.That(settlement.DepartingHunterIds, Is.EqualTo(new[] { 999 }));
                Assert.That(eventCount, Is.Zero);
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task PrepareDepartureAsync_UnavailableHunterDoesNotMutateRoster()
        {
            SettlementInstance settlement = CreateSettlement(2);
            settlement.Hunters[1].Availability = HunterAvailabilityState.Retired;
            using var session = new PlayableSettlementActionSession(settlement, EmptyWeaponTrainingContent.Instance);

            SettlementDepartureCommandResult result = await session.PrepareDepartureAsync(new[] { settlement.Hunters[1].InstanceId });

            Assert.That(result.Succeeded, Is.False);
            Assert.That(settlement.DepartingHunterIds, Is.Empty);
        }

        [Test]
        public async Task PrepareDepartureAsync_RejectsMoreThanFourHunters()
        {
            SettlementInstance settlement = CreateSettlement(5);
            using var session = new PlayableSettlementActionSession(settlement, EmptyWeaponTrainingContent.Instance);
            var requested = new List<int>();
            foreach (HunterInstance hunter in settlement.Hunters)
                requested.Add(hunter.InstanceId);

            SettlementDepartureCommandResult result = await session.PrepareDepartureAsync(requested);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(settlement.DepartingHunterIds, Is.Empty);
        }

        private static SettlementInstance CreateSettlement(int hunterCount)
        {
            var settlement = new SettlementInstance();
            for (int index = 0; index < hunterCount; index++)
                settlement.Hunters.Add(new HunterInstance(null, index + 1) { Name = $"猎人 {index + 1}" });
            return settlement;
        }

        private sealed class EmptyWeaponTrainingContent : IWeaponTrainingContent
        {
            public static EmptyWeaponTrainingContent Instance { get; } = new();
            public string RequiredInventionId => string.Empty;
            public string CostResourceId => string.Empty;
            public int ResourceCost => 0;
            public int Experience => 0;
            public bool TryGetFamily(string masteryId, out WeaponMasteryFamilyDefinition family)
            {
                family = null;
                return false;
            }
        }
    }
}
