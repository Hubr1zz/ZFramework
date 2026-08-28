using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.ActionFlow.Campaign;
using HuntingInDarkness.ActionFlow.Presentation;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Hunt;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableHuntActionSessionTests
    {
        [Test]
        public async Task InteractTileAsync_RevealCommitsStateThenPublishesFact()
        {
            using var rig = new HuntRig();
            HexTileInstance target = rig.FirstInteractable;
            HuntTileInteractionCommittedEvent received = default;
            int receivedCount = 0;
            Action<HuntTileInteractionCommittedEvent> handler = evt =>
            {
                received = evt;
                receivedCount++;
            };
            EventBus.Subscribe(handler);
            try
            {
                HuntTileCommandResult result = await rig.Session.InteractTileAsync(target.AxialCoord);

                Assert.That(result.Succeeded, Is.True);
                Assert.That(result.Commit.Kind, Is.EqualTo(HuntTileInteractionKind.Reveal));
                Assert.That(target.State, Is.EqualTo(TileState.Revealed));
                Assert.That(receivedCount, Is.EqualTo(1));
                Assert.That(received.Coordinate, Is.EqualTo(target.AxialCoord));
                Assert.That(received.Kind, Is.EqualTo(HuntTileInteractionKind.Reveal));
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task InteractTileAsync_EventWoundCommitsThroughHuntRoot()
        {
            using var rig = new HuntRig(includeSurvivor: true);
            rig.TileEvent.immediateEffects.Add(new EventEffect { effectType = EventEffectType.AddRecoverableWound, targetName = "selected", bodyPart = "legs", value = 1 });
            HunterWoundedEvent received = default;
            int receivedCount = 0;
            Action<HunterWoundedEvent> handler = evt =>
            {
                received = evt;
                receivedCount++;
            };
            EventBus.Subscribe(handler);
            try
            {
                HuntTileCommandResult result = await rig.Session.InteractTileAsync(rig.FirstInteractable.AxialCoord);

                Assert.That(result.Succeeded, Is.True, result.Reason);
                Assert.That(rig.Hunter.HP.legs, Is.EqualTo(2));
                Assert.That(receivedCount, Is.EqualTo(1));
                Assert.That(received.HunterId, Is.EqualTo(rig.Hunter.InstanceId));
                Assert.That(received.BodyPartId, Is.EqualTo("legs"));
                Assert.That(received.PreviousHealth, Is.EqualTo(3));
                Assert.That(received.CurrentHealth, Is.EqualTo(2));
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task InteractTileAsync_HuntWorldEffectExhaustsCurrentTileAndNotifiesOnce()
        {
            using var rig = new HuntRig(includeResourcePoints: true);
            rig.TileEvent.immediateEffects.Add(new EventEffect { effectType = EventEffectType.ExhaustCurrentHuntTileResources, value = 0 });
            HexTileInstance target = rig.FirstInteractable;
            int stateChangedCount = 0;
            Vector2Int changedCoordinate = default;
            rig.Manager.OnResourcePointStateChanged = coordinate =>
            {
                stateChangedCount++;
                changedCoordinate = coordinate;
            };

            HuntTileCommandResult result = await rig.Session.InteractTileAsync(target.AxialCoord);

            Assert.That(result.Succeeded, Is.True, result.Reason);
            Assert.That(target.ResourcePoints, Has.Count.EqualTo(2));
            Assert.That(target.ResourcePoints.All(point => point.IsExhausted), Is.True);
            Assert.That(stateChangedCount, Is.EqualTo(1));
            Assert.That(changedCoordinate, Is.EqualTo(target.AxialCoord));
            Assert.That(result.EffectResults.Effects[0].ResolvedTargetId, Is.EqualTo($"{target.AxialCoord.x},{target.AxialCoord.y}"));
            Assert.That(result.EffectResults.Effects[0].StateChanged, Is.True);
            Assert.That(result.EffectResults.Effects[0].PreviousValue, Is.Zero);
            Assert.That(result.EffectResults.Effects[0].CurrentValue, Is.EqualTo(2));

            MethodInfo exhaustMethod = typeof(HuntManager).GetMethod("TryExhaustEventTileResourcePoints", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(exhaustMethod, Is.Not.Null);
            object[] exhaustArguments = { result.Commit, null, null };
            bool repeated = (bool)exhaustMethod.Invoke(rig.Manager, exhaustArguments);
            PlayableEventWorldChange repeatedChange = (PlayableEventWorldChange)exhaustArguments[1];
            string repeatedReason = exhaustArguments[2] as string;
            Assert.That(repeated, Is.True, repeatedReason);
            Assert.That(repeatedChange.Changed, Is.False);
            Assert.That(repeatedChange.AffectedCount, Is.Zero);
            Assert.That(stateChangedCount, Is.EqualTo(1));
        }

        [Test]
        public async Task CommitReactor_PreventionLeavesTileAndFactsUntouched()
        {
            using var rig = new HuntRig();
            HexTileInstance target = rig.FirstInteractable;
            int receivedCount = 0;
            Action<HuntTileInteractionCommittedEvent> handler = _ => receivedCount++;
            EventBus.Subscribe(handler);
            try
            {
                rig.Session.Reactors.RegisterGlobal(new PreventCommitReactor());

                HuntTileCommandResult result = await rig.Session.InteractTileAsync(target.AxialCoord);

                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Reason, Is.EqualTo("测试规则阻止地块提交"));
                Assert.That(target.State, Is.EqualTo(TileState.Interactable));
                Assert.That(receivedCount, Is.Zero);
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task EventReactor_PreventionKeepsRevealAndPreservesPendingOccurrence()
        {
            using var rig = new HuntRig(includeResourcePoints: true);
            HexTileInstance target = rig.FirstInteractable;
            rig.TileEvent.immediateEffects.Add(new EventEffect { effectType = EventEffectType.ExhaustCurrentHuntTileResources, value = 0 });
            int triggeredCount = 0;
            int committedCount = 0;
            int stateChangedCount = 0;
            Action<GameEventTriggeredEvent> triggeredHandler = evt =>
            {
                if (evt.EventId == rig.TileEvent.name)
                    triggeredCount++;
            };
            Action<HuntEventNodeCommittedEvent> committedHandler = _ => committedCount++;
            rig.Manager.OnResourcePointStateChanged = _ => stateChangedCount++;
            EventBus.Subscribe(triggeredHandler);
            EventBus.Subscribe(committedHandler);
            try
            {
                IDisposable prevention = rig.Session.Reactors.RegisterGlobal(new PreventEventNodeReactor());

                HuntTileCommandResult result = await rig.Session.InteractTileAsync(target.AxialCoord);

                Assert.That(result.Succeeded, Is.False);
                Assert.That(target.State, Is.EqualTo(TileState.Revealed));
                Assert.That(triggeredCount, Is.Zero);
                Assert.That(committedCount, Is.Zero);
                Assert.That(rig.Session.HasPendingEventOccurrences, Is.True);
                Assert.That(target.ResourcePoints.All(point => !point.IsExhausted), Is.True);
                Assert.That(stateChangedCount, Is.Zero);

                prevention.Dispose();
                HuntTileCommandResult resumed = await rig.Session.InteractTileAsync(target.AxialCoord);

                Assert.That(resumed.Succeeded, Is.True, resumed.Reason);
                Assert.That(rig.Session.HasPendingEventOccurrences, Is.False);
                Assert.That(triggeredCount, Is.EqualTo(1));
                Assert.That(committedCount, Is.EqualTo(1));
                Assert.That(target.ResourcePoints.All(point => point.IsExhausted), Is.True);
                Assert.That(stateChangedCount, Is.EqualTo(1));
            }
            finally
            {
                EventBus.Unsubscribe(triggeredHandler);
                EventBus.Unsubscribe(committedHandler);
            }
        }

        [Test]
        public async Task InteractTileAsync_ExplicitUnavailableChoiceFailsAndKeepsOccurrence()
        {
            using var rig = new HuntRig();
            rig.TileEvent.eventType = GameEventType.Choice;
            rig.TileEvent.options.Add(new EventOption
            {
                optionText = "安全方案",
                alwaysAvailable = false,
                conditions = new List<EventOptionCondition>
                {
                    new() { conditionKind = EventOptionConditionKind.MinimumResource, key = rig.Resource.ContentId, value = 1 }
                }
            });
            rig.TileEvent.options.Add(new EventOption
            {
                optionText = "危险方案",
                alwaysAvailable = true,
                successEffects = new List<EventEffect>
                {
                    new() { effectType = EventEffectType.AddResource, targetName = rig.Resource.ContentId, value = 9 }
                }
            });
            var input = new ExplicitChoiceInput(0);
            rig.Manager.EventInput = input;

            HuntTileCommandResult result = await rig.Session.InteractTileAsync(rig.FirstInteractable.AxialCoord);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Reason, Does.Contain("条件"));
            Assert.That(input.SelectionCount, Is.EqualTo(1));
            Assert.That(rig.Session.HasPendingEventOccurrences, Is.True);
            Assert.That(rig.Hunter.Collectibles, Is.Empty);
        }

        [Test]
        public async Task InteractTileAsync_CarriedItemOptionConsumesOnlyActorItemAndPublishesChange()
        {
            using var rig = new HuntRig(includeSurvivor: true);
            rig.Hunter.Collectibles.Add(new ItemInstance(rig.RewardItem, 1));
            rig.Survivor.Collectibles.Add(new ItemInstance(rig.RewardItem, 3));
            rig.TileEvent.eventType = GameEventType.Choice;
            rig.TileEvent.options.Add(new EventOption
            {
                optionText = "使用包扎布",
                alwaysAvailable = false,
                conditions = new List<EventOptionCondition>
                {
                    new() { conditionKind = EventOptionConditionKind.MinimumCarriedItem, key = rig.RewardItem.ContentId, displayName = rig.RewardItem.itemName, value = 1 }
                },
                successEffects = new List<EventEffect>
                {
                    new() { effectType = EventEffectType.RemoveItem, targetName = rig.RewardItem.ContentId, value = 1 },
                    new() { effectType = EventEffectType.AddResource, targetName = rig.Resource.ContentId, value = 2 }
                }
            });
            rig.TileEvent.options.Add(new EventOption { optionText = "放弃", alwaysAvailable = true });
            rig.Manager.EventInput = new ExplicitChoiceInput(0);
            int changeCount = 0;
            PlayableEventItemChangedEvent received = default;
            Action<PlayableEventItemChangedEvent> handler = evt =>
            {
                changeCount++;
                received = evt;
            };
            EventBus.Subscribe(handler);
            try
            {
                HuntTileCommandResult result = await rig.Session.InteractTileAsync(rig.FirstInteractable.AxialCoord);

                Assert.That(result.Succeeded, Is.True, result.Reason);
                Assert.That(rig.Hunter.Collectibles.Any(item => item.Data == rig.RewardItem), Is.False);
                Assert.That(rig.Survivor.Collectibles.Single(item => item.Data == rig.RewardItem).Count, Is.EqualTo(3));
                Assert.That(rig.Hunter.Collectibles.Single(item => item.Data == rig.Resource).Count, Is.EqualTo(2));
                Assert.That(rig.Settlement.GetStoredItem(rig.RewardItem.ContentId), Is.Zero);
                Assert.That(changeCount, Is.EqualTo(1));
                Assert.That(received.ActorId, Is.EqualTo(rig.Hunter.InstanceId));
                Assert.That(received.OldAmount, Is.EqualTo(1));
                Assert.That(received.NewAmount, Is.Zero);
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task InteractTileAsync_FatalInjurySurvivalCommitsDeckAfterStableSelection()
        {
            var presenter = new FixedDeathDeckPresenter(0);
            using var rig = new HuntRig(includeSurvivor: true, hunterDeathCommand: new DirectHunterDeathCommand(), randomInteractionPresenter: presenter);
            rig.Hunter.HP.arms = 0;
            rig.Hunter.SurvivalCards = 1;
            rig.Hunter.DeathCards = 0;
            rig.TileEvent.eventType = GameEventType.Choice;
            rig.TileEvent.options.Add(CreateFatalInjuryOption());
            rig.Manager.EventInput = new ExplicitChoiceInput(0);

            HuntTileCommandResult result = await rig.Session.InteractTileAsync(rig.FirstInteractable.AxialCoord);

            Assert.That(result.Succeeded, Is.True, result.Reason);
            Assert.That(presenter.Requests, Has.Count.EqualTo(1));
            Assert.That(presenter.Requests[0].Kind, Is.EqualTo(TabletopRandomInteractionKind.DeathDeck));
            Assert.That(presenter.Requests[0].Instruction, Is.EqualTo("牌堆构成：1存活/0死亡；翻面后选择"));
            Assert.That(presenter.Requests[0].CardFaceLabels, Is.EqualTo(new[] { "存活" }));
            Assert.That(rig.Hunter.IsAlive, Is.True);
            Assert.That(rig.Hunter.SurvivalCards, Is.EqualTo(1));
            Assert.That(rig.Hunter.DeathCards, Is.EqualTo(1));
            Assert.That(result.EffectResults.Effects.Single(effect => effect.EffectType == EventEffectType.FatalInjury).DeathCard, Is.EqualTo(DeathCardType.Survive));
        }

        [Test]
        public async Task InteractTileAsync_FatalInjurySurvivalQueuesTriggeredEventAndInheritsActor()
        {
            var presenter = new FixedDeathDeckPresenter(0);
            EventData survivalEvent = ScriptableObject.CreateInstance<EventData>();
            survivalEvent.name = "FatalInjurySurvivalEvent";
            survivalEvent.ConfigureContentId("hunt_fatal_injury_survivor_test");
            survivalEvent.category = EventCategory.Triggered;
            survivalEvent.immediateEffects.Add(new EventEffect { effectType = EventEffectType.AddLuck, targetName = "selected", value = 1 });
            using var rig = new HuntRig(includeSurvivor: true, hunterDeathCommand: new DirectHunterDeathCommand(), randomInteractionPresenter: presenter);
            rig.Hunter.HP.arms = 0;
            rig.Hunter.SurvivalCards = 1;
            rig.Hunter.DeathCards = 0;
            rig.TileEvent.eventType = GameEventType.Choice;
            EventOption option = CreateFatalInjuryOption();
            EventEffect fatalEffect = option.successEffects.Single();
            fatalEffect.survivalEventId = survivalEvent.ContentId;
            fatalEffect.SurvivalEvent = survivalEvent;
            rig.TileEvent.options.Add(option);
            rig.Manager.EventInput = new ExplicitChoiceInput(0);

            try
            {
                HuntTileCommandResult result = await rig.Session.InteractTileAsync(rig.FirstInteractable.AxialCoord);

                Assert.That(result.Succeeded, Is.True, result.Reason);
                Assert.That(rig.Hunter.IsAlive, Is.True);
                Assert.That(rig.Hunter.Luck, Is.EqualTo(1));
                Assert.That(result.EffectResults.Effects.Any(effect => effect.EffectType == EventEffectType.AddLuck), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(survivalEvent);
            }
        }

        [Test]
        public async Task ResumePendingEvent_InvalidExplicitActorFailsClosedWithoutTransferringLuck()
        {
            using var rig = new HuntRig(includeSurvivor: true);
            EventData child = ScriptableObject.CreateInstance<EventData>();
            child.name = "InvalidActorSurvivalChild";
            child.ConfigureContentId("invalid-actor-survival-child");
            child.category = EventCategory.Triggered;
            child.immediateEffects.Add(new EventEffect { effectType = EventEffectType.AddLuck, targetName = "selected", value = 1 });
            var store = new PlayableHuntEventOccurrenceStore();
            Assert.That(store.TryScheduleRoot(child, rig.FirstInteractable.AxialCoord, 1, rig.Hunter.InstanceId, out _), Is.True);
            rig.Hunter.IsAlive = false;
            using var restoredSession = new PlayableHuntActionSession(rig.Manager, restoredOccurrenceStore: store);

            try
            {
                HuntTileCommandResult result = await restoredSession.InteractTileAsync(rig.FirstInteractable.AxialCoord);

                Assert.That(result.Succeeded, Is.False);
                Assert.That(store.HasPendingOccurrences, Is.True);
                Assert.That(rig.Survivor.Luck, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(child);
            }
        }

        [Test]
        public async Task InteractTileAsync_FatalInjuryDeathUsesHunterDeathCommand()
        {
            var presenter = new FixedDeathDeckPresenter(0);
            using var rig = new HuntRig(includeSurvivor: true, hunterDeathCommand: new DirectHunterDeathCommand(), randomInteractionPresenter: presenter);
            rig.Hunter.HP.arms = 0;
            rig.Hunter.SurvivalCards = 0;
            rig.Hunter.DeathCards = 1;
            rig.TileEvent.eventType = GameEventType.Choice;
            rig.TileEvent.options.Add(CreateFatalInjuryOption());
            rig.Manager.EventInput = new ExplicitChoiceInput(0);

            HuntTileCommandResult result = await rig.Session.InteractTileAsync(rig.FirstInteractable.AxialCoord);

            Assert.That(result.Succeeded, Is.True, result.Reason);
            Assert.That(rig.Hunter.IsAlive, Is.False);
            Assert.That(presenter.Requests[0].Instruction, Is.EqualTo("牌堆构成：0存活/1死亡；翻面后选择"));
            Assert.That(presenter.Requests[0].CardFaceLabels, Is.EqualTo(new[] { "死亡" }));
            Assert.That(result.EffectResults.Effects.Single(effect => effect.EffectType == EventEffectType.FatalInjury).HunterDied, Is.True);
        }

        [Test]
        public async Task InteractTileAsync_FatalInjuryWithoutPresenterUsesPreparedPosition()
        {
            var effectRandom = new CountingRandom();
            var shuffleRandom = new CountingRandom();
            using var rig = new HuntRig(includeSurvivor: true, hunterDeathCommand: new DirectHunterDeathCommand(), fatalInjuryCommandFactory: settlement => new PlayableHuntFatalInjuryCommand(settlement, effectRandom, shuffleRandom, new DirectHunterDeathCommand()));
            rig.Hunter.HP.arms = 0;
            rig.Hunter.SurvivalCards = 1;
            rig.Hunter.DeathCards = 1;
            rig.TileEvent.eventType = GameEventType.Choice;
            rig.TileEvent.options.Add(CreateFatalInjuryOption());
            rig.Manager.EventInput = new ExplicitChoiceInput(0);

            HuntTileCommandResult result = await rig.Session.InteractTileAsync(rig.FirstInteractable.AxialCoord);

            Assert.That(result.Succeeded, Is.True, result.Reason);
            Assert.That(rig.Hunter.IsAlive, Is.False, "First prepared face-down position is Death with the injected shuffle source");
            Assert.That(result.EffectResults.Effects.Single(effect => effect.EffectType == EventEffectType.FatalInjury).DeathCard, Is.EqualTo(DeathCardType.Death));
            Assert.That(effectRandom.Calls, Is.Zero, "headless position selection must not consume effectRandom");
            Assert.That(shuffleRandom.Calls, Is.GreaterThan(0));
        }

        [Test]
        public async Task InteractTileAsync_LastHunterFatalInjuryTruncatesChildrenAsCampaignEnded()
        {
            var presenter = new FixedDeathDeckPresenter(0);
            using var rig = new HuntRig(includeSurvivor: true, hunterDeathCommand: new DirectHunterDeathCommand(), randomInteractionPresenter: presenter);
            rig.Survivor.IsAlive = false;
            rig.Hunter.HP.arms = 0;
            rig.Hunter.SurvivalCards = 0;
            rig.Hunter.DeathCards = 1;
            rig.TileEvent.eventType = GameEventType.Choice;
            EventOption fatalOption = CreateFatalInjuryOption();
            rig.TileEvent.options.Add(fatalOption);
            EventData child = ScriptableObject.CreateInstance<EventData>();
            child.name = "FatalChildMustNotRun";
            child.ConfigureContentId("fatal-child-must-not-run");
            child.eventType = GameEventType.Choice;
            child.options.Add(new EventOption { optionText = "不应执行", alwaysAvailable = true });
            fatalOption.successChain.Add(child);
            rig.Manager.EventInput = new ExplicitChoiceInput(0);
            HuntEventChainTruncatedEvent truncated = default;
            int truncatedCount = 0;
            Action<HuntEventChainTruncatedEvent> handler = evt =>
            {
                truncated = evt;
                truncatedCount++;
            };
            EventBus.Subscribe(handler);

            try
            {
                HuntTileCommandResult result = await rig.Session.InteractTileAsync(rig.FirstInteractable.AxialCoord);

                Assert.That(result.Succeeded, Is.True, result.Reason);
                Assert.That(rig.Hunter.IsAlive, Is.False);
                Assert.That(rig.Session.HasPendingEventOccurrences, Is.False);
                Assert.That(result.EffectResults.Effects.Single(effect => effect.EffectType == EventEffectType.FatalInjury).HunterDied, Is.True);
                Assert.That(truncatedCount, Is.Zero, "campaign-ended 路径应在 occurrence 提交前丢弃子事件，不应继续排队");
                Assert.That(truncated.PreventedChildCount, Is.Zero);
            }
            finally
            {
                EventBus.Unsubscribe(handler);
                UnityEngine.Object.DestroyImmediate(child);
            }
        }

        [Test]
        public async Task InteractTileAsync_FatalInjuryCancellationLeavesPersistentStateUntouched()
        {
            var presenter = new FixedDeathDeckPresenter(cancelled: true);
            using var rig = new HuntRig(includeSurvivor: true, hunterDeathCommand: new DirectHunterDeathCommand(), randomInteractionPresenter: presenter);
            rig.Hunter.HP.arms = 0;
            int initialSurvivalCards = rig.Hunter.SurvivalCards;
            int initialDeathCards = rig.Hunter.DeathCards;
            rig.TileEvent.eventType = GameEventType.Choice;
            rig.TileEvent.options.Add(CreateFatalInjuryOption());
            rig.Manager.EventInput = new ExplicitChoiceInput(0);

            HuntTileCommandResult result = await rig.Session.InteractTileAsync(rig.FirstInteractable.AxialCoord);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(rig.Hunter.IsAlive, Is.True);
            Assert.That(rig.Hunter.HP.arms, Is.Zero);
            Assert.That(rig.Hunter.SurvivalCards, Is.EqualTo(initialSurvivalCards));
            Assert.That(rig.Hunter.DeathCards, Is.EqualTo(initialDeathCards));
            Assert.That(rig.Session.HasPendingEventOccurrences, Is.True);
        }

        [Test]
        public async Task InteractTileAsync_FatalInjuryCancellationDoesNotConsumeAnyCommitRandom()
        {
            var effectRandom = new CountingRandom();
            var shuffleRandom = new CountingRandom();
            var eventRandom = new CountingRandom();
            var presenter = new FixedDeathDeckPresenter(cancelled: true);
            using var rig = new HuntRig(includeSurvivor: true, hunterDeathCommand: new DirectHunterDeathCommand(), randomInteractionPresenter: presenter, eventRandom: eventRandom, fatalInjuryCommandFactory: settlement => new PlayableHuntFatalInjuryCommand(settlement, effectRandom, shuffleRandom, new DirectHunterDeathCommand()));
            rig.Hunter.HP.arms = 0;
            rig.Hunter.SurvivalCards = 1;
            rig.Hunter.DeathCards = 1;
            rig.TileEvent.eventType = GameEventType.Choice;
            rig.TileEvent.options.Add(CreateFatalInjuryOption());
            rig.Manager.EventInput = new ExplicitChoiceInput(0);

            HuntTileCommandResult result = await rig.Session.InteractTileAsync(rig.FirstInteractable.AxialCoord);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(effectRandom.Calls, Is.EqualTo(0));
            Assert.That(shuffleRandom.Calls, Is.GreaterThan(0));
            Assert.That(eventRandom.Calls, Is.EqualTo(0));
        }

        private static EventOption CreateFatalInjuryOption()
        {
            var option = new EventOption { optionId = "fatal", optionText = "撑住石板", alwaysAvailable = true };
            option.successEffects.Add(new EventEffect { effectType = EventEffectType.FatalInjury, targetName = "selected", bodyPart = "arms", fatalDeckId = EventFatalInjuryRules.HunterDeathDeckId, value = 1, description = "测试致命伤" });
            return option;
        }

        [Test]
        public void EventChoice_ItemCostPreflightRejectsAggregatedShortageBeforeEarlierRewards()
        {
            using var rig = new HuntRig(includeSurvivor: true);
            rig.Hunter.Collectibles.Add(new ItemInstance(rig.RewardItem, 1));
            var gameEvent = ScriptableObject.CreateInstance<EventData>();
            gameEvent.ConfigureContentId("test_item_cost_preflight");
            gameEvent.eventType = GameEventType.Choice;
            gameEvent.options.Add(new EventOption
            {
                optionText = "尝试支付两次",
                alwaysAvailable = true,
                successEffects = new List<EventEffect>
                {
                    new() { effectType = EventEffectType.AddResource, targetName = rig.Resource.ContentId, value = 4 },
                    new() { effectType = EventEffectType.RemoveItem, targetName = rig.RewardItem.ContentId, value = 1 },
                    new() { effectType = EventEffectType.RemoveItem, targetName = rig.RewardItem.ContentId, value = 1 }
                }
            });
            try
            {
                var resourceCommand = new HuntEventResourceCommand(rig.Manager);
                var itemCommand = new HuntEventItemCommand(rig.Manager);
                PlayableEventChoiceTransaction transaction = rig.EventSystem.PrepareChoice(gameEvent, 0, rig.Hunter, resourceCommand: resourceCommand, itemCommand: itemCommand);

                Assert.That(transaction, Is.Not.Null);
                PlayableEventCommitResult result = transaction.CommitStandalone();

                Assert.That(result.EffectResults.HasFailures, Is.True);
                Assert.That(rig.Hunter.Collectibles.Single(item => item.Data == rig.RewardItem).Count, Is.EqualTo(1));
                Assert.That(resourceCommand.GetAvailableAmount(rig.Resource.ContentId), Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameEvent);
            }
        }

        [Test]
        public async Task InteractTileAsync_RevealThenMoveUsesTwoCommittedCommands()
        {
            using var rig = new HuntRig();
            HexTileInstance target = rig.FirstInteractable;

            HuntTileCommandResult reveal = await rig.Session.InteractTileAsync(target.AxialCoord);
            HuntTileCommandResult move = await rig.Session.InteractTileAsync(target.AxialCoord);

            Assert.That(reveal.Succeeded, Is.True);
            Assert.That(reveal.Commit.Kind, Is.EqualTo(HuntTileInteractionKind.Reveal));
            Assert.That(move.Succeeded, Is.True);
            Assert.That(move.Commit.Kind, Is.EqualTo(HuntTileInteractionKind.Move));
            Assert.That(rig.Manager.SquadPosition, Is.EqualTo(target.AxialCoord));
        }

        [Test]
        public async Task InteractTileAsync_LockedTileIsRejectedWithoutMutation()
        {
            using var rig = new HuntRig();
            HexTileInstance target = rig.Manager.Map.Values.First(tile => tile.State == TileState.Locked);

            HuntTileCommandResult result = await rig.Session.InteractTileAsync(target.AxialCoord);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(target.State, Is.EqualTo(TileState.Locked));
            Assert.That(rig.Manager.SquadPosition, Is.EqualTo(Vector2Int.zero));
        }

        [Test]
        public async Task InteractTileAsync_AllHuntersLostRejectsFurtherExploration()
        {
            using var rig = new HuntRig();
            HexTileInstance target = rig.FirstInteractable;
            rig.Manager.ActiveHunters.Insert(0, null);
            rig.Hunter.IsAlive = false;

            HuntTileCommandResult result = await rig.Session.InteractTileAsync(target.AxialCoord);
            HuntRetreatCommandResult retreat = await rig.Session.PrepareRetreatAsync(1);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(target.State, Is.EqualTo(TileState.Interactable));
            Assert.That(rig.Manager.SelectedHunter, Is.Null);
            Assert.That(retreat.Succeeded, Is.True);
        }

        [Test]
        public async Task ReturnCheckpointLockRejectsExplorationAndHarvestUntilReleased()
        {
            using var rig = new HuntRig();
            HexTileInstance target = rig.FirstInteractable;
            var resourcePoint = new ResourcePointInstance { Resource = rig.Resource, ResourceName = rig.Resource.itemName };
            rig.Session.SetReturnCheckpointLock(true);

            HuntTileCommandResult lockedTile = await rig.Session.InteractTileAsync(target.AxialCoord);
            PlayableHarvestTransaction lockedHarvest = await rig.Session.PrepareHarvestAsync(resourcePoint);

            Assert.That(lockedTile.Succeeded, Is.False);
            Assert.That(lockedHarvest, Is.Null);
            Assert.That(target.State, Is.EqualTo(TileState.Interactable));
            Assert.That(rig.Session.IsReturnCheckpointLocked, Is.True);

            rig.Session.SetReturnCheckpointLock(false);
            HuntTileCommandResult releasedTile = await rig.Session.InteractTileAsync(target.AxialCoord);

            Assert.That(releasedTile.Succeeded, Is.True, releasedTile.Reason);
        }

        [Test]
        public async Task EventCommit_SelectedHunterLostPromotesLivingSquadMemberBeforeFact()
        {
            using var rig = new HuntRig(includeSurvivor: true, hunterDeathCommand: new DirectHunterDeathCommand());
            rig.TileEvent.immediateEffects.Add(new EventEffect { effectType = EventEffectType.KillHunter, targetName = "test_event", description = "测试死亡" });
            HunterInstance selectedWhenPublished = null;
            Action<HuntEventNodeCommittedEvent> handler = _ => selectedWhenPublished = rig.Manager.SelectedHunter;
            EventBus.Subscribe(handler);
            try
            {
                HuntTileCommandResult result = await rig.Session.InteractTileAsync(rig.FirstInteractable.AxialCoord);

                Assert.That(result.Succeeded, Is.True);
                Assert.That(rig.Hunter.IsAlive, Is.False);
                Assert.That(rig.Manager.SelectedHunter, Is.SameAs(rig.Survivor));
                Assert.That(selectedWhenPublished, Is.SameAs(rig.Survivor));
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task Reveal_WaitsForEntireEventChainBeforeUnlockingNeighbors()
        {
            using var rig = new HuntRig();
            EventData chainedEvent = ScriptableObject.CreateInstance<EventData>();
            chainedEvent.name = "QueuedTileEventChild";
            chainedEvent.eventName = "后续事件";
            chainedEvent.immediateEffects.Add(new EventEffect { effectType = EventEffectType.AddResource, targetName = rig.Resource.ContentId, value = 1 });
            rig.TileEvent.immediateEffects.Add(new EventEffect { effectType = EventEffectType.AddResource, targetName = rig.Resource.ContentId, value = 1 });
            rig.TileEvent.chainedEvents.Add(chainedEvent);
            var input = new BlockingNarrativeInput();
            rig.Manager.EventInput = input;
            HexTileInstance target = rig.FirstInteractable;
            List<HexTileInstance> lockedNeighbors = HexMapGenerator.GetNeighbors(target.AxialCoord).Where(position => rig.Manager.Map.TryGetValue(position, out HexTileInstance tile) && tile.State == TileState.Locked).Select(position => rig.Manager.Map[position]).ToList();
            Assert.That(lockedNeighbors, Is.Not.Empty);

            UniTask<HuntTileCommandResult> interaction = rig.Session.InteractTileAsync(target.AxialCoord);
            Task started = await Task.WhenAny(input.Started.Task, Task.Delay(5000));
            Assert.That(started, Is.SameAs(input.Started.Task), "事件 Action 未在 5 秒内请求玩家输入");

            Assert.That(target.State, Is.EqualTo(TileState.Revealed));
            Assert.That(lockedNeighbors.All(tile => tile.State == TileState.Locked), Is.True);
            Assert.That(rig.Settlement.GetResource(rig.Resource), Is.Zero);

            input.Continue.TrySetResult(true);
            HuntTileCommandResult result = await interaction;

            Assert.That(result.Succeeded, Is.True);
            Assert.That(input.PresentationCount, Is.EqualTo(2));
            Assert.That(lockedNeighbors.All(tile => tile.State == TileState.Interactable), Is.True);
            Assert.That(rig.Settlement.GetResource(rig.Resource), Is.Zero);
            Assert.That(rig.Hunter.Collectibles.Sum(item => item.Count), Is.EqualTo(2));
            Assert.That(rig.Manager.CreateHuntRecord(false, 1).CollectedItems.Sum(item => item.Count), Is.EqualTo(2));

            rig.Manager.OnExit(rig.Settlement);

            Assert.That(rig.Hunter.Collectibles, Is.Empty);
            Assert.That(rig.Settlement.GetResource(rig.Resource), Is.EqualTo(2));
            UnityEngine.Object.DestroyImmediate(chainedEvent);
        }

        [Test]
        public async Task Reveal_WaitsForTilePresentationBeforeStartingEvent()
        {
            var presenter = new BlockingTilePresenter();
            using var rig = new HuntRig(presenter);
            var input = new BlockingNarrativeInput();
            rig.Manager.EventInput = input;
            HexTileInstance target = rig.FirstInteractable;

            UniTask<HuntTileCommandResult> interaction = rig.Session.InteractTileAsync(target.AxialCoord);
            Task presentationStarted = await Task.WhenAny(presenter.Started.Task, Task.Delay(5000));
            Assert.That(presentationStarted, Is.SameAs(presenter.Started.Task), "地块表现未在 5 秒内开始");

            Assert.That(target.State, Is.EqualTo(TileState.Revealed));
            Assert.That(input.Started.Task.IsCompleted, Is.False);

            presenter.Continue.TrySetResult(true);
            Task eventStarted = await Task.WhenAny(input.Started.Task, Task.Delay(5000));
            Assert.That(eventStarted, Is.SameAs(input.Started.Task), "地块表现结束后事件未在 5 秒内开始");
            input.Continue.TrySetResult(true);

            HuntTileCommandResult result = await interaction;
            Assert.That(result.Succeeded, Is.True);
            Assert.That(presenter.Request.Kind, Is.EqualTo(HuntTileInteractionKind.Reveal));
            Assert.That(presenter.Request.Coordinate, Is.EqualTo(target.AxialCoord));
        }

        [Test]
        public async Task Move_WaitsForSquadPresentationBeforeCompletingCommand()
        {
            var presenter = new BlockingTilePresenter(HuntTileInteractionKind.Move);
            using var rig = new HuntRig(presenter);
            rig.Manager.EventInput = null;
            HexTileInstance target = rig.FirstInteractable;
            await rig.Session.InteractTileAsync(target.AxialCoord);

            UniTask<HuntTileCommandResult> movement = rig.Session.InteractTileAsync(target.AxialCoord);
            Task presentationStarted = await Task.WhenAny(presenter.Started.Task, Task.Delay(5000));
            Assert.That(presentationStarted, Is.SameAs(presenter.Started.Task), "小队移动表现未在 5 秒内开始");

            Assert.That(rig.Manager.SquadPosition, Is.EqualTo(target.AxialCoord));
            Assert.That(movement.Status, Is.EqualTo(UniTaskStatus.Pending));

            presenter.Continue.TrySetResult(true);
            HuntTileCommandResult result = await movement;
            Assert.That(result.Succeeded, Is.True);
            Assert.That(presenter.Request.Kind, Is.EqualTo(HuntTileInteractionKind.Move));
        }

        [Test]
        public async Task Reveal_PresentationFailureDoesNotRollbackCommittedGameplay()
        {
            using var rig = new HuntRig(new FailingTilePresenter());
            rig.Manager.EventInput = null;
            HexTileInstance target = rig.FirstInteractable;
            LogAssert.Expect(LogType.Exception, new Regex("测试表现失败"));

            HuntTileCommandResult result = await rig.Session.InteractTileAsync(target.AxialCoord);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(target.State, Is.EqualTo(TileState.Revealed));
            Assert.That(HexMapGenerator.GetNeighbors(target.AxialCoord).Where(rig.Manager.Map.ContainsKey).All(position => rig.Manager.Map[position].State != TileState.Locked), Is.True);
        }

        [Test]
        public async Task Reveal_SelfReferencingEventCommitsOnceAndStillUnlocksNeighbors()
        {
            using var rig = new HuntRig();
            rig.TileEvent.immediateEffects.Add(new EventEffect { effectType = EventEffectType.AddResource, targetName = rig.Resource.ContentId, value = 1 });
            rig.TileEvent.chainedEvents.Add(rig.TileEvent);
            HexTileInstance target = rig.FirstInteractable;
            int preventedCount = 0;
            Action<PlayableEventDuplicatePreventedEvent> handler = _ => preventedCount++;
            EventBus.Subscribe(handler);
            try
            {
                HuntTileCommandResult result = await rig.Session.InteractTileAsync(target.AxialCoord);

                Assert.That(result.Succeeded, Is.True);
                Assert.That(rig.Settlement.GetResource(rig.Resource), Is.Zero);
                Assert.That(rig.Hunter.Collectibles.Sum(item => item.Count), Is.EqualTo(1));
                Assert.That(preventedCount, Is.EqualTo(1));
                Assert.That(HexMapGenerator.GetNeighbors(target.AxialCoord).Where(rig.Manager.Map.ContainsKey).All(position => rig.Manager.Map[position].State != TileState.Locked), Is.True);
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task HuntEventResourceRemoval_IsAtomicAndNeverSpendsSettlementInventory()
        {
            using var rig = new HuntRig();
            rig.Settlement.AddResource(rig.Resource, 5);
            rig.Hunter.Collectibles.Add(new ItemInstance(rig.Resource));
            rig.TileEvent.immediateEffects.Add(new EventEffect { effectType = EventEffectType.RemoveResource, targetName = rig.Resource.ContentId, value = 2 });

            HuntTileCommandResult result = await rig.Session.InteractTileAsync(rig.FirstInteractable.AxialCoord);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.FailedEffectCount, Is.EqualTo(1));
            Assert.That(rig.Hunter.Collectibles.Sum(item => item.Count), Is.EqualTo(1));
            Assert.That(rig.Settlement.GetResource(rig.Resource), Is.EqualTo(5));
        }

        [Test]
        public async Task HuntEventResourceReward_PublishesHuntScopedFactOnly()
        {
            using var rig = new HuntRig();
            rig.TileEvent.immediateEffects.Add(new EventEffect { effectType = EventEffectType.AddResource, targetName = rig.Resource.ContentId, value = 2 });
            int huntFactCount = 0;
            int settlementFactCount = 0;
            PlayableEventResourceChangedEvent received = default;
            Action<PlayableEventResourceChangedEvent> huntHandler = evt =>
            {
                huntFactCount++;
                received = evt;
            };
            Action<ResourceChangedEvent> settlementHandler = _ => settlementFactCount++;
            EventBus.Subscribe(huntHandler);
            EventBus.Subscribe(settlementHandler);
            try
            {
                HuntTileCommandResult result = await rig.Session.InteractTileAsync(rig.FirstInteractable.AxialCoord);

                Assert.That(result.Succeeded, Is.True);
                Assert.That(huntFactCount, Is.EqualTo(1));
                Assert.That(settlementFactCount, Is.Zero);
                Assert.That(received.Scope, Is.EqualTo(PlayableEventResourceScope.HuntCollectibles));
                Assert.That(received.ResourceId, Is.EqualTo(rig.Resource.ContentId));
                Assert.That(received.OldAmount, Is.Zero);
                Assert.That(received.NewAmount, Is.EqualTo(2));
            }
            finally
            {
                EventBus.Unsubscribe(huntHandler);
                EventBus.Unsubscribe(settlementHandler);
            }
        }

        [Test]
        public void EventChoice_ItemCostPreflightRejectsLaterResourceOverflowWithoutPartialCommit()
        {
            using var rig = new HuntRig(includeSurvivor: true);
            rig.Hunter.Collectibles.Add(new ItemInstance(rig.RewardItem, 1));
            rig.Hunter.Collectibles.Add(new ItemInstance(rig.Resource, int.MaxValue - 1));
            var gameEvent = ScriptableObject.CreateInstance<EventData>();
            gameEvent.ConfigureContentId("test_item_cost_resource_overflow");
            gameEvent.eventType = GameEventType.Choice;
            gameEvent.options.Add(new EventOption
            {
                optionText = "支付后获得过量资源",
                alwaysAvailable = true,
                successEffects = new List<EventEffect>
                {
                    new() { effectType = EventEffectType.RemoveItem, targetName = rig.RewardItem.ContentId, value = 1 },
                    new() { effectType = EventEffectType.AddResource, targetName = rig.Resource.ContentId, value = 2 }
                }
            });
            int changeCount = 0;
            Action<PlayableEventItemChangedEvent> handler = _ => changeCount++;
            EventBus.Subscribe(handler);
            try
            {
                var resourceCommand = new HuntEventResourceCommand(rig.Manager);
                var itemCommand = new HuntEventItemCommand(rig.Manager);
                PlayableEventChoiceTransaction transaction = rig.EventSystem.PrepareChoice(gameEvent, 0, rig.Hunter, resourceCommand: resourceCommand, itemCommand: itemCommand);

                Assert.That(transaction, Is.Not.Null);
                PlayableEventCommitResult result = transaction.CommitStandalone();

                Assert.That(result.EffectResults.HasFailures, Is.True);
                Assert.That(rig.Hunter.Collectibles.Single(item => item.Data == rig.RewardItem).Count, Is.EqualTo(1));
                Assert.That(resourceCommand.GetAvailableAmount(rig.Resource.ContentId), Is.EqualTo(int.MaxValue - 1));
                Assert.That(changeCount, Is.Zero);
            }
            finally
            {
                EventBus.Unsubscribe(handler);
                UnityEngine.Object.DestroyImmediate(gameEvent);
            }
        }

        [Test]
        public async Task HuntEventItemReward_AddsNonResourceToActorAndPublishesGameplayFact()
        {
            using var rig = new HuntRig();
            rig.TileEvent.immediateEffects.Add(new EventEffect { effectType = EventEffectType.AddItem, targetName = rig.RewardItem.ContentId, value = 1 });
            PlayableEventItemChangedEvent received = default;
            int factCount = 0;
            Action<PlayableEventItemChangedEvent> handler = evt =>
            {
                received = evt;
                factCount++;
            };
            EventBus.Subscribe(handler);
            try
            {
                HuntTileCommandResult result = await rig.Session.InteractTileAsync(rig.FirstInteractable.AxialCoord);

                Assert.That(result.Succeeded, Is.True, result.Reason);
                Assert.That(result.FailedEffectCount, Is.Zero);
                Assert.That(rig.Hunter.Collectibles.Sum(item => item?.Data?.ContentId == rig.RewardItem.ContentId ? item.Count : 0), Is.EqualTo(1));
                Assert.That(rig.Settlement.GetStoredItem(rig.RewardItem.ContentId), Is.Zero);
                Assert.That(factCount, Is.EqualTo(1));
                Assert.That(received.ItemId, Is.EqualTo(rig.RewardItem.ContentId));
                Assert.That(received.ActorId, Is.EqualTo(rig.Hunter.InstanceId));
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task HuntEventPopulationRescue_StagesHuntPopulationWithoutMutatingSettlement()
        {
            using var rig = new HuntRig();
            rig.TileEvent.immediateEffects.Add(new EventEffect { effectType = EventEffectType.RescuePopulation, value = 1 });
            HuntPopulationRescuedEvent received = default;
            int factCount = 0;
            Action<HuntPopulationRescuedEvent> handler = evt =>
            {
                received = evt;
                factCount++;
            };
            EventBus.Subscribe(handler);
            try
            {
                HuntTileCommandResult result = await rig.Session.InteractTileAsync(rig.FirstInteractable.AxialCoord);

                Assert.That(result.Succeeded, Is.True, result.Reason);
                Assert.That(result.FailedEffectCount, Is.Zero);
                Assert.That(rig.Manager.RescuedPopulation, Is.EqualTo(1));
                Assert.That(rig.Settlement.Population, Is.Zero);
                Assert.That(factCount, Is.EqualTo(1));
                Assert.That(received.ActorId, Is.EqualTo(rig.Hunter.InstanceId));
                Assert.That(received.OldAmount, Is.Zero);
                Assert.That(received.NewAmount, Is.EqualTo(1));
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public void HuntEventPopulationRescue_RejectsInvalidActorAndOverflowWithoutMutation()
        {
            using var rig = new HuntRig();
            var command = new HuntEventPopulationCommand(rig.Manager);
            var foreignActor = new HunterInstance(null, 9912) { IsAlive = true };

            Assert.That(command.TryRescue(1, foreignActor, out _, out _), Is.False);
            Assert.That(command.TryRescue(0, rig.Hunter, out _, out _), Is.False);
            Assert.That(command.TryRescue(int.MaxValue, rig.Hunter, out _, out string firstReason), Is.True, firstReason);
            Assert.That(command.TryRescue(1, rig.Hunter, out _, out _), Is.False);
            Assert.That(rig.Manager.RescuedPopulation, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public async Task UseConsumableAsync_ConsumesSelectedHuntersItemAndRecoversBodyPart()
        {
            using var rig = new HuntRig();
            rig.Hunter.HP.arms = 1;
            rig.Hunter.Collectibles.Add(new ItemInstance(rig.RewardItem, 2));
            HuntConsumableUsedEvent received = default;
            int factCount = 0;
            Action<HuntConsumableUsedEvent> handler = evt =>
            {
                received = evt;
                factCount++;
            };
            EventBus.Subscribe(handler);
            try
            {
                HuntConsumableCommandResult result = await rig.Session.UseConsumableAsync(rig.Hunter.InstanceId, rig.RewardItem.ContentId, HunterBodyPart.Arms);

                Assert.That(result.Succeeded, Is.True, result.Reason);
                Assert.That(result.Recovery.PreviousHealth, Is.EqualTo(1));
                Assert.That(result.Recovery.CurrentHealth, Is.EqualTo(2));
                Assert.That(result.RemainingCount, Is.EqualTo(1));
                Assert.That(rig.Hunter.Collectibles.Sum(item => item.Count), Is.EqualTo(1));
                Assert.That(factCount, Is.EqualTo(1));
                Assert.That(received.SessionId, Is.EqualTo(rig.Session.SessionId));
                Assert.That(received.HunterId, Is.EqualTo(rig.Hunter.InstanceId));
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task UseConsumableAsync_PreventAndInvalidRequestsDoNotMutateHunter()
        {
            using var rig = new HuntRig(includeSurvivor: true);
            rig.Hunter.HP.legs = 1;
            rig.Hunter.Collectibles.Add(new ItemInstance(rig.RewardItem, 1));
            IDisposable prevention = rig.Session.Reactors.RegisterGlobal(new PreventHuntConsumableReactor());
            try
            {
                HuntConsumableCommandResult prevented = await rig.Session.UseConsumableAsync(rig.Hunter.InstanceId, rig.RewardItem.ContentId, HunterBodyPart.Legs);
                Assert.That(prevented.Succeeded, Is.False);
                Assert.That(rig.Hunter.HP.legs, Is.EqualTo(1));
                Assert.That(rig.Hunter.Collectibles.Sum(item => item.Count), Is.EqualTo(1));
            }
            finally
            {
                prevention.Dispose();
            }
            HuntConsumableCommandResult foreignOwner = await rig.Session.UseConsumableAsync(rig.Survivor.InstanceId, rig.RewardItem.ContentId, HunterBodyPart.Legs);
            Assert.That(foreignOwner.Succeeded, Is.False);
            Assert.That(rig.Hunter.HP.legs, Is.EqualTo(1));
            Assert.That(rig.Hunter.Collectibles.Sum(item => item.Count), Is.EqualTo(1));

            HuntConsumableCommandResult missingId = await rig.Session.UseConsumableAsync(rig.Hunter.InstanceId, string.Empty, HunterBodyPart.Legs);
            Assert.That(missingId.Succeeded, Is.False);
            Assert.That(rig.Hunter.Collectibles.Sum(item => item.Count), Is.EqualTo(1));

            rig.Hunter.HP.legs = rig.Hunter.MaxHP.legs;
            HuntConsumableCommandResult healthy = await rig.Session.UseConsumableAsync(rig.Hunter.InstanceId, rig.RewardItem.ContentId, HunterBodyPart.Legs);
            Assert.That(healthy.Succeeded, Is.False);
            Assert.That(rig.Hunter.Collectibles.Sum(item => item.Count), Is.EqualTo(1));
        }

        [Test]
        public void HuntEventResourceReward_RejectsCountOverflowWithoutMutation()
        {
            using var rig = new HuntRig();
            rig.Hunter.Collectibles.Add(new ItemInstance(rig.Resource, int.MaxValue));
            var command = new HuntEventResourceCommand(rig.Manager);

            bool applied = command.TryApply(EventEffectType.AddResource, rig.Resource.ContentId, 1, rig.Hunter, out _, out string reason);

            Assert.That(applied, Is.False);
            Assert.That(reason, Does.Contain("数量范围"));
            Assert.That(rig.Hunter.Collectibles, Has.Count.EqualTo(1));
            Assert.That(rig.Hunter.Collectibles[0].Count, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void HuntEventResourceReward_RejectsForeignActorInsteadOfRedirectingToSquad()
        {
            using var rig = new HuntRig();
            var foreignActor = new HunterInstance(null, 9911) { IsAlive = true };
            var command = new HuntEventResourceCommand(rig.Manager);

            bool applied = command.TryApply(EventEffectType.AddResource, rig.Resource.ContentId, 1, foreignActor, out _, out string reason);

            Assert.That(applied, Is.False);
            Assert.That(reason, Does.Contain("没有可携带资源的猎人"));
            Assert.That(rig.Hunter.Collectibles, Is.Empty);
        }

        [Test]
        public void HuntEventResourceReward_RejectsDeadActorInsteadOfRedirectingToSurvivor()
        {
            using var rig = new HuntRig(includeSurvivor: true);
            rig.Hunter.IsAlive = false;
            var command = new HuntEventResourceCommand(rig.Manager);

            bool applied = command.TryApply(EventEffectType.AddResource, rig.Resource.ContentId, 1, rig.Hunter, out _, out string reason);

            Assert.That(applied, Is.False);
            Assert.That(reason, Does.Contain("没有可携带资源的猎人"));
            Assert.That(rig.Survivor.Collectibles, Is.Empty);
        }

        [Test]
        public async Task CombatEvent_PublishesOneContextualRequestAfterRootAndStopsLaterChain()
        {
            using var rig = new HuntRig();
            EventData ignoredChild = ScriptableObject.CreateInstance<EventData>();
            ignoredChild.name = "AfterCombatChild";
            ignoredChild.immediateEffects.Add(new EventEffect { effectType = EventEffectType.AddResource, targetName = "should-not-apply", value = 1 });
            rig.TileEvent.eventType = GameEventType.Combat;
            rig.TileEvent.combatEncounterId = "event-boss";
            rig.TileEvent.immediateEffects.Add(new EventEffect { effectType = EventEffectType.TriggerCombat, targetName = "ignored-effect-boss" });
            rig.TileEvent.immediateEffects.Add(new EventEffect { effectType = EventEffectType.TriggerCombat, targetName = "ignored-second-boss" });
            rig.TileEvent.chainedEvents.Add(ignoredChild);
            HexTileInstance target = rig.FirstInteractable;
            int receivedCount = 0;
            int legacyRequestCount = 0;
            CampaignEncounterRequest received = default;
            bool neighborsUnlockedWhenPublished = false;
            Action<CampaignEncounterRequestedEvent> handler = evt =>
            {
                receivedCount++;
                received = evt.Request;
                neighborsUnlockedWhenPublished = HexMapGenerator.GetNeighbors(target.AxialCoord).Where(rig.Manager.Map.ContainsKey).All(position => rig.Manager.Map[position].State != TileState.Locked);
            };
            Action<PlayableEventEncounterRequestedEvent> legacyHandler = _ => legacyRequestCount++;
            EventBus.Subscribe(handler);
            EventBus.Subscribe(legacyHandler);
            try
            {
                HuntTileCommandResult result = await rig.Session.InteractTileAsync(target.AxialCoord);

                Assert.That(result.Succeeded, Is.True);
                Assert.That(receivedCount, Is.EqualTo(1));
                Assert.That(legacyRequestCount, Is.Zero);
                Assert.That(received.SourceSessionId, Is.EqualTo(rig.Session.SessionId));
                Assert.That(received.EncounterId, Is.EqualTo("event-boss"));
                Assert.That(received.SourceKind, Is.EqualTo(CampaignEncounterSourceKind.HuntEvent));
                Assert.That(received.SourceCoordinate, Is.EqualTo(target.AxialCoord));
                Assert.That(received.SourceEventId, Is.EqualTo(rig.TileEvent.name));
                Assert.That(received.SourceContextId, Is.EqualTo("test-destination"));
                Assert.That(neighborsUnlockedWhenPublished, Is.True);
                Assert.That(rig.Settlement.GetResource("should-not-apply"), Is.Zero);

                HuntTileCommandResult blocked = await rig.Session.InteractTileAsync(target.AxialCoord);
                Assert.That(blocked.Succeeded, Is.False);
                Assert.That(blocked.Reason, Does.Contain("遭遇事件"));

                rig.Session.ReleaseEncounterHandoffLock();
                HuntTileCommandResult resumed = await rig.Session.InteractTileAsync(target.AxialCoord);
                Assert.That(resumed.Succeeded, Is.True, resumed.Reason);
            }
            finally
            {
                EventBus.Unsubscribe(handler);
                EventBus.Unsubscribe(legacyHandler);
                UnityEngine.Object.DestroyImmediate(ignoredChild);
            }
        }

        [Test]
        public async Task BossTile_UsesConfiguredEncounterIdAndCurrentSession()
        {
            using var rig = new HuntRig();
            HexTileInstance target = rig.FirstInteractable;
            target.HasBossEncounter = true;
            target.DomainState.HasBossEncounter = true;
            target.Config.bossEncounterId = "tile-boss";
            CampaignEncounterRequest received = default;
            Action<CampaignEncounterRequestedEvent> handler = evt => received = evt.Request;
            EventBus.Subscribe(handler);
            try
            {
                HuntTileCommandResult result = await rig.Session.InteractTileAsync(target.AxialCoord);

                Assert.That(result.Succeeded, Is.True);
                Assert.That(received.EncounterId, Is.EqualTo("tile-boss"));
                Assert.That(received.SourceSessionId, Is.EqualTo(rig.Session.SessionId));
                Assert.That(received.SourceKind, Is.EqualTo(CampaignEncounterSourceKind.HuntBossTile));
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        private sealed class HuntRig : IDisposable
        {
            private readonly EventData tileEvent;
            private readonly HexTileData startingTile;
            private readonly HexTileData plainTile;
            private readonly HunterData hunterTemplate;
            private readonly ItemData resource;
            private readonly ItemData rewardItem;
            private readonly List<ItemData> previousItems;

            public HuntRig(IHuntTileInteractionPresenter tileInteractionPresenter = null, bool includeSurvivor = false, IHunterDeathCommand hunterDeathCommand = null, bool includeResourcePoints = false, ITabletopRandomInteractionPresenter randomInteractionPresenter = null, IRandomSource eventRandom = null, Func<SettlementInstance, IPlayableEventFatalInjuryCommand> fatalInjuryCommandFactory = null)
            {
                previousItems = PlayableSettlementItemRegistry.Items.ToList();
                resource = ScriptableObject.CreateInstance<ItemData>();
                resource.name = "test_hunt_resource";
                resource.ConfigureContentId("test_hunt_resource");
                resource.itemName = "测试资源";
                resource.itemType = ItemType.Resource;
                rewardItem = ScriptableObject.CreateInstance<ItemData>();
                rewardItem.name = "test_hunt_reward_item";
                rewardItem.ConfigureContentId("test_hunt_reward_item");
                rewardItem.itemName = "测试包扎布";
                rewardItem.itemType = ItemType.Consumable;
                rewardItem.ConfigureConsumableEffect(ConsumableEffectKind.RecoverBodyPart, 1);
                var configuredItems = new List<ItemData>(previousItems) { resource, rewardItem };
                PlayableSettlementItemRegistry.Configure(configuredItems);
                hunterTemplate = ScriptableObject.CreateInstance<HunterData>();
                hunterTemplate.name = "TestHuntActor";
                hunterTemplate.hunterName = "测试猎人";
                Hunter = new HunterInstance(hunterTemplate);
                Survivor = includeSurvivor ? new HunterInstance(hunterTemplate) { Name = "后备猎人" } : null;
                tileEvent = ScriptableObject.CreateInstance<EventData>();
                tileEvent.name = "QueuedTileEvent";
                tileEvent.eventName = "队列地块事件";
                tileEvent.category = EventCategory.Hunt;
                tileEvent.drawWeight = 1;
                startingTile = ScriptableObject.CreateInstance<HexTileData>();
                startingTile.name = "QueuedStartingTile";
                startingTile.tileType = TileType.Starting;
                startingTile.tileName = "起点";
                plainTile = ScriptableObject.CreateInstance<HexTileData>();
                plainTile.name = "QueuedPlainTile";
                plainTile.tileType = TileType.Plains;
                plainTile.tileName = "测试地块";
                plainTile.tileRevealEvent = tileEvent;
                if (includeResourcePoints)
                {
                    plainTile.maxResourcePoints = 2;
                    plainTile.resourcePoints.Add(new ResourcePointConfig { resourcePointId = "point:one", resource = resource, drawCount = 1 });
                    plainTile.resourcePoints.Add(new ResourcePointConfig { resourcePointId = "point:two", resource = resource, drawCount = 1 });
                }
                Settlement = new SettlementInstance();
                if (includeSurvivor)
                {
                    Settlement.Hunters.Add(Hunter);
                    Settlement.Hunters.Add(Survivor);
                }
                EventSystem = new EventSystem(Settlement, eventRandom ?? new FirstRandom(), hunterDeathCommand: hunterDeathCommand);
                Manager = new HuntManager(EventSystem, seed: 17)
                {
                    StartingTileConfig = startingTile,
                    TilePool = { plainTile }
                };
                Manager.OnEnter(includeSurvivor ? new List<HunterInstance> { Hunter, Survivor } : new List<HunterInstance> { Hunter });
                Session = new PlayableHuntActionSession(Manager, "default-boss", "test-destination", randomInteractionPresenter: randomInteractionPresenter, tileInteractionPresenter: tileInteractionPresenter, fatalInjuryCommand: fatalInjuryCommandFactory?.Invoke(Settlement));
            }

            public EventSystem EventSystem { get; }
            public SettlementInstance Settlement { get; }
            public EventData TileEvent => tileEvent;
            public HunterInstance Hunter { get; }
            public HunterInstance Survivor { get; }
            public ItemData Resource => resource;
            public ItemData RewardItem => rewardItem;
            public HuntManager Manager { get; }
            public PlayableHuntActionSession Session { get; }
            public HexTileInstance FirstInteractable => Manager.Map.Values.First(tile => tile.State == TileState.Interactable);

            public void Dispose()
            {
                Session.Dispose();
                PlayableSettlementItemRegistry.Configure(previousItems);
                UnityEngine.Object.DestroyImmediate(rewardItem);
                UnityEngine.Object.DestroyImmediate(resource);
                UnityEngine.Object.DestroyImmediate(hunterTemplate);
                UnityEngine.Object.DestroyImmediate(plainTile);
                UnityEngine.Object.DestroyImmediate(startingTile);
                UnityEngine.Object.DestroyImmediate(tileEvent);
            }
        }

        private sealed class BlockingNarrativeInput : IPlayableEventInput
        {
            public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource<bool> Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public int PresentationCount { get; private set; }

            public async UniTask ConfirmNarrativeAsync(EventData gameEvent, HunterInstance actor, CancellationToken cancellationToken)
            {
                PresentationCount++;
                if (PresentationCount != 1) return;
                Started.TrySetResult(true);
                await Continue.Task.AsUniTask().AttachExternalCancellation(cancellationToken);
            }

            public UniTask<PlayableEventChoiceSelection> SelectChoiceAsync(EventData gameEvent, HunterInstance actor, IReadOnlyList<HunterInstance> hunters, IPlayableEventResourceAvailability resourceAvailability, CancellationToken cancellationToken) => UniTask.FromResult(new PlayableEventChoiceSelection(-1, null));
            public UniTask<PlayableEventCheckDecision> PresentCheckAsync(PlayableEventChoiceTransaction transaction, CancellationToken cancellationToken) => UniTask.FromResult(PlayableEventCheckDecision.Accept);
            public UniTask ConfirmResultAsync(EventData gameEvent, EventResolutionResult result, CancellationToken cancellationToken) => UniTask.CompletedTask;
        }

        private sealed class ExplicitChoiceInput : IPlayableEventInput
        {
            private readonly int optionIndex;

            public ExplicitChoiceInput(int optionIndex)
            {
                this.optionIndex = optionIndex;
            }

            public int SelectionCount { get; private set; }

            public UniTask ConfirmNarrativeAsync(EventData gameEvent, HunterInstance actor, CancellationToken cancellationToken) => UniTask.CompletedTask;
            public UniTask<PlayableEventChoiceSelection> SelectChoiceAsync(EventData gameEvent, HunterInstance actor, IReadOnlyList<HunterInstance> hunters, IPlayableEventResourceAvailability resourceAvailability, CancellationToken cancellationToken)
            {
                SelectionCount++;
                return UniTask.FromResult(new PlayableEventChoiceSelection(optionIndex, actor));
            }
            public UniTask<PlayableEventCheckDecision> PresentCheckAsync(PlayableEventChoiceTransaction transaction, CancellationToken cancellationToken) => UniTask.FromResult(PlayableEventCheckDecision.Accept);
            public UniTask ConfirmResultAsync(EventData gameEvent, EventResolutionResult result, CancellationToken cancellationToken) => UniTask.CompletedTask;
        }

        private sealed class BlockingTilePresenter : IHuntTileInteractionPresenter
        {
            private readonly HuntTileInteractionKind blockedKind;

            public BlockingTilePresenter(HuntTileInteractionKind blockedKind = HuntTileInteractionKind.Reveal)
            {
                this.blockedKind = blockedKind;
            }

            public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource<bool> Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public HuntTileInteractionPresentationRequest Request { get; private set; }

            public async UniTask PresentAsync(HuntTileInteractionPresentationRequest request, CancellationToken cancellationToken)
            {
                if (request.Kind != blockedKind) return;
                Request = request;
                Started.TrySetResult(true);
                await Continue.Task.AsUniTask().AttachExternalCancellation(cancellationToken);
            }
        }

        private sealed class FailingTilePresenter : IHuntTileInteractionPresenter
        {
            public UniTask PresentAsync(HuntTileInteractionPresentationRequest request, CancellationToken cancellationToken) => UniTask.FromException(new InvalidOperationException("测试表现失败"));
        }

        private sealed class PreventCommitReactor : GameActionReactor<CommitHuntTileInteractionAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            protected override void React(CommitHuntTileInteractionAction action, ReactionContext context, ReactionResponse response) => response.Prevent("测试规则阻止地块提交");
        }

        private sealed class PreventEventNodeReactor : GameActionReactor<ResolvePlayableEventNodeAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            protected override void React(ResolvePlayableEventNodeAction action, ReactionContext context, ReactionResponse response) => response.Prevent("测试规则覆盖事件节点");
        }

        private sealed class PreventHuntConsumableReactor : GameActionReactor<UseHuntConsumableAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            protected override void React(UseHuntConsumableAction action, ReactionContext context, ReactionResponse response) => response.Prevent("测试规则阻止狩猎消耗品");
        }

        private sealed class FirstRandom : IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
        }

        private sealed class CountingRandom : IRandomSource
        {
            public int Calls { get; private set; }

            public int Next(int minInclusive, int maxExclusive)
            {
                Calls++;
                return minInclusive;
            }

            public double NextDouble()
            {
                Calls++;
                return 0d;
            }
        }

        private sealed class FixedDeathDeckPresenter : ITabletopRandomInteractionPresenter
        {
            private readonly int selectedPosition;
            private readonly bool cancelled;

            public FixedDeathDeckPresenter(int selectedPosition = 0, bool cancelled = false)
            {
                this.selectedPosition = selectedPosition;
                this.cancelled = cancelled;
            }

            public List<TabletopRandomInteractionRequest> Requests { get; } = new();

            public UniTask<TabletopRandomInteractionResult> PresentAsync(TabletopRandomInteractionRequest request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                var cardIds = cancelled ? Array.Empty<string>() : new[] { $"{request.DeckId}:position-{selectedPosition}" };
                return UniTask.FromResult(new TabletopRandomInteractionResult(request.InteractionId, Array.Empty<int>(), cardIds, cancelled));
            }
        }

        private sealed class DirectHunterDeathCommand : IHunterDeathCommand
        {
            public bool TryKill(HunterInstance hunter, string causeId, string causeText, out string reason)
            {
                reason = string.Empty;
                if (hunter == null || !hunter.IsAlive) return false;
                hunter.IsAlive = false;
                return true;
            }
        }
    }
}
