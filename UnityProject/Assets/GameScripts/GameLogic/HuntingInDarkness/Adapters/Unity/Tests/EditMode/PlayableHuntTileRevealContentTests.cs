using System.Collections.Generic;
using System.Linq;
using HuntingInDarkness.Data;
using NUnit.Framework;
using UnityEditor;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableHuntTileRevealContentTests
    {
        private const string ContentRoot = "Assets/GameScripts/GameLogic/HuntingInDarkness/Content";
        private const string TileRoot = ContentRoot + "/Hunt/Tiles/";

        [Test]
        public void ProductionTiles_KeepExplicitEventsAndNoiseFallbackPlayable()
        {
            HexTileData startingCamp = LoadTile("StartingCamp");
            HexTileData statuePlains = LoadTile("StatuePlains");
            HexTileData mushroomForest = LoadTile("MushroomForest");
            HexTileData shallowSwamp = LoadTile("ShallowSwamp");
            HexTileData brokenRuins = LoadTile("BrokenRuins");

            Assert.That(startingCamp.tileRevealEvent, Is.Null);
            Assert.That(mushroomForest.tileRevealEvent, Is.Not.Null);
            Assert.That(mushroomForest.tileRevealEvent.ContentId, Is.EqualTo("hunt_fungal_whisper"));
            Assert.That(statuePlains.tileRevealEvent, Is.Null, "成组雕像地块应保留普通噪音入口");
            Assert.That(shallowSwamp.tileRevealEvent, Is.Null, "沼泽应保留普通噪音入口");
            Assert.That(brokenRuins.tileRevealEvent, Is.Null, "废墟应保留普通噪音入口");

            AssertRevealEvent(mushroomForest.tileRevealEvent);
            Assert.That(mushroomForest.bossEncounterWeight, Is.Zero);
        }

        [Test]
        public void ProductionHuntEventResourceEffects_UseResolvableStableItemIds()
        {
            var itemIds = new HashSet<string>(AssetDatabase.FindAssets("t:ItemData", new[] { ContentRoot }).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<ItemData>).Where(item => item != null).Select(item => item.ContentId));
            List<EventData> huntEvents = AssetDatabase.FindAssets("t:EventData", new[] { ContentRoot + "/Hunt/Events" }).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<EventData>).Where(gameEvent => gameEvent != null && gameEvent.category == EventCategory.Hunt).ToList();

            Assert.That(huntEvents, Is.Not.Empty);
            foreach (EventData gameEvent in huntEvents)
            foreach (EventEffect effect in GetEffects(gameEvent).Where(effect => effect.effectType == EventEffectType.AddResource || effect.effectType == EventEffectType.RemoveResource))
                Assert.That(itemIds.Contains(effect.targetName), Is.True, $"{gameEvent.ContentId} 引用了未注册的稳定物品 ID：{effect.targetName}");
        }

        private static HexTileData LoadTile(string name)
        {
            HexTileData tile = AssetDatabase.LoadAssetAtPath<HexTileData>(TileRoot + name + ".asset");
            Assert.That(tile, Is.Not.Null, name);
            return tile;
        }

        private static void AssertRevealEvent(EventData gameEvent)
        {
            Assert.That(gameEvent, Is.Not.Null);
            Assert.That(gameEvent.HasExplicitContentId, Is.True);
            Assert.That(gameEvent.category, Is.EqualTo(EventCategory.Hunt));
            Assert.That(gameEvent.eventType, Is.EqualTo(GameEventType.Choice));
            Assert.That(gameEvent.minYear, Is.LessThanOrEqualTo(1));
            Assert.That(gameEvent.maxYear == 0 || gameEvent.maxYear >= 1, Is.True);
            Assert.That(gameEvent.options, Has.Count.GreaterThanOrEqualTo(2));
        }

        private static IEnumerable<EventEffect> GetEffects(EventData gameEvent)
        {
            foreach (EventEffect effect in gameEvent.immediateEffects)
                yield return effect;
            foreach (EventOption option in gameEvent.options)
            {
                foreach (EventEffect effect in option.successEffects)
                    yield return effect;
                foreach (EventEffect effect in option.failEffects)
                    yield return effect;
            }
        }
    }
}
