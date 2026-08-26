using System.Collections.Generic;
using Core;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableEventChoiceTransactionTests
    {
        [Test]
        public void CheckedChoice_RerollsThenCommitsEffectsExactlyOnce()
        {
            var settlement = new SettlementInstance();
            var hunter = new HunterInstance(null, 8123) { Name = "解读者", Understanding = 1, Willpower = 1, WillpowerMax = 1 };
            settlement.Hunters.Add(hunter);
            var eventSystem = new EventSystem(settlement, new SequenceRandom(1, 8));
            EventData gameEvent = CreateCheckedEvent();
            int completedCount = 0;
            eventSystem.OnEventChainCompleted = () => completedCount++;

            try
            {
                PlayableEventChoiceTransaction transaction = eventSystem.PrepareChoice(gameEvent, 0, hunter);

                Assert.That(transaction.RollValue, Is.EqualTo(2));
                Assert.That(transaction.Success, Is.False);
                Assert.That(settlement.GetResource("碎石"), Is.Zero);
                Assert.That(transaction.TryReroll(), Is.True);
                Assert.That(transaction.RollValue, Is.EqualTo(9));
                Assert.That(transaction.Success, Is.True);
                Assert.That(hunter.Willpower, Is.Zero);
                Assert.That(hunter.Luck, Is.EqualTo(1));

                transaction.Commit();
                transaction.Commit();
                Assert.That(settlement.GetResource("碎石"), Is.EqualTo(2));
                Assert.That(settlement.HasDiscoveredMaterial("碎石"), Is.True);
                Assert.That(completedCount, Is.Zero);

                transaction.Continue();
                transaction.Continue();
                Assert.That(completedCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(gameEvent);
            }
        }

        [Test]
        public void HuntWorldEffect_RequiresPortAndDelegatesToBoundWorld()
        {
            var settlement = new SettlementInstance();
            var hunter = new HunterInstance(null, 8130) { Name = "探路者" };
            settlement.Hunters.Add(hunter);
            var gameEvent = ScriptableObject.CreateInstance<EventData>();
            gameEvent.name = "hunt-world-effect";
            gameEvent.category = EventCategory.Hunt;
            gameEvent.options.Add(new EventOption
            {
                optionText = "封存地块",
                alwaysAvailable = true,
                successEffects = new List<EventEffect> { new() { effectType = EventEffectType.ExhaustCurrentHuntTileResources, value = 0 } }
            });

            try
            {
                EventSystem eventSystem = new(settlement, new SequenceRandom(0));
                PlayableEventCommitResult withoutPort = eventSystem.PrepareChoice(gameEvent, 0, hunter).CommitStandalone();
                Assert.That(withoutPort.EffectResults.FailedCount, Is.EqualTo(1));
                Assert.That(withoutPort.EffectResults.Effects[0].Reason, Does.Contain("世界效果端口"));

                var worldCommand = new RecordingWorldCommand();
                PlayableEventCommitResult withPort = eventSystem.PrepareChoice(gameEvent, 0, hunter, worldCommand: worldCommand).CommitStandalone();
                Assert.That(withPort.EffectResults.AppliedCount, Is.EqualTo(1));
                Assert.That(worldCommand.ApplyCount, Is.EqualTo(1));
                Assert.That(withPort.EffectResults.Effects[0].ResolvedTargetId, Is.EqualTo("test-target"));
                Assert.That(withPort.EffectResults.Effects[0].StateChanged, Is.True);
                Assert.That(withPort.EffectResults.Effects[0].PreviousValue, Is.Zero);
                Assert.That(withPort.EffectResults.Effects[0].CurrentValue, Is.EqualTo(2));
                PlayableEventCommitResult repeated = eventSystem.PrepareChoice(gameEvent, 0, hunter, worldCommand: worldCommand).CommitStandalone();
                Assert.That(repeated.EffectResults.Effects[0].ResolvedTargetId, Is.EqualTo("test-target"));
                Assert.That(worldCommand.ApplyCount, Is.EqualTo(2));

                PlayableEventChoiceTransaction transaction = eventSystem.PrepareChoice(gameEvent, 0, hunter, worldCommand: worldCommand);
                PlayableEventCommitResult firstCommit = transaction.CommitStandalone();
                PlayableEventCommitResult secondCommit = transaction.CommitStandalone();
                Assert.That(firstCommit.EffectResults.Effects[0].CurrentValue, Is.EqualTo(2));
                Assert.That(secondCommit.EffectResults.Effects[0].CurrentValue, Is.EqualTo(2));
                Assert.That(worldCommand.ApplyCount, Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(gameEvent);
            }
        }

        [Test]
        public void CheckedChoice_AcceptsFailureWithoutApplyingSuccessEffects()
        {
            var settlement = new SettlementInstance();
            var hunter = new HunterInstance(null, 8124) { Understanding = 0 };
            settlement.Hunters.Add(hunter);
            var eventSystem = new EventSystem(settlement, new SequenceRandom(0));
            EventData gameEvent = CreateCheckedEvent();

            try
            {
                PlayableEventChoiceTransaction transaction = eventSystem.PrepareChoice(gameEvent, 0, hunter);
                EventResolutionResult result = transaction.Commit();

                Assert.That(result.Success, Is.False);
                Assert.That(hunter.Insanity, Is.EqualTo(1));
                Assert.That(settlement.GetResource("碎石"), Is.Zero);
                Assert.That(settlement.HasDiscoveredMaterial("碎石"), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameEvent);
            }
        }

        [Test]
        public void CheckedChoice_RejectsInvalidPreparedPhysicalRolls()
        {
            var settlement = new SettlementInstance();
            var hunter = new HunterInstance(null, 8125) { Understanding = 0, Willpower = 1, WillpowerMax = 1 };
            settlement.Hunters.Add(hunter);
            var eventSystem = new EventSystem(settlement, new SequenceRandom(0));
            EventData gameEvent = CreateCheckedEvent();

            try
            {
                Assert.That(eventSystem.PrepareChoice(gameEvent, 0, hunter, 0), Is.Null);
                Assert.That(eventSystem.PrepareChoice(gameEvent, 0, hunter, 11), Is.Null);
                PlayableEventChoiceTransaction transaction = eventSystem.PrepareChoice(gameEvent, 0, hunter, 5);
                Assert.That(transaction, Is.Not.Null);
                Assert.That(transaction.RollValue, Is.EqualTo(5));
                Assert.That(transaction.TryReroll(11), Is.False);
                Assert.That(hunter.Willpower, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(gameEvent);
            }
        }

        [Test]
        public void CombatNarrative_PublishesStructuredEncounterRequest()
        {
            var eventSystem = new EventSystem(new SettlementInstance(), new SequenceRandom(0));
            EventData gameEvent = ScriptableObject.CreateInstance<EventData>();
            gameEvent.name = "SettlementCombatEvent";
            gameEvent.eventType = GameEventType.Combat;
            gameEvent.combatEncounterId = "first-showdown";
            EventData child = ScriptableObject.CreateInstance<EventData>();
            gameEvent.chainedEvents.Add(child);
            PlayableEventEncounterRequestedEvent received = default;
            int receivedCount = 0;
            System.Action<PlayableEventEncounterRequestedEvent> handler = evt =>
            {
                received = evt;
                receivedCount++;
            };
            EventBus.Subscribe(handler);
            try
            {
                IReadOnlyList<EventData> chain = eventSystem.ResolveNarrativeStandalone(gameEvent);

                Assert.That(receivedCount, Is.EqualTo(1));
                Assert.That(received.EncounterId, Is.EqualTo("first-showdown"));
                Assert.That(received.SourceEventId, Is.EqualTo(gameEvent.name));
                Assert.That(chain, Is.Empty);
            }
            finally
            {
                EventBus.Unsubscribe(handler);
                Object.DestroyImmediate(child);
                Object.DestroyImmediate(gameEvent);
            }
        }

        [Test]
        public void ChoiceCommit_ExposesPartialEffectResultsWithoutHidingLaterEffects()
        {
            var settlement = new SettlementInstance();
            var hunter = new HunterInstance(null, 8126) { Name = "记录者" };
            settlement.Hunters.Add(hunter);
            EventData gameEvent = ScriptableObject.CreateInstance<EventData>();
            gameEvent.name = "partial-effect-event";
            gameEvent.options.Add(new EventOption
            {
                optionText = "执行多效果",
                successEffects = new List<EventEffect>
                {
                    new EventEffect { effectType = EventEffectType.AddResource, targetName = "碎石", value = 1 },
                    new EventEffect { effectType = EventEffectType.UnlockInvention, targetName = "missing-invention" },
                    new EventEffect { effectType = EventEffectType.AddInsanity, targetName = "selected", value = 1 }
                }
            });

            try
            {
                PlayableEventChoiceTransaction transaction = new EventSystem(settlement, new SequenceRandom(0)).PrepareChoice(gameEvent, 0, hunter);
                PlayableEventCommitResult result = transaction.CommitStandalone();

                Assert.That(result.EffectResults.Count, Is.EqualTo(3));
                Assert.That(result.EffectResults.AppliedCount, Is.EqualTo(2));
                Assert.That(result.EffectResults.FailedCount, Is.EqualTo(1));
                Assert.That(result.EffectResults.Effects[1].EventId, Is.EqualTo("partial-effect-event"));
                Assert.That(result.EffectResults.Effects[1].Reason, Does.Contain("未注册发明"));
                Assert.That(settlement.GetResource("碎石"), Is.EqualTo(1));
                Assert.That(hunter.Insanity, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(gameEvent);
            }
        }

        [Test]
        public void EffectBatch_CapturesStableSnapshotWhenSourceListChanges()
        {
            var source = new List<PlayableEventEffectResult>
            {
                new(0, new EventEffect { effectType = EventEffectType.AddResource, targetName = "碎石" }, PlayableEventEffectStatus.Applied, string.Empty)
            };
            var batch = new PlayableEventEffectBatchResult(source);

            source.Add(new PlayableEventEffectResult(1, null, PlayableEventEffectStatus.Failed, "后续失败"));

            Assert.That(batch.Count, Is.EqualTo(1));
            Assert.That(batch.AppliedCount, Is.EqualTo(1));
            Assert.That(batch.FailedCount, Is.Zero);
            Assert.That(batch.Effects[0].TargetName, Is.EqualTo("碎石"));
        }

        private static EventData CreateCheckedEvent()
        {
            var gameEvent = ScriptableObject.CreateInstance<EventData>();
            var option = new EventOption
            {
                optionText = "解读刻痕",
                checkType = CheckType.Understanding,
                checkTarget = 8,
                successText = "找到了石片。",
                failText = "低语侵入意识。",
                successEffects = new List<EventEffect>
                {
                    new EventEffect { effectType = EventEffectType.AddResource, targetName = "碎石", value = 2 }
                },
                failEffects = new List<EventEffect>
                {
                    new EventEffect { effectType = EventEffectType.AddInsanity, value = 1 }
                }
            };
            gameEvent.options.Add(option);
            return gameEvent;
        }

        private sealed class RecordingWorldCommand : IPlayableEventWorldCommand
        {
            public int ApplyCount { get; private set; }

            public bool TryApply(EventEffect effect, out PlayableEventWorldChange change, out string reason)
            {
                ApplyCount++;
                change = new PlayableEventWorldChange("test-target", 2);
                reason = string.Empty;
                return true;
            }
        }

        private sealed class SequenceRandom : IRandomSource
        {
            private readonly Queue<int> values;

            public SequenceRandom(params int[] values)
            {
                this.values = new Queue<int>(values);
            }

            public int Next(int minInclusive, int maxExclusive)
            {
                int offset = values.Count > 0 ? values.Dequeue() : 0;
                return Mathf.Clamp(minInclusive + offset, minInclusive, maxExclusive - 1);
            }

            public double NextDouble() => 0d;
        }
    }
}
