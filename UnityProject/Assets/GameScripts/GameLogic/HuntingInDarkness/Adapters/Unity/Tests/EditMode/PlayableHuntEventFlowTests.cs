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
        private const string SymptomCatalogPath = "Assets/GameScripts/GameLogic/HuntingInDarkness/Content/Settlement/Symptoms/PlayableSymptomCatalog.asset";

        [SetUp]
        public void SetUp()
        {
            PlayableSymptomRuntime.Configure(AssetDatabase.LoadAssetAtPath<PlayableSymptomCatalog>(SymptomCatalogPath));
            PlayableEventTableRuntime.ClearCache();
        }

        [TearDown]
        public void TearDown()
        {
            PlayableEventTableRuntime.ClearCache();
            PlayableSymptomRuntime.Configure(null);
        }

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

            Assert.That(events, Has.Count.GreaterThanOrEqualTo(14));
            Assert.That(events.Exists(gameEvent => gameEvent.options.Exists(option => option.checkPresentation == EventCheckPresentationKind.PhysicalDice)), Is.True);
            Assert.That(events.Exists(gameEvent => gameEvent.options.Exists(option => option.checkPresentation == EventCheckPresentationKind.DrawCards)), Is.True);
            Assert.That(events.Exists(gameEvent => gameEvent.options.Exists(option => option.checkPresentation == EventCheckPresentationKind.FlipCards)), Is.True);
            Assert.That(events.Exists(gameEvent => gameEvent.options.Exists(option => option.checkPresentation == EventCheckPresentationKind.OldMaid)), Is.True);
            Assert.That(events.Exists(gameEvent => gameEvent.options.Exists(option => option.failEffects.Exists(effect => effect.effectType == EventEffectType.AddRecoverableWound && effect.targetName == "selected" && effect.bodyPart == "legs"))), Is.True);
            EventData quarry = events.Single(gameEvent => gameEvent.ContentId == "hunt_breathing_quarry");
            EventEffect quarryWorldEffect = quarry.options[1].failEffects.Single(effect => effect.effectType == EventEffectType.ExhaustCurrentHuntTileResources);
            Assert.That(quarryWorldEffect.targetName, Is.Empty);
            Assert.That(quarryWorldEffect.bodyPart, Is.Empty);
            Assert.That(quarryWorldEffect.value, Is.Zero);
            EventData cache = events.Single(gameEvent => gameEvent.ContentId == "hunt_buried_cache");
            EventEffect itemReward = cache.options[0].successEffects.Single(effect => effect.effectType == EventEffectType.AddItem);
            Assert.That(itemReward.targetName, Is.EqualTo("weathered_field_dressing"));
            Assert.That(PlayableItemTableRuntime.GetItems().Single(item => item.ContentId == itemReward.targetName).itemType, Is.EqualTo(ItemType.Consumable));
            EventData rescue = events.Single(gameEvent => gameEvent.ContentId == "hunt_lost_survivor");
            EventEffect populationReward = rescue.options[0].successEffects.Single(effect => effect.effectType == EventEffectType.RescuePopulation);
            Assert.That(populationReward.value, Is.EqualTo(1));
            Assert.That(events.Select(gameEvent => gameEvent.ContentId), Is.SupersetOf(new[]
            {
                "hunt_sap_suture", "hunt_carapace_cairn", "hunt_white_hair_lure",
                "hunt_root_pulse", "hunt_rust_burial", "hunt_worm_rain", "hunt_lost_survivor"
            }));
        }

        [Test]
        public void HuntTable_RustBurialAddsTriggeredFollowUpContract()
        {
            IReadOnlyList<EventTableRecord> records = new JsonEventTableSource("HuntingInDarkness/Tables/hunt-events").Load();
            Assert.That(records, Has.Count.EqualTo(16));
            Assert.That(records.Count(record => record.category == "Hunt"), Is.EqualTo(15));
            Assert.That(records.Count(record => record.category == "Triggered"), Is.EqualTo(1));

            EventTableRecord parentRecord = records.Single(record => record.id == "hunt_rust_burial");
            EventTableRecord childRecord = records.Single(record => record.id == "hunt_rust_burial_open_eyes");
            Assert.That(parentRecord.options, Has.Count.EqualTo(2));
            Assert.That(parentRecord.options[0].successChainIds, Is.Empty);
            Assert.That(parentRecord.options[1].successChainIds, Is.EqualTo(new[] { childRecord.id }));
            Assert.That(childRecord.category, Is.EqualTo("Triggered"));
            Assert.That(childRecord.eventType, Is.EqualTo("Choice"));
            Assert.That(childRecord.minYear, Is.EqualTo(parentRecord.minYear));
            Assert.That(childRecord.options, Has.Count.EqualTo(2));

            EventOptionTableRecord riskOption = childRecord.options[0];
            Assert.That(riskOption.checkType, Is.EqualTo("Understanding"));
            Assert.That(riskOption.checkTarget, Is.EqualTo(12));
            Assert.That(riskOption.checkPresentation, Is.EqualTo("FlipCards"));
            Assert.That(riskOption.checkCount, Is.EqualTo(2));
            Assert.That(riskOption.checkSides, Is.EqualTo(10));
            Assert.That(riskOption.checkDeckId, Is.EqualTo("rust-burial-open-eyes"));
            Assert.That(riskOption.successEffects.Single().effectType, Is.EqualTo("AddResource"));
            Assert.That(riskOption.successEffects.Single().targetName, Is.EqualTo("ancient_stone_chip"));
            Assert.That(riskOption.successEffects.Single().value, Is.EqualTo(1));
            Assert.That(riskOption.failEffects.Single().effectType, Is.EqualTo("AddRecoverableWound"));
            Assert.That(riskOption.failEffects.Single().targetName, Is.EqualTo("selected"));
            Assert.That(riskOption.failEffects.Single().bodyPart, Is.EqualTo("arms"));
            Assert.That(riskOption.failEffects.Single().value, Is.EqualTo(1));

            EventOptionTableRecord safeOption = childRecord.options[1];
            Assert.That(safeOption.checkType, Is.EqualTo("None"));
            Assert.That(safeOption.alwaysAvailable, Is.False);
            Assert.That(safeOption.conditions.Single().conditionKind, Is.EqualTo("MinimumResource"));
            Assert.That(safeOption.conditions.Single().key, Is.EqualTo("metal_fragment"));
            Assert.That(safeOption.conditions.Single().value, Is.EqualTo(1));
            Assert.That(safeOption.successEffects.Single().effectType, Is.EqualTo("RemoveResource"));
            Assert.That(safeOption.successEffects.Single().targetName, Is.EqualTo("metal_fragment"));
            Assert.That(safeOption.successEffects.Single().value, Is.EqualTo(1));

            EventData parent = PlayableEventTableRuntime.GetEvents().Single(gameEvent => gameEvent.ContentId == parentRecord.id);
            EventData child = PlayableEventTableRuntime.GetEvents().Single(gameEvent => gameEvent.ContentId == childRecord.id);
            Assert.That(parent.options[1].successChain, Has.Count.EqualTo(1));
            Assert.That(parent.options[1].successChain[0], Is.SameAs(child));
            Assert.That(child.category, Is.EqualTo(EventCategory.Triggered));
            Assert.That(child.options[0].successEffects.Single().targetName, Is.EqualTo("ancient_stone_chip"));
            Assert.That(child.options[0].failEffects.Single().bodyPart, Is.EqualTo("arms"));
        }

        [Test]
        public void ExtendHunt_MergesRouteContentAndOverridesByStableId()
        {
            EventData routeEvent = CreateEvent("hunt_echoing_tracks");
            routeEvent.ConfigureContentId("hunt_echoing_tracks");
            EventData settlementEvent = CreateEvent("settlement-only");
            settlementEvent.ConfigureContentId("settlement-only");
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
        public void ExtendHunt_RejectsEveryRouteEventSharingADuplicateStableId()
        {
            EventData first = CreateEvent("route-duplicate-first");
            EventData second = CreateEvent("route-duplicate-second");
            first.ConfigureContentId("route-duplicate");
            second.ConfigureContentId("route-duplicate");

            try
            {
                List<EventData> merged = PlayableEventTableRuntime.ExtendHunt(new[] { first, second });

                Assert.That(merged.Exists(gameEvent => gameEvent.ContentId == "route-duplicate"), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void ExtendHunt_TableEventCanAuthoritativelyReplaceRejectedRouteDuplicates()
        {
            EventData first = CreateEvent("route-duplicate-first");
            EventData second = CreateEvent("route-duplicate-second");
            first.ConfigureContentId("hunt_echoing_tracks");
            second.ConfigureContentId("hunt_echoing_tracks");

            try
            {
                List<EventData> merged = PlayableEventTableRuntime.ExtendHunt(new[] { first, second });
                EventData resolved = merged.Single(gameEvent => gameEvent.ContentId == "hunt_echoing_tracks");

                Assert.That(resolved, Is.Not.SameAs(first));
                Assert.That(resolved, Is.Not.SameAs(second));
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
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
