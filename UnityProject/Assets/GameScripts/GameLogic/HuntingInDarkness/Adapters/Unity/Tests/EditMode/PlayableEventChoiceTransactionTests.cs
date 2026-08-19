using System.Collections.Generic;
using Core;
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
        public void CheckedChoice_AcceptsFailureWithoutApplyingSuccessEffects()
        {
            var settlement = new SettlementInstance();
            var hunter = new HunterInstance(null, 8124) { Understanding = 0 };
            var eventSystem = new EventSystem(settlement, new SequenceRandom(0));
            EventData gameEvent = CreateCheckedEvent();

            try
            {
                PlayableEventChoiceTransaction transaction = eventSystem.PrepareChoice(gameEvent, 0, hunter);
                EventResolutionResult result = transaction.Commit();

                Assert.That(result.Success, Is.False);
                Assert.That(hunter.Insanity, Is.EqualTo(1));
                Assert.That(settlement.GetResource("碎石"), Is.Zero);
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
