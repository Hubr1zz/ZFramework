using System.Collections.Generic;
using System.Linq;
using HuntingInDarkness.ContentTables;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableHuntEventFlowTests
    {
        private const string OutskirtsPath = "Assets/GameScripts/GameLogic/HuntingInDarkness/Content/Hunt/Destinations/StoneForestOutskirts.asset";
        private const string MarshPath = "Assets/GameScripts/GameLogic/HuntingInDarkness/Content/Hunt/Destinations/SunkenFungalMarsh.asset";

        [Test]
        public void RouteContent_ProvidesReusableHuntChoiceEvents()
        {
            PlayableHuntContentCatalog outskirts = AssetDatabase.LoadAssetAtPath<PlayableHuntContentCatalog>(OutskirtsPath);
            PlayableHuntContentCatalog marsh = AssetDatabase.LoadAssetAtPath<PlayableHuntContentCatalog>(MarshPath);

            Assert.That(outskirts, Is.Not.Null);
            Assert.That(marsh, Is.Not.Null);
            AssertHuntEventPool(outskirts.EventPool);
            AssertHuntEventPool(marsh.EventPool);
        }

        [Test]
        public void TableContent_ProvidesSharedHuntEventsWithTabletopChecks()
        {
            List<EventData> events = PlayableEventTableRuntime.GetEvents().Where(gameEvent => gameEvent.category == EventCategory.Hunt).ToList();

            Assert.That(events, Has.Count.GreaterThanOrEqualTo(3));
            Assert.That(events.Exists(gameEvent => gameEvent.options.Exists(option => option.checkPresentation == EventCheckPresentationKind.PhysicalDice)), Is.True);
            Assert.That(events.Exists(gameEvent => gameEvent.options.Exists(option => option.checkPresentation == EventCheckPresentationKind.DrawCards)), Is.True);
            Assert.That(events.Exists(gameEvent => gameEvent.options.Exists(option => option.failEffects.Exists(effect => effect.effectType == EventEffectType.AddRecoverableWound && effect.targetName == "selected" && effect.bodyPart == "legs"))), Is.True);
        }

        [Test]
        public void ExtendHunt_MergesRouteContentAndOverridesByStableId()
        {
            EventData routeEvent = CreateEvent("hunt_echoing_tracks");
            EventData settlementEvent = CreateEvent("settlement-only");
            settlementEvent.category = EventCategory.Settlement;

            try
            {
                List<EventData> merged = PlayableEventTableRuntime.ExtendHunt(new[] { routeEvent, settlementEvent });

                Assert.That(merged.Count(gameEvent => gameEvent.name == "hunt_echoing_tracks"), Is.EqualTo(1));
                Assert.That(merged.Single(gameEvent => gameEvent.name == "hunt_echoing_tracks"), Is.Not.SameAs(routeEvent));
                Assert.That(merged.Contains(settlementEvent), Is.False);
                Assert.That(merged.TrueForAll(gameEvent => gameEvent.category == EventCategory.Hunt), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(routeEvent);
                Object.DestroyImmediate(settlementEvent);
            }
        }

        [Test]
        public void RevealNonBoss_WithoutExplicitEvent_DoesNotRollHiddenProbability()
        {
            EventData gameEvent = CreateEvent("GuaranteedHuntEvent");
            var huntEvents = new HuntEventSystem(new FirstRandom()) { HuntEventPool = new List<EventData> { gameEvent } };

            try
            {
                EventData selected = huntEvents.SelectTileRevealEvent(new HexTileInstance { State = TileState.Revealed });

                Assert.That(selected, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(gameEvent);
            }
        }

        [Test]
        public void RevealBoss_SkipsTileAndRandomEvents()
        {
            EventData gameEvent = CreateEvent("ForbiddenBossEvent");
            HexTileData config = ScriptableObject.CreateInstance<HexTileData>();
            config.tileRevealEvent = gameEvent;
            var huntEvents = new HuntEventSystem(new FirstRandom()) { HuntEventPool = new List<EventData> { gameEvent } };

            try
            {
                EventData selected = huntEvents.SelectTileRevealEvent(new HexTileInstance { State = TileState.Revealed, HasBossEncounter = true, Config = config });

                Assert.That(selected, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(gameEvent);
            }
        }

        [Test]
        public void MoveEvent_DoesNotRollHiddenProbability()
        {
            EventData gameEvent = CreateEvent("OncePerTileEvent");
            var huntEvents = new HuntEventSystem(new FirstRandom()) { HuntEventPool = new List<EventData> { gameEvent } };
            var tile = new HexTileInstance { State = TileState.Revealed, AxialCoord = new Vector2Int(1, 0) };

            try
            {
                Assert.That(huntEvents.SelectSquadMoveEvent(tile), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(gameEvent);
            }
        }

        [Test]
        public void ExplicitRevealEvent_IsReturnedWithoutPoolRoll()
        {
            EventData gameEvent = CreateEvent("FutureHuntEvent");
            gameEvent.minYear = 2;
            gameEvent.maxYear = 3;
            var huntEvents = new HuntEventSystem(new FirstRandom()) { HuntEventPool = new List<EventData> { gameEvent } };
            HexTileData config = ScriptableObject.CreateInstance<HexTileData>();
            config.tileRevealEvent = gameEvent;
            var tile = new HexTileInstance { State = TileState.Revealed, Config = config };

            try
            {
                Assert.That(huntEvents.SelectTileRevealEvent(tile), Is.SameAs(gameEvent));
            }
            finally
            {
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(gameEvent);
            }
        }

        [Test]
        public void InputGuard_NestedOwnersReleaseIndependently()
        {
            const int firstOwner = 73101;
            const int secondOwner = 73102;
            PlayableHuntInputGuard.Release(firstOwner);
            PlayableHuntInputGuard.Release(secondOwner);

            try
            {
                PlayableHuntInputGuard.Acquire(firstOwner);
                PlayableHuntInputGuard.Acquire(firstOwner);
                PlayableHuntInputGuard.Acquire(secondOwner);
                Assert.That(PlayableHuntInputGuard.IsBlocked, Is.True);

                PlayableHuntInputGuard.Release(firstOwner);
                Assert.That(PlayableHuntInputGuard.IsBlocked, Is.True);

                PlayableHuntInputGuard.Release(secondOwner);
                Assert.That(PlayableHuntInputGuard.IsBlocked, Is.False);
            }
            finally
            {
                PlayableHuntInputGuard.Release(firstOwner);
                PlayableHuntInputGuard.Release(secondOwner);
            }
        }

        private static void AssertHuntEventPool(IReadOnlyList<EventData> events)
        {
            Assert.That(events, Has.Count.GreaterThanOrEqualTo(3));
            foreach (EventData gameEvent in events)
            {
                Assert.That(gameEvent, Is.Not.Null);
                Assert.That(gameEvent.category, Is.EqualTo(EventCategory.Hunt));
                Assert.That(gameEvent.eventType, Is.EqualTo(GameEventType.Choice));
                Assert.That(gameEvent.options, Has.Count.GreaterThanOrEqualTo(2));
                Assert.That(gameEvent.options.TrueForAll(option => option != null && !string.IsNullOrWhiteSpace(option.optionText)), Is.True);
            }
            Assert.That(new List<EventData>(events).Exists(gameEvent => gameEvent.options.Exists(option => option.checkType != CheckType.None && option.successEffects.Count + option.failEffects.Count > 0)), Is.True);
        }

        private static EventData CreateEvent(string name)
        {
            EventData gameEvent = ScriptableObject.CreateInstance<EventData>();
            gameEvent.name = name;
            gameEvent.eventName = name;
            gameEvent.category = EventCategory.Hunt;
            gameEvent.drawWeight = 1;
            return gameEvent;
        }

        private sealed class FirstRandom : IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
        }
    }
}
