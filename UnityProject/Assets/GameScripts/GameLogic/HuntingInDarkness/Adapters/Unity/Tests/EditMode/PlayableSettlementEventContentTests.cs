using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableSettlementEventContentTests
    {
        private const string CatalogPath = "Assets/GameScripts/GameLogic/HuntingInDarkness/Content/Settlement/PlayableSettlementContentCatalog.asset";

        [Test]
        public void Timeline_WithAlternatives_DoesNotRepeatMostRecentRandomEvent()
        {
            var settlement = new SettlementInstance { CurrentYear = 4 };
            settlement.Timeline.Add(new AnnalEntry { Year = 4, EventId = "First", EntryType = TimelineEntryType.Random });
            EventData first = CreateEvent("First");
            EventData second = CreateEvent("Second");
            var timeline = new TimelineSystem(settlement, new FirstRandom()) { RandomEventPool = new List<EventData> { first, second } };

            try
            {
                List<EventData> events = timeline.GetEventsForYear(5);

                Assert.That(events, Has.Count.EqualTo(1));
                Assert.That(events[0].name, Is.EqualTo("Second"));
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void ContentCatalog_ProvidesSustainableChoiceEventPool()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PlayableSettlementContentCatalog>(CatalogPath);
            var manager = new SettlementManager(1);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.ApplyTo(manager), Is.True);
            Assert.That(PlayableSettlementItemRegistry.TryGet("黑盐", out ItemData blackSalt), Is.True);
            Assert.That(blackSalt.name, Is.EqualTo("black_salt"));
            Assert.That(PlayableSettlementItemRegistry.TryGet("盐纹护符", out ItemData saltWard), Is.True);
            Assert.That(saltWard.name, Is.EqualTo("salt_ward"));
            Assert.That(saltWard.keywords, Is.EquivalentTo(new[] { "ritual", "ward" }));
            Assert.That(saltWard.armorStats.armorBody, Is.EqualTo(1));
            Assert.That(manager.Workshop.AllRecipes.Exists(recipe => recipe != null && recipe.recipeName == "刻制盐纹护符" && recipe.outputItem == saltWard), Is.True);
            Assert.That(PlayableSettlementInventionRegistry.TryGet("faith", out InventionData faith), Is.True);
            Assert.That(PlayableSettlementInventionRegistry.TryGet("ritual", out InventionData ritual), Is.True);
            Assert.That(ritual.prerequisites, Is.EqualTo(new[] { faith }));
            Assert.That(PlayableSettlementInventionRegistry.TryGet("paper-and-pen", out InventionData paperAndPen), Is.True);
            Assert.That(PlayableSettlementInventionRegistry.TryGet("plant-knowledge", out InventionData plantKnowledge), Is.True);
            Assert.That(plantKnowledge.prerequisites, Is.EqualTo(new[] { paperAndPen }));
            Assert.That(plantKnowledge.actionEffects, Has.Count.EqualTo(1));
            Assert.That(plantKnowledge.actionEffects[0].kind, Is.EqualTo(InventionActionEffectKind.ModifyHarvestHitChance));
            Assert.That(manager.Timeline.RandomEventPool, Has.Count.GreaterThanOrEqualTo(5));
            Assert.That(manager.Timeline.RandomEventPool.Exists(gameEvent => gameEvent != null && gameEvent.name == "random_stone_vigil"), Is.True);
            Assert.That(manager.Timeline.RandomEventPool.FindAll(gameEvent => gameEvent != null && gameEvent.eventType == GameEventType.Choice && gameEvent.maxYear <= 0), Has.Count.GreaterThanOrEqualTo(3));
            Assert.That(manager.Timeline.RandomEventPool.Exists(gameEvent => gameEvent != null && gameEvent.options.Exists(option => option != null && option.checkType != CheckType.None && option.successEffects.Count > 0 && option.failEffects.Count > 0)), Is.True);
            foreach (EventData gameEvent in manager.Timeline.RandomEventPool)
                Assert.That(gameEvent.options == null || gameEvent.options.TrueForAll(option => option != null && !string.IsNullOrWhiteSpace(option.optionText)), Is.True);
        }

        private static EventData CreateEvent(string name)
        {
            var gameEvent = ScriptableObject.CreateInstance<EventData>();
            gameEvent.name = name;
            gameEvent.minYear = 1;
            gameEvent.maxYear = 99;
            gameEvent.drawWeight = 1;
            gameEvent.category = EventCategory.Random;
            return gameEvent;
        }

        private sealed class FirstRandom : HuntingInDarkness.GameCore.Foundation.IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
        }
    }
}
