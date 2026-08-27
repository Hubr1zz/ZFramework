using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CardGame.ActionQueue;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Campaign;
using HuntingInDarkness.ActionFlow.Presentation;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using NUnit.Framework;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableSettlementFacilityDutyActionTests
    {
        [Test]
        public async Task AssignFacilityDutyAsync_RejectsLastDepartureEligibleHunter()
        {
            SettlementInstance settlement = CreateSettlement(1);
            using PlayableSettlementActionSession session = CreateSession(settlement);

            SettlementFacilityDutyCommandResult result = await session.AssignFacilityDutyAsync("shelter_watch", "shelter", 1);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(settlement.FacilityDuties, Is.Empty);
        }

        [Test]
        public async Task AssignFacilityDutyAsync_PersistsCalendarIdentityAndBlocksAssignedHunter()
        {
            SettlementInstance settlement = CreateSettlement(2);
            using PlayableSettlementActionSession session = CreateSession(settlement);

            SettlementFacilityDutyCommandResult result = await session.AssignFacilityDutyAsync("shelter_watch", "shelter", 1);

            Assert.That(result.Succeeded, Is.True, result.Reason);
            Assert.That(settlement.FacilityDuties, Has.Count.EqualTo(1));
            Assert.That(settlement.FacilityDuties[0].AssignmentId, Is.Not.Empty);
            Assert.That(settlement.FacilityDuties[0].CalendarId, Is.EqualTo("test_calendar"));
            Assert.That(settlement.CanHunterDepart(1, 1, 0), Is.False);
            Assert.That(settlement.CanHunterDepart(2, 1, 0), Is.True);
        }

        [Test]
        public async Task ResolveFacilityDutyAsync_CancelledDiceLeavesPopulationAndDutyUntouched()
        {
            SettlementInstance settlement = CreateSettlement(2);
            using PlayableSettlementActionSession session = CreateSession(settlement, new DicePresenter(cancelled: true));
            Assert.That((await session.AssignFacilityDutyAsync("shelter_watch", "shelter", 1)).Succeeded, Is.True);
            string assignmentId = settlement.FacilityDuties[0].AssignmentId;
            settlement.CurrentSeasonIndex = 1;

            SettlementFacilityDutyCommandResult result = await session.ResolveFacilityDutyAsync(assignmentId);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(settlement.Population, Is.Zero);
            Assert.That(settlement.FacilityDuties, Has.Count.EqualTo(1));
            Assert.That(settlement.FacilityDuties[0].AssignmentId, Is.EqualTo(assignmentId));
        }

        [Test]
        public async Task ResolveFacilityDutyAsync_RetiredWorkerClearsWithoutPresenterOrPopulationGain()
        {
            SettlementInstance settlement = CreateSettlement(2);
            using PlayableSettlementActionSession session = CreateSession(settlement);
            Assert.That((await session.AssignFacilityDutyAsync("shelter_watch", "shelter", 1)).Succeeded, Is.True);
            string assignmentId = settlement.FacilityDuties[0].AssignmentId;
            settlement.Hunters[0].Availability = HunterAvailabilityState.Retired;
            settlement.CurrentSeasonIndex = 1;

            SettlementFacilityDutyCommandResult cancelResult = await session.CancelFacilityDutyAsync(assignmentId);
            Assert.That(cancelResult.Succeeded, Is.False);

            SettlementFacilityDutyCommandResult result = await session.ResolveFacilityDutyAsync(assignmentId);

            Assert.That(result.Succeeded, Is.True, result.Reason);
            Assert.That(settlement.Population, Is.Zero);
            Assert.That(settlement.FacilityDuties, Is.Empty);
        }

        [Test]
        public async Task ResolveFacilityDutyAsync_AppliesConfiguredRollAndReleasesDepartureGate()
        {
            SettlementInstance settlement = CreateSettlement(2);
            using PlayableSettlementActionSession session = CreateSession(settlement, new DicePresenter(6));
            Assert.That((await session.AssignFacilityDutyAsync("shelter_watch", "shelter", 1)).Succeeded, Is.True);
            string assignmentId = settlement.FacilityDuties[0].AssignmentId;
            settlement.CurrentSeasonIndex = 1;
            Assert.That(settlement.HasDueFacilityDuty(1, 1), Is.True);

            SettlementFacilityDutyCommandResult result = await session.ResolveFacilityDutyAsync(assignmentId);

            Assert.That(result.Succeeded, Is.True, result.Reason);
            Assert.That(result.Roll, Is.EqualTo(6));
            Assert.That(settlement.Population, Is.EqualTo(2));
            Assert.That(settlement.FacilityDuties, Is.Empty);
            Assert.That(settlement.GetDepartureEligibleHunters(1, 1), Has.Count.EqualTo(2));
        }

        [Test]
        public async Task FacilityDuty_ReactorPreventionLeavesAssignmentAndResolutionUntouched()
        {
            SettlementInstance settlement = CreateSettlement(2);
            using PlayableSettlementActionSession session = CreateSession(settlement, new DicePresenter(6));
            IDisposable assignPrevention = session.Reactors.RegisterGlobal(new PreventAssignReactor());

            SettlementFacilityDutyCommandResult blockedAssign = await session.AssignFacilityDutyAsync("shelter_watch", "shelter", 1);

            Assert.That(blockedAssign.Succeeded, Is.False);
            Assert.That(settlement.FacilityDuties, Is.Empty);
            assignPrevention.Dispose();
            Assert.That((await session.AssignFacilityDutyAsync("shelter_watch", "shelter", 1)).Succeeded, Is.True);
            settlement.CurrentSeasonIndex = 1;
            session.Reactors.RegisterGlobal(new PreventResolveReactor());

            SettlementFacilityDutyCommandResult blockedResolve = await session.ResolveFacilityDutyAsync(settlement.FacilityDuties[0].AssignmentId);

            Assert.That(blockedResolve.Succeeded, Is.False);
            Assert.That(settlement.Population, Is.Zero);
            Assert.That(settlement.FacilityDuties, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task ResolveFacilityDutyAsync_SessionDisposeDuringDiceDoesNotCommit()
        {
            SettlementInstance settlement = CreateSettlement(2);
            var presenter = new DeferredDicePresenter();
            var session = CreateSession(settlement, presenter);
            Assert.That((await session.AssignFacilityDutyAsync("shelter_watch", "shelter", 1)).Succeeded, Is.True);
            settlement.CurrentSeasonIndex = 1;

            UniTask<SettlementFacilityDutyCommandResult> pending = session.ResolveFacilityDutyAsync(settlement.FacilityDuties[0].AssignmentId);
            session.Dispose();
            presenter.Complete(6);

            SettlementFacilityDutyCommandResult result = await pending;

            Assert.That(result.Succeeded, Is.False);
            Assert.That(settlement.Population, Is.Zero);
            Assert.That(settlement.FacilityDuties, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task DueFacilityDuty_BlocksPreparationAndFinalCampaignRoster()
        {
            SettlementInstance settlement = CreateSettlement(2);
            using PlayableSettlementActionSession session = CreateSession(settlement);
            Assert.That((await session.AssignFacilityDutyAsync("shelter_watch", "shelter", 1)).Succeeded, Is.True);
            settlement.CurrentSeasonIndex = 1;

            SettlementDepartureCommandResult preparation = await session.PrepareDepartureAsync(new[] { 2 });
            PlayableCampaignLoopContract.CommitDepartureRoster(settlement, new[] { 2 });
            bool finalAccepted = PlayableCampaignLoopContract.TryResolveDepartureRoster(settlement, out _, out string reason);

            Assert.That(preparation.Succeeded, Is.False);
            Assert.That(finalAccepted, Is.False);
            Assert.That(reason, Does.Contain("到期"));
        }

        private static SettlementInstance CreateSettlement(int hunterCount)
        {
            var settlement = new SettlementInstance { CampaignCalendarId = "test_calendar" };
            settlement.UnlockInvention("shelter");
            for (int index = 0; index < hunterCount; index++)
                settlement.Hunters.Add(new HunterInstance(null, index + 1) { Name = $"猎人 {index + 1}" });
            return settlement;
        }

        private static PlayableSettlementActionSession CreateSession(SettlementInstance settlement, ITabletopRandomInteractionPresenter presenter = null)
        {
            var timeline = new TimelineSystem(settlement, new FirstRandom());
            Assert.That(timeline.TryBindCalendar(new CampaignCalendarDefinition("test_calendar", new[]
            {
                new SeasonDefinition("early", "早季", 0),
                new SeasonDefinition("late", "晚季", 1)
            }), out string reason), Is.True, reason);
            return new PlayableSettlementActionSession(settlement, EmptyWeaponTrainingContent.Instance, randomInteractionPresenter: presenter, timeline: timeline, facilityDuties: new[] { CreateDefinition() });
        }

        private static SettlementFacilityDutyDefinition CreateDefinition()
        {
            return new SettlementFacilityDutyDefinition("shelter_watch", "shelter", 1, SettlementFacilityDutyCheckType.PhysicalDice, new[]
            {
                new SettlementFacilityDutyPopulationBand(1, 2, 0),
                new SettlementFacilityDutyPopulationBand(3, 5, 1),
                new SettlementFacilityDutyPopulationBand(6, 6, 2)
            }, "shelter", diceCount: 1, diceSides: 6);
        }

        private sealed class DicePresenter : ITabletopRandomInteractionPresenter
        {
            private readonly int roll;
            private readonly bool cancelled;

            public DicePresenter(int roll = 0, bool cancelled = false)
            {
                this.roll = roll;
                this.cancelled = cancelled;
            }

            public UniTask<TabletopRandomInteractionResult> PresentAsync(TabletopRandomInteractionRequest request, CancellationToken cancellationToken)
            {
                IReadOnlyList<int> values = cancelled ? Array.Empty<int>() : new[] { roll };
                return UniTask.FromResult(new TabletopRandomInteractionResult(request.InteractionId, values, Array.Empty<string>(), cancelled));
            }
        }

        private sealed class DeferredDicePresenter : ITabletopRandomInteractionPresenter
        {
            private readonly UniTaskCompletionSource<TabletopRandomInteractionResult> completion = new();
            private TabletopRandomInteractionRequest request;

            public UniTask<TabletopRandomInteractionResult> PresentAsync(TabletopRandomInteractionRequest value, CancellationToken cancellationToken)
            {
                request = value;
                return completion.Task;
            }

            public void Complete(int roll) => completion.TrySetResult(new TabletopRandomInteractionResult(request.InteractionId, new[] { roll }, Array.Empty<string>(), false));
        }

        private sealed class PreventAssignReactor : GameActionReactor<AssignSettlementFacilityDutyAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            protected override void React(AssignSettlementFacilityDutyAction action, ReactionContext context, ReactionResponse response) => response.Prevent("派驻被阻止");
        }

        private sealed class PreventResolveReactor : GameActionReactor<ResolveSettlementFacilityDutyAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            protected override void React(ResolveSettlementFacilityDutyAction action, ReactionContext context, ReactionResponse response) => response.Prevent("结算被阻止");
        }

        private sealed class FirstRandom : IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
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
