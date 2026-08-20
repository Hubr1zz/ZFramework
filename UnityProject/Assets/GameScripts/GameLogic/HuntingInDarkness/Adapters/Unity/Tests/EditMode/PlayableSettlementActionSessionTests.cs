using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Campaign;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.ActionFlow.Presentation;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Data;
using HuntingInDarkness.ContentTables;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableSettlementActionSessionTests
    {
        [Test]
        public async Task TrainWeaponAsync_SuccessCommitsStateThenPublishesFactsInOrder()
        {
            SettlementInstance settlement = CreateSettlement(resourceAmount: 2);
            HunterInstance hunter = settlement.Hunters[0];
            var received = new List<string>();
            Action<ResourceChangedEvent> resourceHandler = evt => received.Add($"resource:{evt.OldAmount}>{evt.NewAmount}");
            Action<WeaponMasteryChangedEvent> masteryHandler = evt => received.Add($"mastery:{evt.OldValue}>{evt.NewValue}");
            Action<SettlementTransactionCommittedEvent> committedHandler = evt => received.Add($"committed:{evt.Kind}");
            EventBus.Subscribe(resourceHandler);
            EventBus.Subscribe(masteryHandler);
            EventBus.Subscribe(committedHandler);
            try
            {
                using var session = new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent());

                WeaponTrainingCommandResult result = await session.TrainWeaponAsync(hunter.InstanceId, "mastery_blade");

                Assert.That(result.Success, Is.True);
                Assert.That(result.MasteryOutcome.NewValue, Is.EqualTo(1));
                Assert.That(settlement.GetResource("碎石"), Is.EqualTo(1));
                Assert.That(received, Is.EqualTo(new[] { "resource:2>1", "mastery:0>1", "committed:WeaponTraining" }));
            }
            finally
            {
                EventBus.Unsubscribe(resourceHandler);
                EventBus.Unsubscribe(masteryHandler);
                EventBus.Unsubscribe(committedHandler);
            }
        }

        [Test]
        public async Task TrainWeaponAsync_InsufficientResourceDoesNotPartiallyCommit()
        {
            SettlementInstance settlement = CreateSettlement(resourceAmount: 0);
            HunterInstance hunter = settlement.Hunters[0];
            int committedCount = 0;
            Action<SettlementTransactionCommittedEvent> handler = _ => committedCount++;
            EventBus.Subscribe(handler);
            try
            {
                using var session = new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent());

                WeaponTrainingCommandResult result = await session.TrainWeaponAsync(hunter.InstanceId, "mastery_blade");

                Assert.That(result.Success, Is.False);
                Assert.That(result.Reason, Is.EqualTo("训练资源不足"));
                Assert.That(settlement.GetResource("碎石"), Is.Zero);
                Assert.That(hunter.WeaponMasteries, Is.Empty);
                Assert.That(committedCount, Is.Zero);
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task BeforeReactor_CanOverrideThisTrainingTerms()
        {
            SettlementInstance settlement = CreateSettlement(resourceAmount: 1);
            HunterInstance hunter = settlement.Hunters[0];
            using var session = new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent());
            session.Reactors.RegisterGlobal(new TrainingTermsReactor(resourceCost: 0, experience: 3));

            WeaponTrainingCommandResult result = await session.TrainWeaponAsync(hunter.InstanceId, "mastery_blade");

            Assert.That(result.Success, Is.True);
            Assert.That(result.MasteryOutcome.NewValue, Is.EqualTo(3));
            Assert.That(settlement.GetResource("碎石"), Is.EqualTo(1));
        }

        [Test]
        public async Task BeforeReactor_PreventionLeavesStateAndEventsUntouched()
        {
            SettlementInstance settlement = CreateSettlement(resourceAmount: 1);
            HunterInstance hunter = settlement.Hunters[0];
            int committedCount = 0;
            Action<SettlementTransactionCommittedEvent> handler = _ => committedCount++;
            EventBus.Subscribe(handler);
            try
            {
                using var session = new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent());
                session.Reactors.RegisterGlobal(new PreventTrainingReactor());

                WeaponTrainingCommandResult result = await session.TrainWeaponAsync(hunter.InstanceId, "mastery_blade");

                Assert.That(result.Success, Is.False);
                Assert.That(result.Reason, Is.EqualTo("训练被营地规则阻止"));
                Assert.That(settlement.GetResource("碎石"), Is.EqualTo(1));
                Assert.That(hunter.WeaponMasteries, Is.Empty);
                Assert.That(committedCount, Is.Zero);
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task TrainWeaponAsync_ForeignHunterIdIsRejected()
        {
            SettlementInstance settlement = CreateSettlement(resourceAmount: 1);
            using var session = new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent());

            WeaponTrainingCommandResult result = await session.TrainWeaponAsync(999, "mastery_blade");

            Assert.That(result.Success, Is.False);
            Assert.That(settlement.GetResource("碎石"), Is.EqualTo(1));
        }

        [Test]
        public async Task TrainWeaponAsync_MaximumMasteryDoesNotSpendOrPublish()
        {
            SettlementInstance settlement = CreateSettlement(resourceAmount: 1);
            HunterInstance hunter = settlement.Hunters[0];
            hunter.WeaponMasteries.Add(new WeaponMasteryState { MasteryId = "mastery_blade", DisplayName = "刃术", Experience = int.MaxValue });
            int committedCount = 0;
            Action<SettlementTransactionCommittedEvent> handler = _ => committedCount++;
            EventBus.Subscribe(handler);
            try
            {
                using var session = new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent());

                WeaponTrainingCommandResult result = await session.TrainWeaponAsync(hunter.InstanceId, "mastery_blade");

                Assert.That(result.Success, Is.False);
                Assert.That(result.Reason, Is.EqualTo("熟练度已达到上限"));
                Assert.That(settlement.GetResource("碎石"), Is.EqualTo(1));
                Assert.That(committedCount, Is.Zero);
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task SpendHunterGrowthAsync_CommitsThroughSettlementRunner()
        {
            SettlementInstance settlement = CreateSettlement(resourceAmount: 0);
            HunterInstance hunter = settlement.Hunters[0];
            hunter.UnspentGrowth = 1;
            var received = new List<string>();
            Action<HunterGrowthSpentEvent> growthHandler = evt => received.Add($"growth:{evt.Choice}");
            Action<SettlementTransactionCommittedEvent> commitHandler = evt => received.Add($"commit:{evt.Kind}");
            EventBus.Subscribe(growthHandler);
            EventBus.Subscribe(commitHandler);
            try
            {
                using var session = new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent());

                HunterGrowthCommandResult result = await session.SpendHunterGrowthAsync(hunter.InstanceId, HunterGrowthChoice.Courage);

                Assert.That(result.Succeeded, Is.True, result.Reason);
                Assert.That(result.PreviousValue, Is.Zero);
                Assert.That(result.CurrentValue, Is.EqualTo(1));
                Assert.That(result.RemainingGrowth, Is.Zero);
                Assert.That(hunter.Courage, Is.EqualTo(1));
                Assert.That(received, Is.EqualTo(new[] { "growth:Courage", "commit:HunterGrowth" }));
            }
            finally
            {
                EventBus.Unsubscribe(growthHandler);
                EventBus.Unsubscribe(commitHandler);
            }
        }

        [Test]
        public async Task SpendHunterGrowthAsync_PreventedActionLeavesHunterUntouched()
        {
            SettlementInstance settlement = CreateSettlement(resourceAmount: 0);
            HunterInstance hunter = settlement.Hunters[0];
            hunter.UnspentGrowth = 1;
            int commitCount = 0;
            Action<SettlementTransactionCommittedEvent> handler = _ => commitCount++;
            EventBus.Subscribe(handler);
            try
            {
                using var session = new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent());
                session.Reactors.RegisterGlobal(new PreventGrowthReactor());

                HunterGrowthCommandResult result = await session.SpendHunterGrowthAsync(hunter.InstanceId, HunterGrowthChoice.Understanding);

                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Reason, Is.EqualTo("成长被营地效果阻止"));
                Assert.That(hunter.Understanding, Is.Zero);
                Assert.That(hunter.UnspentGrowth, Is.EqualTo(1));
                Assert.That(commitCount, Is.Zero);
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task ResolveEventsAsync_ResolvesInitialAndChildNodesThroughOneRoot()
        {
            SettlementInstance settlement = CreateSettlement(resourceAmount: 0);
            var eventSystem = new EventSystem(settlement, new FirstRandom());
            EventData child = CreateNarrativeEvent("child", "碎石", 2);
            EventData root = CreateNarrativeEvent("root", "碎石", 1);
            root.chainedEvents.Add(child);
            var committed = new List<SettlementTransactionKind>();
            Action<SettlementTransactionCommittedEvent> handler = evt => committed.Add(evt.Kind);
            EventBus.Subscribe(handler);
            try
            {
                using var session = new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent(), eventSystem);

                SettlementEventCommandResult result = await session.ResolveEventsAsync(new[] { root });

                Assert.That(result.Succeeded, Is.True);
                Assert.That(result.ResolvedCount, Is.EqualTo(2));
                Assert.That(settlement.GetResource("碎石"), Is.EqualTo(3));
                Assert.That(committed, Is.EqualTo(new[] { SettlementTransactionKind.EventResolution, SettlementTransactionKind.EventResolution }));
            }
            finally
            {
                EventBus.Unsubscribe(handler);
                UnityEngine.Object.DestroyImmediate(child);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public async Task ResolveEventsAsync_BloodlineActivationCommitsThroughSettlementRoot()
        {
            SettlementInstance settlement = CreateSettlement(resourceAmount: 0);
            HunterInstance hunter = settlement.Hunters[0];
            hunter.BloodlineId = "stone-listener";
            hunter.BloodlineName = "听石之血";
            EventData gameEvent = PlayableEventTableRuntime.GetEvents().First(item => item.name == "random_bloodline_awakening");
            int optionIndex = gameEvent.options.FindIndex(option => option.successEffects.Any(effect => effect.effectType == EventEffectType.ActivateBloodline && effect.targetName == hunter.BloodlineId));
            var eventSystem = new EventSystem(settlement, new FirstRandom());
            var input = new FixedChoiceInput(optionIndex, hunter);
            int commitCount = 0;
            Action<SettlementTransactionCommittedEvent> handler = evt =>
            {
                if (evt.Kind == SettlementTransactionKind.EventResolution)
                    commitCount++;
            };
            EventBus.Subscribe(handler);
            try
            {
                using var session = new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent(), eventSystem, input);

                SettlementEventCommandResult result = await session.ResolveEventsAsync(new[] { gameEvent });

                Assert.That(result.Succeeded, Is.True, result.Reason);
                Assert.That(result.ResolvedCount, Is.EqualTo(1));
                Assert.That(hunter.IsBloodlineActivated, Is.True);
                Assert.That(hunter.Traits, Contains.Item("石语者"));
                Assert.That(commitCount, Is.EqualTo(1));
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task ResolveEventsAsync_EncounterStopsRemainingNodesAndPublishesAfterCommit()
        {
            SettlementInstance settlement = CreateSettlement(resourceAmount: 0);
            var eventSystem = new EventSystem(settlement, new FirstRandom());
            EventData encounter = CreateNarrativeEvent("ambush", "碎石", 1);
            encounter.eventType = GameEventType.Combat;
            encounter.combatEncounterId = "first-showdown";
            EventData ignored = CreateNarrativeEvent("ignored", "碎石", 10);
            CampaignEncounterRequest request = default;
            int requestCount = 0;
            Action<CampaignEncounterRequestedEvent> handler = evt =>
            {
                request = evt.Request;
                requestCount++;
            };
            EventBus.Subscribe(handler);
            try
            {
                using var session = new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent(), eventSystem);

                SettlementEventCommandResult result = await session.ResolveEventsAsync(new[] { encounter, ignored });

                Assert.That(result.Succeeded, Is.True);
                Assert.That(result.EncounterRequested, Is.True);
                Assert.That(result.ResolvedCount, Is.EqualTo(1));
                Assert.That(settlement.GetResource("碎石"), Is.EqualTo(1));
                Assert.That(requestCount, Is.EqualTo(1));
                Assert.That(request.SourceSessionId, Is.EqualTo(session.SessionId));
                Assert.That(request.SourceEventId, Is.EqualTo("ambush"));
            }
            finally
            {
                EventBus.Unsubscribe(handler);
                UnityEngine.Object.DestroyImmediate(ignored);
                UnityEngine.Object.DestroyImmediate(encounter);
            }
        }

        [Test]
        public async Task ResolveEventsAsync_DisposeDuringInputDoesNotCommitPendingNode()
        {
            SettlementInstance settlement = CreateSettlement(resourceAmount: 0);
            var eventSystem = new EventSystem(settlement, new FirstRandom());
            EventData gameEvent = CreateNarrativeEvent("waiting", "碎石", 3);
            var input = new BlockingEventInput();
            int commitCount = 0;
            Action<SettlementTransactionCommittedEvent> handler = _ => commitCount++;
            EventBus.Subscribe(handler);
            var session = new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent(), eventSystem, input);
            try
            {
                UniTask<SettlementEventCommandResult> execution = session.ResolveEventsAsync(new[] { gameEvent });
                await input.Started.Task;

                session.Dispose();
                SettlementEventCommandResult result = await execution;

                Assert.That(result.Succeeded, Is.False);
                Assert.That(settlement.GetResource("碎石"), Is.Zero);
                Assert.That(commitCount, Is.Zero);
            }
            finally
            {
                session.Dispose();
                EventBus.Unsubscribe(handler);
                UnityEngine.Object.DestroyImmediate(gameEvent);
            }
        }

        [Test]
        public async Task EventEntryReactor_PreventionLeavesNodeStateUntouched()
        {
            SettlementInstance settlement = CreateSettlement(resourceAmount: 0);
            var eventSystem = new EventSystem(settlement, new FirstRandom());
            EventData gameEvent = CreateNarrativeEvent("prevented", "碎石", 3);
            int commitCount = 0;
            Action<SettlementTransactionCommittedEvent> handler = _ => commitCount++;
            EventBus.Subscribe(handler);
            try
            {
                using var session = new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent(), eventSystem);
                session.Reactors.RegisterGlobal(new PreventEventEntryReactor());

                SettlementEventCommandResult result = await session.ResolveEventsAsync(new[] { gameEvent });

                Assert.That(result.Succeeded, Is.False);
                Assert.That(settlement.GetResource("碎石"), Is.Zero);
                Assert.That(commitCount, Is.Zero);
            }
            finally
            {
                EventBus.Unsubscribe(handler);
                UnityEngine.Object.DestroyImmediate(gameEvent);
            }
        }

        [Test]
        public async Task DisposeAfterReroll_PublishesRerollCheckpointWithoutResolvingEvent()
        {
            SettlementInstance settlement = CreateSettlement(resourceAmount: 0);
            HunterInstance hunter = settlement.Hunters[0];
            hunter.Willpower = 1;
            hunter.WillpowerMax = 1;
            var eventSystem = new EventSystem(settlement, new FirstRandom());
            EventData gameEvent = ScriptableObject.CreateInstance<EventData>();
            gameEvent.name = "reroll-then-cancel";
            gameEvent.eventType = GameEventType.Choice;
            gameEvent.options.Add(new EventOption { optionText = "尝试", checkType = CheckType.Courage, checkTarget = 99 });
            var input = new RerollThenBlockInput(hunter);
            var commits = new List<SettlementTransactionKind>();
            Action<SettlementTransactionCommittedEvent> handler = evt => commits.Add(evt.Kind);
            EventBus.Subscribe(handler);
            var session = new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent(), eventSystem, input);
            try
            {
                UniTask<SettlementEventCommandResult> execution = session.ResolveEventsAsync(new[] { gameEvent });
                await input.Rerolled.Task;

                session.Dispose();
                SettlementEventCommandResult result = await execution;

                Assert.That(result.Succeeded, Is.False);
                Assert.That(hunter.Willpower, Is.Zero);
                Assert.That(commits, Is.EqualTo(new[] { SettlementTransactionKind.EventReroll }));
            }
            finally
            {
                session.Dispose();
                EventBus.Unsubscribe(handler);
                UnityEngine.Object.DestroyImmediate(gameEvent);
            }
        }

        [Test]
        public async Task EventCheck_UsesPhysicalDiceResultsForInitialRollAndReroll()
        {
            SettlementInstance settlement = CreateSettlement(resourceAmount: 0);
            HunterInstance hunter = settlement.Hunters[0];
            hunter.Courage = 0;
            hunter.Willpower = 1;
            hunter.WillpowerMax = 1;
            var eventSystem = new EventSystem(settlement, new FirstRandom());
            EventData gameEvent = ScriptableObject.CreateInstance<EventData>();
            gameEvent.name = "physical-dice-check";
            gameEvent.eventType = GameEventType.Choice;
            gameEvent.options.Add(new EventOption
            {
                optionText = "尝试",
                checkType = CheckType.Courage,
                checkTarget = 8,
                successEffects = new List<EventEffect> { new EventEffect { effectType = EventEffectType.AddResource, targetName = "碎石", value = 3 } }
            });
            var input = new RerollThenAcceptInput(hunter);
            var presenter = new FixedDicePresenter(2, 9);
            try
            {
                using var session = new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent(), eventSystem, input, randomInteractionPresenter: presenter);

                SettlementEventCommandResult result = await session.ResolveEventsAsync(new[] { gameEvent });

                Assert.That(result.Succeeded, Is.True, result.Reason);
                Assert.That(settlement.GetResource("碎石"), Is.EqualTo(3));
                Assert.That(hunter.Willpower, Is.Zero);
                Assert.That(presenter.Requests, Has.Count.EqualTo(2));
                Assert.That(presenter.Requests[0].Sides, Is.EqualTo(10));
                Assert.That(presenter.Requests[0].ActorId, Is.EqualTo(hunter.InstanceId.ToString()));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameEvent);
            }
        }

        [Test]
        public async Task EventCheck_DoesNotPresentRerollWhenActorCannotPayRerollCost()
        {
            SettlementInstance settlement = CreateSettlement(resourceAmount: 0);
            HunterInstance hunter = settlement.Hunters[0];
            hunter.Courage = 0;
            hunter.Willpower = 0;
            hunter.WillpowerMax = 1;
            var eventSystem = new EventSystem(settlement, new FirstRandom());
            EventData gameEvent = ScriptableObject.CreateInstance<EventData>();
            gameEvent.name = "no-reroll-cost";
            gameEvent.eventType = GameEventType.Choice;
            gameEvent.options.Add(new EventOption { optionText = "尝试", checkType = CheckType.Courage, checkTarget = 8 });
            var presenter = new FixedDicePresenter(2, 9);
            try
            {
                using var session = new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent(), eventSystem, new RerollThenAcceptInput(hunter), randomInteractionPresenter: presenter);

                SettlementEventCommandResult result = await session.ResolveEventsAsync(new[] { gameEvent });

                Assert.That(result.Succeeded, Is.True, result.Reason);
                Assert.That(presenter.Requests, Has.Count.EqualTo(1));
                Assert.That(hunter.Willpower, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameEvent);
            }
        }

        [Test]
        public async Task ForeignActorSelection_FallsBackToCurrentSettlementRoster()
        {
            SettlementInstance settlement = CreateSettlement(resourceAmount: 0);
            HunterInstance rosterHunter = settlement.Hunters[0];
            var foreignHunter = new HunterInstance(null, 999) { Name = "外来猎人" };
            var eventSystem = new EventSystem(settlement, new FirstRandom());
            EventData gameEvent = ScriptableObject.CreateInstance<EventData>();
            gameEvent.name = "foreign-actor";
            gameEvent.eventType = GameEventType.Choice;
            gameEvent.options.Add(new EventOption
            {
                optionText = "迎难而上",
                checkType = CheckType.Courage,
                checkTarget = 1,
                successEffects = new List<EventEffect> { new EventEffect { effectType = EventEffectType.AddCourage, targetName = "selected", value = 1 } }
            });
            try
            {
                using var session = new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent(), eventSystem, new ForeignActorInput(foreignHunter));

                SettlementEventCommandResult result = await session.ResolveEventsAsync(new[] { gameEvent });

                Assert.That(result.Succeeded, Is.True);
                Assert.That(rosterHunter.Courage, Is.EqualTo(1));
                Assert.That(foreignHunter.Courage, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameEvent);
            }
        }

        [Test]
        public async Task SelfReferencingEvent_IsCommittedOnceAndReportsDuplicate()
        {
            SettlementInstance settlement = CreateSettlement(resourceAmount: 0);
            var eventSystem = new EventSystem(settlement, new FirstRandom());
            EventData gameEvent = CreateNarrativeEvent("self-cycle", "碎石", 1);
            gameEvent.chainedEvents.Add(gameEvent);
            int preventedCount = 0;
            Action<PlayableEventDuplicatePreventedEvent> handler = _ => preventedCount++;
            EventBus.Subscribe(handler);
            try
            {
                using var session = new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent(), eventSystem);

                SettlementEventCommandResult result = await session.ResolveEventsAsync(new[] { gameEvent });

                Assert.That(result.Succeeded, Is.True);
                Assert.That(result.ResolvedCount, Is.EqualTo(1));
                Assert.That(settlement.GetResource("碎石"), Is.EqualTo(1));
                Assert.That(preventedCount, Is.EqualTo(1));
            }
            finally
            {
                EventBus.Unsubscribe(handler);
                UnityEngine.Object.DestroyImmediate(gameEvent);
            }
        }

        private static SettlementInstance CreateSettlement(int resourceAmount)
        {
            var settlement = new SettlementInstance();
            settlement.Hunters.Add(new HunterInstance(null, 7) { Name = "训练者" });
            settlement.UnlockInvention("weapon_training");
            settlement.AddResource("碎石", resourceAmount);
            return settlement;
        }

        private static EventData CreateNarrativeEvent(string id, string resourceId, int amount)
        {
            EventData gameEvent = ScriptableObject.CreateInstance<EventData>();
            gameEvent.name = id;
            gameEvent.eventName = id;
            gameEvent.immediateEffects.Add(new EventEffect { effectType = EventEffectType.AddResource, targetName = resourceId, value = amount });
            return gameEvent;
        }

        private sealed class TestWeaponTrainingContent : IWeaponTrainingContent
        {
            private readonly WeaponMasteryFamilyDefinition family = new("mastery_blade", "刃术", Array.Empty<WeaponMasteryMilestoneDefinition>());

            public string RequiredInventionId => "weapon_training";
            public string CostResourceId => "碎石";
            public int ResourceCost => 1;
            public int Experience => 1;

            public bool TryGetFamily(string masteryId, out WeaponMasteryFamilyDefinition result)
            {
                result = string.Equals(masteryId, family.Id, StringComparison.Ordinal) ? family : null;
                return result != null;
            }
        }

        private sealed class FirstRandom : HuntingInDarkness.GameCore.Foundation.IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
        }

        private sealed class BlockingEventInput : IPlayableEventInput
        {
            public UniTaskCompletionSource<bool> Started { get; } = new();

            public async UniTask ConfirmNarrativeAsync(EventData gameEvent, HunterInstance actor, CancellationToken cancellationToken)
            {
                Started.TrySetResult(true);
                await UniTask.Delay(-1, cancellationToken: cancellationToken);
            }

            public UniTask<PlayableEventChoiceSelection> SelectChoiceAsync(EventData gameEvent, HunterInstance actor, IReadOnlyList<HunterInstance> hunters, CancellationToken cancellationToken) => UniTask.FromResult(new PlayableEventChoiceSelection(-1, null));
            public UniTask<PlayableEventCheckDecision> PresentCheckAsync(PlayableEventChoiceTransaction transaction, CancellationToken cancellationToken) => UniTask.FromResult(PlayableEventCheckDecision.Accept);
            public UniTask ConfirmResultAsync(EventData gameEvent, EventResolutionResult result, CancellationToken cancellationToken) => UniTask.CompletedTask;
        }

        private sealed class FixedChoiceInput : IPlayableEventInput
        {
            private readonly int optionIndex;
            private readonly HunterInstance hunter;

            public FixedChoiceInput(int optionIndex, HunterInstance hunter)
            {
                this.optionIndex = optionIndex;
                this.hunter = hunter;
            }

            public UniTask ConfirmNarrativeAsync(EventData gameEvent, HunterInstance actor, CancellationToken cancellationToken) => UniTask.CompletedTask;
            public UniTask<PlayableEventChoiceSelection> SelectChoiceAsync(EventData gameEvent, HunterInstance actor, IReadOnlyList<HunterInstance> hunters, CancellationToken cancellationToken) => UniTask.FromResult(new PlayableEventChoiceSelection(optionIndex, hunter));
            public UniTask<PlayableEventCheckDecision> PresentCheckAsync(PlayableEventChoiceTransaction transaction, CancellationToken cancellationToken) => UniTask.FromResult(PlayableEventCheckDecision.Accept);
            public UniTask ConfirmResultAsync(EventData gameEvent, EventResolutionResult result, CancellationToken cancellationToken) => UniTask.CompletedTask;
        }

        private sealed class RerollThenBlockInput : IPlayableEventInput
        {
            private readonly HunterInstance hunter;
            private int checkCount;

            public RerollThenBlockInput(HunterInstance hunter)
            {
                this.hunter = hunter;
            }

            public UniTaskCompletionSource<bool> Rerolled { get; } = new();
            public UniTask ConfirmNarrativeAsync(EventData gameEvent, HunterInstance actor, CancellationToken cancellationToken) => UniTask.CompletedTask;
            public UniTask<PlayableEventChoiceSelection> SelectChoiceAsync(EventData gameEvent, HunterInstance actor, IReadOnlyList<HunterInstance> hunters, CancellationToken cancellationToken) => UniTask.FromResult(new PlayableEventChoiceSelection(0, hunter));

            public async UniTask<PlayableEventCheckDecision> PresentCheckAsync(PlayableEventChoiceTransaction transaction, CancellationToken cancellationToken)
            {
                if (checkCount++ == 0) return PlayableEventCheckDecision.Reroll;
                Rerolled.TrySetResult(true);
                await UniTask.Delay(-1, cancellationToken: cancellationToken);
                return PlayableEventCheckDecision.Accept;
            }

            public UniTask ConfirmResultAsync(EventData gameEvent, EventResolutionResult result, CancellationToken cancellationToken) => UniTask.CompletedTask;
        }

        private sealed class ForeignActorInput : IPlayableEventInput
        {
            private readonly HunterInstance foreignActor;

            public ForeignActorInput(HunterInstance foreignActor)
            {
                this.foreignActor = foreignActor;
            }

            public UniTask ConfirmNarrativeAsync(EventData gameEvent, HunterInstance actor, CancellationToken cancellationToken) => UniTask.CompletedTask;
            public UniTask<PlayableEventChoiceSelection> SelectChoiceAsync(EventData gameEvent, HunterInstance actor, IReadOnlyList<HunterInstance> hunters, CancellationToken cancellationToken) => UniTask.FromResult(new PlayableEventChoiceSelection(0, foreignActor));
            public UniTask<PlayableEventCheckDecision> PresentCheckAsync(PlayableEventChoiceTransaction transaction, CancellationToken cancellationToken) => UniTask.FromResult(PlayableEventCheckDecision.Accept);
            public UniTask ConfirmResultAsync(EventData gameEvent, EventResolutionResult result, CancellationToken cancellationToken) => UniTask.CompletedTask;
        }

        private sealed class RerollThenAcceptInput : IPlayableEventInput
        {
            private readonly HunterInstance hunter;
            private int checkCount;

            public RerollThenAcceptInput(HunterInstance hunter) => this.hunter = hunter;

            public UniTask ConfirmNarrativeAsync(EventData gameEvent, HunterInstance actor, CancellationToken cancellationToken) => UniTask.CompletedTask;
            public UniTask<PlayableEventChoiceSelection> SelectChoiceAsync(EventData gameEvent, HunterInstance actor, IReadOnlyList<HunterInstance> hunters, CancellationToken cancellationToken) => UniTask.FromResult(new PlayableEventChoiceSelection(0, hunter));
            public UniTask<PlayableEventCheckDecision> PresentCheckAsync(PlayableEventChoiceTransaction transaction, CancellationToken cancellationToken) => UniTask.FromResult(checkCount++ == 0 ? PlayableEventCheckDecision.Reroll : PlayableEventCheckDecision.Accept);
            public UniTask ConfirmResultAsync(EventData gameEvent, EventResolutionResult result, CancellationToken cancellationToken) => UniTask.CompletedTask;
        }

        private sealed class FixedDicePresenter : ITabletopRandomInteractionPresenter
        {
            private readonly Queue<int> values;

            public FixedDicePresenter(params int[] values) => this.values = new Queue<int>(values);

            public List<TabletopRandomInteractionRequest> Requests { get; } = new();

            public UniTask<TabletopRandomInteractionResult> PresentAsync(TabletopRandomInteractionRequest request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return UniTask.FromResult(new TabletopRandomInteractionResult(request.InteractionId, new[] { values.Dequeue() }, Array.Empty<string>()));
            }
        }

        private sealed class TrainingTermsReactor : GameActionReactor<TrainWeaponAction>
        {
            private readonly int resourceCost;
            private readonly int experience;

            public TrainingTermsReactor(int resourceCost, int experience)
            {
                this.resourceCost = resourceCost;
                this.experience = experience;
            }

            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;

            protected override void React(TrainWeaponAction action, ReactionContext context, ReactionResponse response)
            {
                action.SetResourceCost(resourceCost);
                action.SetExperience(experience);
            }
        }

        private sealed class PreventTrainingReactor : GameActionReactor<TrainWeaponAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;

            protected override void React(TrainWeaponAction action, ReactionContext context, ReactionResponse response)
            {
                response.Prevent("训练被营地规则阻止");
            }
        }

        private sealed class PreventGrowthReactor : GameActionReactor<SpendHunterGrowthAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;

            protected override void React(SpendHunterGrowthAction action, ReactionContext context, ReactionResponse response)
            {
                response.Prevent("成长被营地效果阻止");
            }
        }

        private sealed class PreventEventEntryReactor : GameActionReactor<ResolvePlayableEventNodeAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;

            protected override void React(ResolvePlayableEventNodeAction action, ReactionContext context, ReactionResponse response)
            {
                response.Prevent("事件被营地规则阻止");
            }
        }
    }
}
