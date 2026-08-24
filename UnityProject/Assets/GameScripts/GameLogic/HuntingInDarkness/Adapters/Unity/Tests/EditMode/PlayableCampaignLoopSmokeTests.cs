using System.Collections.Generic;
using System.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Campaign;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableCampaignLoopSmokeTests
    {
        [Test]
        public async Task DepartureRetreatReturnAndNextDepartureAdvanceExactlyOneYear()
        {
            var settlement = new SettlementInstance { CurrentYear = 1 };
            var hunter = new HunterInstance(null, 4101) { Name = "循环猎人", IsAlive = true };
            settlement.Hunters.Add(hunter);
            var random = new FirstRandom();
            var eventSystem = new EventSystem(settlement, random);
            var timeline = new TimelineSystem(settlement, random);
            var hunterManagement = new HunterManagementSystem(settlement, random);
            HexTileData startingTile = ScriptableObject.CreateInstance<HexTileData>();
            HexTileData plainTile = ScriptableObject.CreateInstance<HexTileData>();
            startingTile.tileType = TileType.Starting;
            plainTile.tileType = TileType.Plains;
            try
            {
                using (var departureSession = new PlayableSettlementActionSession(settlement, EmptyWeaponTrainingContent.Instance, eventSystem, timeline: timeline, hunterManagement: hunterManagement))
                {
                    SettlementDepartureCommandResult departure = await departureSession.PrepareDepartureAsync(new[] { hunter.InstanceId });
                    Assert.That(departure.Succeeded, Is.True, departure.Reason);
                }

                var huntManager = new HuntManager(eventSystem, 17) { StartingTileConfig = startingTile, TilePool = { plainTile } };
                PlayableHuntActionSession huntSession = null;
                bool entered = PlayableCampaignLoopContract.TryEnterHunt(settlement, () => true, roster =>
                {
                    huntManager.OnEnter(new List<HunterInstance>(roster), settlement.CurrentYear);
                    huntSession = new PlayableHuntActionSession(huntManager);
                    return CampaignHuntEntryResult.Success();
                }, () => { }, out string rosterReason);
                Assert.That(entered, Is.True, rosterReason);
                Assert.That(settlement.DepartingHunterIds, Is.Empty);

                HuntRetreatCommandResult retreat;
                using (huntSession)
                    retreat = await huntSession.PrepareRetreatAsync(settlement.CurrentYear);
                Assert.That(retreat.Succeeded, Is.True, retreat.Reason);
                Assert.That(retreat.Record.Year, Is.EqualTo(1));
                settlement.PendingHuntReturn = retreat.Record;

                using (var returnSession = new PlayableSettlementActionSession(settlement, EmptyWeaponTrainingContent.Instance, eventSystem, timeline: timeline, hunterManagement: hunterManagement))
                {
                    SettlementHuntReturnCommandResult returned = await returnSession.ApplyHuntReturnAsync(retreat.Record);
                    Assert.That(returned.Succeeded, Is.True, returned.Reason);
                    Assert.That(returned.Applied, Is.True);
                }
                Assert.That(PlayableCampaignLoopContract.TryClearAppliedReturnCheckpoint(settlement, retreat.Record, out string checkpointReason), Is.True, checkpointReason);
                Assert.That(settlement.CurrentYear, Is.EqualTo(2));
                Assert.That(settlement.PendingHuntReturn, Is.Null);
                Assert.That(settlement.HuntHistory, Has.Count.EqualTo(1));

                using var nextDepartureSession = new PlayableSettlementActionSession(settlement, EmptyWeaponTrainingContent.Instance, eventSystem, timeline: timeline, hunterManagement: hunterManagement);
                SettlementDepartureCommandResult nextDeparture = await nextDepartureSession.PrepareDepartureAsync(new[] { hunter.InstanceId });
                Assert.That(nextDeparture.Succeeded, Is.True, nextDeparture.Reason);
                Assert.That(PlayableCampaignLoopContract.TryResolveDepartureRoster(settlement, out _, out rosterReason), Is.True, rosterReason);
            }
            finally
            {
                Object.DestroyImmediate(plainTile);
                Object.DestroyImmediate(startingTile);
            }
        }

        [Test]
        public void RuntimeDepartureGateRejectsMissingDuplicateAndUnavailableRosters()
        {
            var settlement = new SettlementInstance();
            var hunter = new HunterInstance(null, 5101) { Name = "门禁猎人", IsAlive = true };
            settlement.Hunters.Add(hunter);

            Assert.That(PlayableCampaignLoopContract.TryResolveDepartureRoster(settlement, out _, out _), Is.False);
            settlement.DepartingHunterIds = new List<int> { hunter.InstanceId, hunter.InstanceId };
            Assert.That(PlayableCampaignLoopContract.TryResolveDepartureRoster(settlement, out _, out _), Is.False);
            settlement.DepartingHunterIds = new List<int> { hunter.InstanceId };
            hunter.Availability = HunterAvailabilityState.Retired;
            Assert.That(PlayableCampaignLoopContract.TryResolveDepartureRoster(settlement, out _, out _), Is.False);
        }

        [Test]
        public void DevelopmentRosterRequiresOneToFourAvailableHunters()
        {
            var settlement = new SettlementInstance();
            Assert.That(PlayableCampaignLoopContract.TryResolveDevelopmentRoster(settlement, out _, out _), Is.False);

            for (int index = 0; index < DepartureRules.MaximumHunters; index++)
                settlement.Hunters.Add(new HunterInstance(null, 5200 + index) { Name = $"开发猎人 {index}", IsAlive = true });
            Assert.That(PlayableCampaignLoopContract.TryResolveDevelopmentRoster(settlement, out List<HunterInstance> validRoster, out string reason), Is.True, reason);
            Assert.That(validRoster, Has.Count.EqualTo(DepartureRules.MaximumHunters));

            settlement.Hunters.Add(new HunterInstance(null, 5299) { Name = "超额猎人", IsAlive = true });
            Assert.That(PlayableCampaignLoopContract.TryResolveDevelopmentRoster(settlement, out List<HunterInstance> oversizedRoster, out _), Is.False);
            Assert.That(oversizedRoster, Is.Empty);
        }

        [Test]
        public void ReturnCheckpointRejectsDifferentRecordWithoutClearingState()
        {
            var pending = new HuntRecord { RecordId = "pending", Year = 1 };
            var settlement = new SettlementInstance { PendingHuntReturn = pending, DepartingHunterIds = new List<int> { 1 } };

            bool cleared = PlayableCampaignLoopContract.TryClearAppliedReturnCheckpoint(settlement, new HuntRecord { RecordId = "other", Year = 1 }, out _);

            Assert.That(cleared, Is.False);
            Assert.That(settlement.PendingHuntReturn, Is.SameAs(pending));
            Assert.That(settlement.DepartingHunterIds, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public async Task HuntInitializationFailureRollsBackWithoutConsumingPreparedRoster()
        {
            var settlement = new SettlementInstance { CurrentYear = 4 };
            var hunter = new HunterInstance(null, 6101) { Name = "重试猎人", IsAlive = true };
            settlement.Hunters.Add(hunter);
            using (var session = new PlayableSettlementActionSession(settlement, EmptyWeaponTrainingContent.Instance))
                Assert.That((await session.PrepareDepartureAsync(new[] { hunter.InstanceId })).Succeeded, Is.True);
            bool rolledBack = false;

            bool entered = PlayableCampaignLoopContract.TryEnterHunt(settlement, () => true, _ => CampaignHuntEntryResult.Failed("测试初始化失败"), () => rolledBack = true, out string reason);

            Assert.That(entered, Is.False);
            Assert.That(reason, Does.Contain("初始化失败"));
            Assert.That(rolledBack, Is.True);
            Assert.That(settlement.DepartingHunterIds, Is.EqualTo(new[] { hunter.InstanceId }));
            Assert.That(PlayableCampaignLoopContract.TryResolveDepartureRoster(settlement, out _, out _), Is.True);
        }

        [Test]
        public async Task ReloadedPreparedRosterCannotBypassRuntimeDepartureToken()
        {
            var settlement = new SettlementInstance { CurrentYear = 2 };
            var hunter = new HunterInstance(null, 7101) { Name = "旧令牌猎人", IsAlive = true };
            settlement.Hunters.Add(hunter);
            using (var session = new PlayableSettlementActionSession(settlement, EmptyWeaponTrainingContent.Instance))
                Assert.That((await session.PrepareDepartureAsync(new[] { hunter.InstanceId })).Succeeded, Is.True);
            settlement.RuntimeDeparturePreparationToken = string.Empty;

            Assert.That(PlayableCampaignLoopContract.TryResolveDepartureRoster(settlement, out _, out _), Is.False);
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

        private sealed class FirstRandom : IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
        }
    }
}
