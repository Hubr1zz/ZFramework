using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CardGame.ActionQueue;
using Core;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using NUnit.Framework;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableSettlementSymptomActionTests
    {
        private static readonly SymptomDefinition cowardice = new("symptom_cowardice", "胆怯", "面对黑暗时会本能退缩。", new SymptomStatModifiers(-1, 0, 0, 0), new SymptomStatModifiers(0, 0, 1, 0), 2, 1, 2, 1);

        [Test]
        public async Task Internalize_CommitsThroughSettlementRunnerAndPublishesFactsInOrder()
        {
            SettlementInstance settlement = CreateSettlement();
            HunterInstance hunter = settlement.Hunters[0];
            var received = new List<string>();
            Action<HunterSymptomResolvedEvent> symptomHandler = evt => received.Add($"symptom:{evt.Choice}");
            Action<SettlementTransactionCommittedEvent> commitHandler = evt => received.Add($"commit:{evt.Kind}");
            EventBus.Subscribe(symptomHandler);
            EventBus.Subscribe(commitHandler);
            try
            {
                using var session = CreateSession(settlement);

                HunterSymptomCommandResult result = await session.ResolveHunterSymptomAsync(hunter.InstanceId, cowardice.Id, SymptomResolutionChoice.Internalize);

                Assert.That(result.Succeeded, Is.True, result.Reason);
                Assert.That(result.PreviousProgress, Is.Zero);
                Assert.That(result.CurrentProgress, Is.EqualTo(1));
                Assert.That(hunter.Willpower, Is.EqualTo(2));
                Assert.That(received, Is.EqualTo(new[] { "symptom:Internalize", "commit:Symptom" }));
            }
            finally
            {
                EventBus.Unsubscribe(symptomHandler);
                EventBus.Unsubscribe(commitHandler);
            }
        }

        [Test]
        public async Task BeforeReactor_CanRewriteInternalizeIntoOvercome()
        {
            SettlementInstance settlement = CreateSettlement();
            HunterInstance hunter = settlement.Hunters[0];
            hunter.Courage = 2;
            hunter.UnspentGrowth = 1;
            using var session = CreateSession(settlement);
            session.Reactors.RegisterGlobal(new OvercomeInsteadReactor());

            HunterSymptomCommandResult result = await session.ResolveHunterSymptomAsync(hunter.InstanceId, cowardice.Id, SymptomResolutionChoice.Internalize);

            Assert.That(result.Succeeded, Is.True, result.Reason);
            Assert.That(result.Choice, Is.EqualTo(SymptomResolutionChoice.Overcome));
            Assert.That(result.IsOvercome, Is.True);
            Assert.That(hunter.Willpower, Is.EqualTo(3));
            Assert.That(hunter.UnspentGrowth, Is.Zero);
        }

        [Test]
        public async Task BeforeReactor_PreventionLeavesHunterAndFactsUntouched()
        {
            SettlementInstance settlement = CreateSettlement();
            HunterInstance hunter = settlement.Hunters[0];
            int commitCount = 0;
            Action<SettlementTransactionCommittedEvent> handler = _ => commitCount++;
            EventBus.Subscribe(handler);
            try
            {
                using var session = CreateSession(settlement);
                session.Reactors.RegisterGlobal(new PreventSymptomReactor());

                HunterSymptomCommandResult result = await session.ResolveHunterSymptomAsync(hunter.InstanceId, cowardice.Id, SymptomResolutionChoice.Internalize);

                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Reason, Is.EqualTo("症状被营地效果阻止"));
                Assert.That(hunter.Willpower, Is.EqualTo(3));
                Assert.That(HunterSymptomRules.Find(hunter, cowardice.Id).InternalizationProgress, Is.Zero);
                Assert.That(commitCount, Is.Zero);
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task UnknownSymptomDoesNotMutateHunter()
        {
            SettlementInstance settlement = CreateSettlement();
            HunterInstance hunter = settlement.Hunters[0];
            using var session = CreateSession(settlement);

            HunterSymptomCommandResult result = await session.ResolveHunterSymptomAsync(hunter.InstanceId, "missing", SymptomResolutionChoice.Internalize);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Reason, Is.EqualTo("症状内容尚未配置。"));
            Assert.That(hunter.Willpower, Is.EqualTo(3));
        }

        [Test]
        public async Task SecondInternalizeInSameYearDoesNotSpendAgain()
        {
            SettlementInstance settlement = CreateSettlement();
            HunterInstance hunter = settlement.Hunters[0];
            using var session = CreateSession(settlement);

            HunterSymptomCommandResult first = await session.ResolveHunterSymptomAsync(hunter.InstanceId, cowardice.Id, SymptomResolutionChoice.Internalize);
            HunterSymptomCommandResult second = await session.ResolveHunterSymptomAsync(hunter.InstanceId, cowardice.Id, SymptomResolutionChoice.Internalize);

            Assert.That(first.Succeeded, Is.True, first.Reason);
            Assert.That(second.Succeeded, Is.False);
            Assert.That(second.Reason, Is.EqualTo("本年已经面对过这一症状。"));
            Assert.That(hunter.Willpower, Is.EqualTo(2));
            Assert.That(HunterSymptomRules.Find(hunter, cowardice.Id).InternalizationProgress, Is.EqualTo(1));
        }

        private static SettlementInstance CreateSettlement()
        {
            var settlement = new SettlementInstance { CurrentYear = 1 };
            var hunter = new HunterInstance(null, 7) { Name = "见证者", Willpower = 3, WillpowerMax = 3 };
            hunter.Stats.strength = 2;
            HunterSymptomRules.Register(hunter, cowardice);
            settlement.Hunters.Add(hunter);
            return settlement;
        }

        private static PlayableSettlementActionSession CreateSession(SettlementInstance settlement)
        {
            return new PlayableSettlementActionSession(settlement, EmptyWeaponTrainingContent.Instance, symptomContent: new TestSymptomContent());
        }

        private sealed class TestSymptomContent : ISettlementSymptomContent
        {
            public IReadOnlyList<SymptomDefinition> GetDefinitions() => new[] { cowardice };

            public bool TryGetById(string symptomId, out SymptomDefinition definition)
            {
                definition = string.Equals(symptomId, cowardice.Id, StringComparison.Ordinal) ? cowardice : null;
                return definition != null;
            }
        }

        private sealed class EmptyWeaponTrainingContent : IWeaponTrainingContent
        {
            public static EmptyWeaponTrainingContent Instance { get; } = new();
            public string RequiredInventionId => string.Empty;
            public string CostResourceId => string.Empty;
            public int ResourceCost => 0;
            public int Experience => 1;
            public bool TryGetFamily(string masteryId, out WeaponMasteryFamilyDefinition family)
            {
                family = null;
                return false;
            }
        }

        private sealed class OvercomeInsteadReactor : GameActionReactor<ResolveHunterSymptomAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            protected override void React(ResolveHunterSymptomAction action, ReactionContext context, ReactionResponse response) => action.SetChoice(SymptomResolutionChoice.Overcome);
        }

        private sealed class PreventSymptomReactor : GameActionReactor<ResolveHunterSymptomAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            protected override void React(ResolveHunterSymptomAction action, ReactionContext context, ReactionResponse response) => response.Prevent("症状被营地效果阻止");
        }
    }
}
