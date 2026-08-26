using System.Collections.Generic;
using System.Reflection;
using HuntingInDarkness.ContentTables;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableSettlementEventContentTests
    {
        private const string CatalogPath = "Assets/GameScripts/GameLogic/HuntingInDarkness/Content/Settlement/PlayableSettlementContentCatalog.asset";
        private const string SymptomCatalogPath = "Assets/GameScripts/GameLogic/HuntingInDarkness/Content/Settlement/Symptoms/PlayableSymptomCatalog.asset";
        private static readonly MethodInfo resetSettlementContentRuntimeMethod = typeof(PlayableSettlementContentRuntime).GetMethod("ResetRuntimeState", BindingFlags.Static | BindingFlags.NonPublic);

        [SetUp]
        public void SetUp()
        {
            PlayableSymptomRuntime.Configure(AssetDatabase.LoadAssetAtPath<PlayableSymptomCatalog>(SymptomCatalogPath));
            PlayableEventTableRuntime.ClearCache();
        }

        [TearDown]
        public void TearDown()
        {
            resetSettlementContentRuntimeMethod.Invoke(null, null);
            PlayableEventTableRuntime.ClearCache();
            PlayableSymptomRuntime.Configure(null);
        }

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
            Assert.That(PlayableSettlementInventionRegistry.TryGet("prayer", out InventionData prayer), Is.True);
            Assert.That(prayer.prerequisites, Is.EqualTo(new[] { faith }));
            Assert.That(prayer.activeEffects, Has.Count.EqualTo(1));
            Assert.That(manager.Timeline.ResolveEvent(prayer.activeEffects[0].eventId).category, Is.EqualTo(EventCategory.Triggered));
            Assert.That(PlayableSettlementInventionRegistry.TryGet("fire", out InventionData fire), Is.True);
            Assert.That(PlayableSettlementInventionRegistry.TryGet("cooking", out InventionData cooking), Is.True);
            Assert.That(cooking.prerequisites, Is.EqualTo(new[] { fire }));
            Assert.That(cooking.unlockEffects, Has.Count.EqualTo(1));
            Assert.That(cooking.unlockEffects[0].target, Is.EqualTo(InventionEffectTarget.AllLivingAndFutureHunters));
            Assert.That(PlayableSettlementInventionRegistry.TryGet("tools", out InventionData tools), Is.True);
            Assert.That(PlayableSettlementInventionRegistry.TryGet("shelter", out InventionData shelter), Is.True);
            Assert.That(shelter.prerequisites, Is.EqualTo(new[] { tools }));
            Assert.That(shelter.unlockEffects, Has.Count.EqualTo(1));
            Assert.That(shelter.unlockEffects[0].modifierId, Is.EqualTo("shelter:willpower-maximum"));
            Assert.That(manager.Timeline.RandomEventPool, Has.Count.GreaterThanOrEqualTo(5));
            Assert.That(manager.Timeline.RandomEventPool.Exists(gameEvent => gameEvent != null && gameEvent.name == "random_stone_vigil"), Is.True);
            EventData stoneVigil = manager.Timeline.RandomEventPool.Find(gameEvent => gameEvent != null && gameEvent.name == "random_stone_vigil");
            Assert.That(stoneVigil.options.Exists(option => option.successEffects.Exists(effect => effect.effectType == EventEffectType.CreateHuntNoiseLease && effect.targetName == "stone_vigil_risk" && effect.value == 2)), Is.True);
            Assert.That(manager.Timeline.RandomEventPool.Exists(gameEvent => gameEvent != null && gameEvent.name == "random_dark_bargain" && gameEvent.options.Exists(option => option.successEffects.Exists(effect => effect.effectType == EventEffectType.KillHunter))), Is.True);
            Assert.That(manager.Timeline.RandomEventPool.Exists(gameEvent => gameEvent != null && gameEvent.name == "random_falling_beam" && gameEvent.options.Exists(option => option.failEffects.Exists(effect => effect.effectType == EventEffectType.AddRecoverableWound && effect.bodyPart == "arms"))), Is.True);
            Assert.That(manager.Timeline.RandomEventPool.FindAll(gameEvent => gameEvent != null && gameEvent.eventType == GameEventType.Choice && gameEvent.maxYear <= 0), Has.Count.GreaterThanOrEqualTo(3));
            Assert.That(manager.Timeline.RandomEventPool.Exists(gameEvent => gameEvent != null && gameEvent.options.Exists(option => option != null && option.checkType != CheckType.None && option.successEffects.Count > 0 && option.failEffects.Count > 0)), Is.True);
            foreach (EventData gameEvent in manager.Timeline.RandomEventPool)
                Assert.That(gameEvent.options == null || gameEvent.options.TrueForAll(option => option != null && !string.IsNullOrWhiteSpace(option.optionText)), Is.True);
        }

        [Test]
        public void CompatibilityApplyTo_RejectsMissingSymptomDependencyWithoutPartialEventPool()
        {
            PlayableSettlementContentCatalog catalog = AssetDatabase.LoadAssetAtPath<PlayableSettlementContentCatalog>(CatalogPath);
            var manager = new SettlementManager(1);
            PlayableSymptomRuntime.Configure(null);
            LogAssert.Expect(LogType.Error, "[SettlementManager] 兼容 ApplyTo 必须先安装症状内容目录。");

            bool applied = catalog.ApplyTo(manager);

            Assert.That(applied, Is.False);
            Assert.That(manager.Timeline.RandomEventPool, Is.Empty);
        }

        [Test]
        public void TableEvents_ReferenceResourcesByCanonicalContentId()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PlayableSettlementContentCatalog>(CatalogPath);
            var manager = new SettlementManager(1);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.ApplyTo(manager), Is.True);

            var resourceEffects = new List<EventEffect>();
            foreach (EventData gameEvent in PlayableEventTableRuntime.GetEvents())
            {
                CollectResourceEffects(gameEvent.immediateEffects, resourceEffects);
                foreach (EventOption option in gameEvent.options ?? new List<EventOption>())
                {
                    if (option == null) continue;
                    CollectResourceEffects(option.successEffects, resourceEffects);
                    CollectResourceEffects(option.failEffects, resourceEffects);
                }
            }

            Assert.That(resourceEffects, Is.Not.Empty);
            foreach (EventEffect effect in resourceEffects)
            {
                Assert.That(PlayableSettlementItemRegistry.TryGet(effect.targetName, out ItemData item), Is.True, $"未知资源引用：{effect.targetName}");
                Assert.That(effect.targetName, Is.EqualTo(item.ContentId), $"事件资源必须使用稳定 ID：{effect.targetName}");
            }
        }

        private static void CollectResourceEffects(IReadOnlyList<EventEffect> effects, ICollection<EventEffect> destination)
        {
            if (effects == null) return;
            foreach (EventEffect effect in effects)
                if (effect != null && effect.effectType == EventEffectType.AddResource)
                    destination.Add(effect);
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
