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
        public async Task ApplyHuntReturnAsync_CommitsHistoryAndYearInsideSettlementRunner()
        {
            var settlement = new SettlementInstance { CurrentYear = 5, HuntsPerYear = 2 };
            var timeline = new TimelineSystem(settlement, new FirstRandom());
            using var session = new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent(), timeline: timeline);
            var record = new HuntRecord { RecordId = "runner-hunt", Year = 5 };
            bool factObservedCommittedState = false;
            int factCount = 0;
            Action<HuntCompletedEvent> handler = evt =>
            {
                factCount++;
                factObservedCommittedState = evt.AdvancedToYear == 6 && settlement.CurrentYear == 6 && settlement.HuntHistory.Count == 1;
            };
            EventBus.Subscribe(handler);

            try
            {
                SettlementHuntReturnCommandResult result = await session.ApplyHuntReturnAsync(record);
                SettlementHuntReturnCommandResult duplicate = await session.ApplyHuntReturnAsync(new HuntRecord { RecordId = "runner-hunt", Year = 5 });

                Assert.That(result.Succeeded, Is.True);
                Assert.That(duplicate.Succeeded, Is.True);
                Assert.That(factObservedCommittedState, Is.True);
                Assert.That(factCount, Is.EqualTo(1));
                Assert.That(settlement.CurrentYear, Is.EqualTo(6));
                Assert.That(settlement.HuntHistory, Has.Count.EqualTo(1));
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task ApplyHuntReturnAsync_RejectsRecordWithoutStableId()
        {
            var settlement = new SettlementInstance { CurrentYear = 5 };
            using var session = new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent());

            SettlementHuntReturnCommandResult result = await session.ApplyHuntReturnAsync(new HuntRecord { Year = 5 });

            Assert.That(result.Succeeded, Is.False);
            Assert.That(settlement.CurrentYear, Is.EqualTo(5));
            Assert.That(settlement.HuntHistory, Is.Empty);
        }

        [Test]
        public async Task ApplyHuntReturnAsync_RejectsDifferentRecordFromPastYear()
        {
            var settlement = new SettlementInstance { CurrentYear = 6 };
            using var session = new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent());

            SettlementHuntReturnCommandResult result = await session.ApplyHuntReturnAsync(new HuntRecord { RecordId = "stale-return", Year = 5 });

            Assert.That(result.Succeeded, Is.False);
            Assert.That(settlement.CurrentYear, Is.EqualTo(6));
            Assert.That(settlement.HuntHistory, Is.Empty);
        }

        [Test]
        public async Task ApplyHuntReturnAsync_CurrentSchemaCommitsResourcesGrowthAndClearsCollectibles()
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemName = "石材";
            item.ConfigureContentId("resource.stone");
            item.stackLimit = 5;
            var hunter = new HunterInstance(null, 71) { Name = "回营猎人" };
            hunter.Collectibles.Add(new ItemInstance(item));
            var settlement = new SettlementInstance { CurrentYear = 2 };
            settlement.Hunters.Add(hunter);
            settlement.AddResource("resource.stone", 10);
            PlayableSettlementItemRegistry.Configure(new[] { item });
            try
            {
                var record = new HuntRecord
                {
                    RecordId = "current-return",
                    ReturnSchemaVersion = HuntRecord.CurrentReturnSchemaVersion,
                    Year = 2,
                    HuntersDeployed = 1,
                    HuntersLost = 0,
                    ParticipantHunterIds = new List<int> { hunter.InstanceId },
                    CollectedResources = new List<string> { "resource.stone" }
                };
                using var session = new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent());

                SettlementHuntReturnCommandResult result = await session.ApplyHuntReturnAsync(record);
                SettlementHuntReturnCommandResult duplicate = await session.ApplyHuntReturnAsync(record);

                Assert.That(result.Succeeded, Is.True, result.Reason);
                Assert.That(duplicate.Succeeded, Is.True, duplicate.Reason);
                Assert.That(duplicate.Applied, Is.False);
                Assert.That(settlement.GetResource("resource.stone"), Is.EqualTo(11));
                Assert.That(hunter.Age, Is.EqualTo(2));
                Assert.That(hunter.Collectibles, Is.Empty);
                Assert.That(settlement.CurrentYear, Is.EqualTo(3));
                Assert.That(settlement.HuntHistory, Has.Count.EqualTo(1));
            }
            finally
            {
                PlayableSettlementItemRegistry.Configure(Array.Empty<ItemData>());
                UnityEngine.Object.DestroyImmediate(item);
            }
        }

        [Test]
        public async Task ApplyHuntReturnAsync_AdvancesSurvivorAndSkipsDeadParticipantExactlyOnce()
        {
            var retiringHunter = new HunterInstance(null, 73) { Name = "退休猎人", Age = HunterAdvancementRules.MaximumAge };
            var deadHunter = new HunterInstance(null, 74) { Name = "阵亡猎人", Age = 4, IsAlive = false };
            var settlement = new SettlementInstance { CurrentYear = 2 };
            settlement.Hunters.Add(retiringHunter);
            settlement.Hunters.Add(deadHunter);
            var record = new HuntRecord
            {
                RecordId = "survivor-return",
                ReturnSchemaVersion = HuntRecord.CurrentReturnSchemaVersion,
                Year = 2,
                HuntersDeployed = 2,
                HuntersLost = 1,
                ParticipantHunterIds = new List<int> { retiringHunter.InstanceId, deadHunter.InstanceId }
            };
            using var session = new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent());

            SettlementHuntReturnCommandResult result = await session.ApplyHuntReturnAsync(record);
            SettlementHuntReturnCommandResult duplicate = await session.ApplyHuntReturnAsync(record);

            Assert.That(result.Succeeded, Is.True, result.Reason);
            Assert.That(duplicate.Applied, Is.False);
            Assert.That(retiringHunter.Availability, Is.EqualTo(HunterAvailabilityState.Retired));
            Assert.That(retiringHunter.Age, Is.EqualTo(HunterAdvancementRules.MaximumAge));
            Assert.That(deadHunter.Age, Is.EqualTo(4));
            Assert.That(settlement.HuntHistory, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task ApplyHuntReturnAsync_PendingCheckpointUsesRecordResourcesAfterReload()
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemName = "草药";
            item.ConfigureContentId("resource.herb");
            item.stackLimit = 5;
            var hunter = new HunterInstance(null, 72) { Name = "恢复猎人" };
            var settlement = new SettlementInstance { CurrentYear = 2 };
            settlement.Hunters.Add(hunter);
            PlayableSettlementItemRegistry.Configure(new[] { item });
            try
            {
                var record = new HuntRecord
                {
                    RecordId = "pending-return",
                    ReturnSchemaVersion = HuntRecord.CurrentReturnSchemaVersion,
                    Year = 2,
                    HuntersDeployed = 1,
                    ParticipantHunterIds = new List<int> { hunter.InstanceId },
                    CollectedResources = new List<string> { "resource.herb" }
                };
                settlement.PendingHuntReturn = record;
                using var session = new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent());

                SettlementHuntReturnCommandResult result = await session.ApplyHuntReturnAsync(record);

                Assert.That(result.Succeeded, Is.True, result.Reason);
                Assert.That(settlement.GetResource("resource.herb"), Is.EqualTo(1));
                Assert.That(hunter.Age, Is.EqualTo(2));
                Assert.That(settlement.HuntHistory, Has.Count.EqualTo(1));
            }
            finally
            {
                PlayableSettlementItemRegistry.Configure(Array.Empty<ItemData>());
                UnityEngine.Object.DestroyImmediate(item);
            }
        }

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
                Assert.That(settlement.HasPendingEventChainOccurrences, Is.False);
            }
            finally
            {
                EventBus.Unsubscribe(handler);
                UnityEngine.Object.DestroyImmediate(child);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public async Task ResolveEventsAsync_PersistsAndResolvesTwoSameIdOccurrencesIndependently()
        {
            SettlementInstance settlement = CreateSettlement(resourceAmount: 0);
            var eventSystem = new EventSystem(settlement, new FirstRandom());
            EventData child = CreateNarrativeEvent("same-child", "碎石", 1);
            EventData root = CreateNarrativeEvent("same-root", "碎石", 1);
            root.chainedEvents.Add(child);
            root.chainedEvents.Add(child);

            using var session = new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent(), eventSystem);
            SettlementEventCommandResult result = await session.ResolveEventsAsync(new[] { root });

            Assert.That(result.Succeeded, Is.True, result.Reason);
            Assert.That(result.ResolvedCount, Is.EqualTo(3));
            Assert.That(settlement.GetResource("碎石"), Is.EqualTo(3));
            Assert.That(settlement.HasPendingEventChainOccurrences, Is.False);
        }

        [Test]
        public async Task ResolveEventsAsync_PersistsChildrenFromMultipleInitialRoots()
        {
            SettlementInstance settlement = CreateSettlement(resourceAmount: 0);
            var eventSystem = new EventSystem(settlement, new FirstRandom());
            EventData firstChild = CreateNarrativeEvent("first-root-child", "first-root-resource", 1);
            EventData secondChild = CreateNarrativeEvent("second-root-child", "second-root-resource", 1);
            EventData firstRoot = CreateNarrativeEvent("first-root", "first-root-resource", 1);
            EventData secondRoot = CreateNarrativeEvent("second-root", "second-root-resource", 1);
            firstRoot.chainedEvents.Add(firstChild);
            secondRoot.chainedEvents.Add(secondChild);

            try
            {
                using var session = new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent(), eventSystem);

                SettlementEventCommandResult result = await session.ResolveEventsAsync(new[] { firstRoot, secondRoot });

                Assert.That(result.Succeeded, Is.True, result.Reason);
                Assert.That(result.ResolvedCount, Is.EqualTo(4));
                Assert.That(settlement.GetResource("first-root-resource"), Is.EqualTo(2));
                Assert.That(settlement.GetResource("second-root-resource"), Is.EqualTo(2));
                Assert.That(settlement.HasPendingEventChainOccurrences, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstChild);
                UnityEngine.Object.DestroyImmediate(secondChild);
                UnityEngine.Object.DestroyImmediate(firstRoot);
                UnityEngine.Object.DestroyImmediate(secondRoot);
            }
        }

        [Test]
        public async Task ResolveEventsAsync_DoesNotExecuteChildrenRejectedByCheckpointLimit()
        {
            SettlementInstance settlement = CreateSettlement(resourceAmount: 0);
            var eventSystem = new EventSystem(settlement, new FirstRandom());
            EventData root = ScriptableObject.CreateInstance<EventData>();
            root.name = "overflow-root";
            root.eventName = root.name;
            var children = new List<EventData>();
            for (int index = 0; index < SettlementInstance.MaxPendingEventChainOccurrences + 1; index++)
            {
                EventData child = CreateNarrativeEvent($"overflow-child-{index}", $"overflow-resource-{index}", 1);
                children.Add(child);
                root.chainedEvents.Add(child);
            }

            try
            {
                using var session = new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent(), eventSystem);

                SettlementEventCommandResult result = await session.ResolveEventsAsync(new[] { root });

                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Reason, Does.Contain("上限"));
                Assert.That(result.ResolvedCount, Is.EqualTo(1));
                Assert.That(settlement.GetResource("overflow-resource-0"), Is.Zero);
                Assert.That(settlement.GetResource("overflow-resource-63"), Is.Zero);
                Assert.That(settlement.GetResource("overflow-resource-64"), Is.Zero);
                Assert.That(settlement.HasPendingEventChainOccurrences, Is.True);
                Assert.That(settlement.PendingEventChains, Has.Count.EqualTo(1));
                Assert.That(settlement.PendingEventChains[0].PendingOccurrences, Has.Count.EqualTo(SettlementInstance.MaxPendingEventChainOccurrences));
                Assert.That(settlement.PendingEventChains[0].Diagnostic, Does.Contain("上限"));
                Assert.That(settlement.PendingEventChains[0].PendingOccurrences[^1].EventId, Is.EqualTo("overflow-child-63"));
            }
            finally
            {
                foreach (EventData child in children)
                    UnityEngine.Object.DestroyImmediate(child);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public async Task ResolveEventsAsync_ResultConfirmationFailureKeepsChildCheckpoint()
        {
            SettlementInstance settlement = CreateSettlement(resourceAmount: 0);
            var eventSystem = new EventSystem(settlement, new FirstRandom());
            HunterInstance hunter = settlement.Hunters[0];
            EventData child = CreateNarrativeEvent("confirm-child", "碎石", 2);
            EventData root = ScriptableObject.CreateInstance<EventData>();
            root.name = "confirm-root";
            root.eventType = GameEventType.Choice;
            root.options.Add(new EventOption { optionText = "确认", successChain = new List<EventData> { child } });
            var input = new ThrowingResultConfirmationInput(hunter);

            using var session = new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent(), eventSystem, input);
            SettlementEventCommandResult result = await session.ResolveEventsAsync(new[] { root });

            Assert.That(result.Succeeded, Is.False);
            Assert.That(settlement.HasPendingEventChainOccurrences, Is.True);
            Assert.That(settlement.PendingEventChains[0].PendingOccurrences[0].EventId, Is.EqualTo("confirm-child"));
        }

        [Test]
        public async Task ResolveEventsAsync_ReportsPartialEffectResultsAtCommandBoundary()
        {
            SettlementInstance settlement = CreateSettlement(resourceAmount: 0);
            var eventSystem = new EventSystem(settlement, new FirstRandom());
            EventData gameEvent = CreateNarrativeEvent("partial-settlement", "碎石", 1);
            gameEvent.immediateEffects.Add(new EventEffect { effectType = EventEffectType.UnlockInvention, targetName = "missing-invention" });
            gameEvent.immediateEffects.Add(new EventEffect { effectType = EventEffectType.AddCourage, targetName = "all", value = 1 });

            try
            {
                using var session = new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent(), eventSystem);

                SettlementEventCommandResult result = await session.ResolveEventsAsync(new[] { gameEvent });

                Assert.That(result.Succeeded, Is.True);
                Assert.That(result.FailedEffectCount, Is.EqualTo(1));
                Assert.That(result.EffectResults.AppliedCount, Is.EqualTo(2));
                Assert.That(settlement.GetResource("碎石"), Is.EqualTo(1));
                Assert.That(settlement.Hunters[0].Courage, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameEvent);
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
        public async Task EventCheck_UsesConfiguredCardInteractionInsideSettlementRoot()
        {
            SettlementInstance settlement = CreateSettlement(resourceAmount: 0);
            HunterInstance hunter = settlement.Hunters[0];
            hunter.Understanding = 0;
            EventData gameEvent = PlayableEventTableRuntime.GetEvents().First(item => item.name == "random_bone_omens");
            var presenter = new FixedCardPresenter(9);
            using var session = new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent(), new EventSystem(settlement, new FirstRandom()), new FixedChoiceInput(0, hunter), randomInteractionPresenter: presenter);

            SettlementEventCommandResult result = await session.ResolveEventsAsync(new[] { gameEvent });

            Assert.That(result.Succeeded, Is.True, result.Reason);
            Assert.That(settlement.GetResource("碎石"), Is.EqualTo(2));
            Assert.That(hunter.Understanding, Is.EqualTo(1));
            Assert.That(presenter.Requests, Has.Count.EqualTo(1));
            Assert.That(presenter.Requests[0].Kind, Is.EqualTo(TabletopRandomInteractionKind.FlipCards));
            Assert.That(presenter.Requests[0].DeckId, Is.EqualTo("bone-omens"));
            Assert.That(presenter.Requests[0].ActorId, Is.EqualTo(hunter.InstanceId.ToString()));
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

        [Test]
        public async Task EventKillHunter_UsesSharedDeathTransactionInsideSettlementRoot()
        {
            var manager = new SettlementManager(3);
            var victim = new HunterInstance(null, 31) { Name = "交易者", Age = 3 };
            victim.EquippedItemNames.Add("旧护符");
            var survivor = new HunterInstance(null, 32) { Name = "守望者", Age = 2 };
            manager.Data.Hunters.Add(victim);
            manager.Data.Hunters.Add(survivor);
            EventData gameEvent = ScriptableObject.CreateInstance<EventData>();
            gameEvent.name = "event-death";
            gameEvent.eventType = GameEventType.Choice;
            gameEvent.options.Add(new EventOption
            {
                optionText = "履行交易",
                successEffects = new List<EventEffect> { new EventEffect { effectType = EventEffectType.KillHunter, targetName = "dark_bargain", description = "履行了黑暗交易" } }
            });
            HunterDiedEvent diedEvent = default;
            Action<HunterDiedEvent> handler = evt => diedEvent = evt;
            var input = new FixedChoiceInput(0, victim);
            EventBus.Subscribe(handler);
            try
            {
                using var session = new PlayableSettlementActionSession(manager.Data, new TestWeaponTrainingContent(), manager.Events, input);

                SettlementEventCommandResult result = await session.ResolveEventsAsync(new[] { gameEvent });

                Assert.That(result.Succeeded, Is.True, result.Reason);
                Assert.That(victim.IsAlive, Is.False);
                Assert.That(victim.EquippedItemNames, Is.Empty);
                Assert.That(manager.Data.GetStoredEquipment("旧护符"), Is.EqualTo(1));
                Assert.That(manager.Data.Timeline.FindAll(entry => entry.EventId == "death:31"), Has.Count.EqualTo(1));
                Assert.That(survivor.UnspentGrowth, Is.EqualTo(1));
                Assert.That(diedEvent.CauseId, Is.EqualTo("dark_bargain"));
                Assert.That(input.ResultConfirmationCount, Is.EqualTo(1));
            }
            finally
            {
                EventBus.Unsubscribe(handler);
                UnityEngine.Object.DestroyImmediate(gameEvent);
            }
        }

        [Test]
        public async Task EventKillHunter_LastHunterSkipsResultPromptForGameOverOwnership()
        {
            var manager = new SettlementManager(4);
            var victim = new HunterInstance(null, 33) { Name = "最后的守火者", Age = 3 };
            manager.Data.Hunters.Add(victim);
            EventData gameEvent = ScriptableObject.CreateInstance<EventData>();
            gameEvent.name = "last-hunter-event-death";
            gameEvent.eventType = GameEventType.Choice;
            gameEvent.options.Add(new EventOption
            {
                optionText = "走入黑暗",
                successEffects = new List<EventEffect> { new EventEffect { effectType = EventEffectType.KillHunter, targetName = "dark_bargain", description = "履行了最后的交易" } }
            });
            EventData chainedEvent = ScriptableObject.CreateInstance<EventData>();
            chainedEvent.name = "must-not-run-after-game-over";
            gameEvent.options[0].successChain.Add(chainedEvent);
            var input = new FixedChoiceInput(0, victim);
            try
            {
                using var session = new PlayableSettlementActionSession(manager.Data, new TestWeaponTrainingContent(), manager.Events, input);

                SettlementEventCommandResult result = await session.ResolveEventsAsync(new[] { gameEvent });

                Assert.That(result.Succeeded, Is.True, result.Reason);
                Assert.That(result.ResolvedCount, Is.EqualTo(1));
                Assert.That(victim.IsAlive, Is.False);
                Assert.That(input.ResultConfirmationCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(chainedEvent);
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

            public int ResultConfirmationCount { get; private set; }

            public UniTask ConfirmNarrativeAsync(EventData gameEvent, HunterInstance actor, CancellationToken cancellationToken) => UniTask.CompletedTask;
            public UniTask<PlayableEventChoiceSelection> SelectChoiceAsync(EventData gameEvent, HunterInstance actor, IReadOnlyList<HunterInstance> hunters, CancellationToken cancellationToken) => UniTask.FromResult(new PlayableEventChoiceSelection(optionIndex, hunter));
            public UniTask<PlayableEventCheckDecision> PresentCheckAsync(PlayableEventChoiceTransaction transaction, CancellationToken cancellationToken) => UniTask.FromResult(PlayableEventCheckDecision.Accept);
            public UniTask ConfirmResultAsync(EventData gameEvent, EventResolutionResult result, CancellationToken cancellationToken)
            {
                ResultConfirmationCount++;
                return UniTask.CompletedTask;
            }
        }

        private sealed class ThrowingResultConfirmationInput : IPlayableEventInput
        {
            private readonly HunterInstance hunter;

            public ThrowingResultConfirmationInput(HunterInstance hunter)
            {
                this.hunter = hunter;
            }

            public UniTask ConfirmNarrativeAsync(EventData gameEvent, HunterInstance actor, CancellationToken cancellationToken) => UniTask.CompletedTask;
            public UniTask<PlayableEventChoiceSelection> SelectChoiceAsync(EventData gameEvent, HunterInstance actor, IReadOnlyList<HunterInstance> hunters, CancellationToken cancellationToken) => UniTask.FromResult(new PlayableEventChoiceSelection(0, hunter));
            public UniTask<PlayableEventCheckDecision> PresentCheckAsync(PlayableEventChoiceTransaction transaction, CancellationToken cancellationToken) => UniTask.FromResult(PlayableEventCheckDecision.Accept);
            public UniTask ConfirmResultAsync(EventData gameEvent, EventResolutionResult result, CancellationToken cancellationToken) => UniTask.FromException(new InvalidOperationException("测试确认失败"));
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

        private sealed class FixedCardPresenter : ITabletopRandomInteractionPresenter
        {
            private readonly int value;

            public FixedCardPresenter(int value) => this.value = value;

            public List<TabletopRandomInteractionRequest> Requests { get; } = new();

            public UniTask<TabletopRandomInteractionResult> PresentAsync(TabletopRandomInteractionRequest request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return UniTask.FromResult(new TabletopRandomInteractionResult(request.InteractionId, new[] { value }, new[] { $"{request.DeckId}:card-{value}" }));
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
