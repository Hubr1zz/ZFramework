using System.Collections.Generic;
using System.Reflection;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableHuntContentBundleTests
    {
        private readonly List<Object> ownedObjects = new();
        private PlayableHuntContentBundle bundle;

        [TearDown]
        public void TearDown()
        {
            bundle?.Dispose();
            PlayableSettlementItemRegistry.Configure(null);
            foreach (Object ownedObject in ownedObjects)
                if (ownedObject != null)
                    Object.DestroyImmediate(ownedObject);
            ownedObjects.Clear();
        }

        [Test]
        public void Bundle_FreezesTileListsNestedRulesAndNoiseProfile()
        {
            EventData dangerEvent = CreateHuntEvent("hunt:danger");
            HexTileData startingTile = CreateTile("tile:start", TileType.Starting, 1);
            HexTileData pooledTile = CreateTile("tile:plain", TileType.Plains, 7);
            PlayableHuntNoiseProfile noiseProfile = CreateNoiseProfile(dangerEvent);
            PlayableHuntContentCatalog catalog = CreateCatalog(startingTile, new List<HexTileData> { pooledTile }, new List<EventData> { dangerEvent }, noiseProfile);

            Assert.That(TryCreateBundle(catalog, new List<PlayableHuntDestination>(), out string reason), Is.True, reason);
            PlayableHuntRoutePlan route = bundle.DefaultRoute;
            SetPrivateField(catalog, "tilePool", new List<HexTileData>());
            pooledTile.spawnWeight = 99;
            SetPrivateField(noiseProfile, "baseNoisePerHunter", 99);

            Assert.That(route.TilePool, Has.Count.EqualTo(1));
            Assert.That(route.TilePool[0], Is.Not.SameAs(pooledTile));
            Assert.That(route.TilePool[0].spawnWeight, Is.EqualTo(7));
            Assert.That(route.NoiseProfile, Is.Not.SameAs(noiseProfile));
            Assert.That(route.IsUsable, Is.True);
        }

        [Test]
        public void Managers_BindDistinctRoutesWithoutCrossMutation()
        {
            EventData dangerEvent = CreateHuntEvent("hunt:danger");
            PlayableHuntContentCatalog defaultCatalog = CreateCatalog(CreateTile("tile:start", TileType.Starting, 1), new List<HexTileData> { CreateTile("tile:default", TileType.Plains, 2) }, new List<EventData> { dangerEvent }, CreateNoiseProfile(dangerEvent));
            PlayableHuntContentCatalog northCatalog = CreateCatalog(CreateTile("tile:north-start", TileType.Starting, 1), new List<HexTileData> { CreateTile("tile:north", TileType.Forest, 3) }, new List<EventData> { dangerEvent }, CreateNoiseProfile(dangerEvent));
            PlayableHuntDestination destination = CreateDestination("north", northCatalog);

            Assert.That(TryCreateBundle(defaultCatalog, new List<PlayableHuntDestination> { destination }, out string reason), Is.True, reason);
            Assert.That(bundle.TryResolveRoute("north", 1, out PlayableHuntRoutePlan northRoute, out reason), Is.True, reason);
            HuntManager defaultManager = CreateManager(11);
            HuntManager northManager = CreateManager(22);
            Assert.That(defaultManager.TryBindContent(bundle.DefaultRoute, out reason), Is.True, reason);
            Assert.That(defaultManager.TryBindContent(bundle.DefaultRoute, out reason), Is.True, reason);
            Assert.That(northManager.TryBindContent(northRoute, out reason), Is.True, reason);

            Assert.That(defaultManager.BoundContentBundle, Is.SameAs(bundle));
            Assert.That(northManager.BoundContentBundle, Is.SameAs(bundle));
            Assert.That(defaultManager.BoundRoute, Is.Not.SameAs(northManager.BoundRoute));
            Assert.That(defaultManager.TilePool[0].ContentId, Is.EqualTo("tile:default"));
            Assert.That(northManager.TilePool[0].ContentId, Is.EqualTo("tile:north"));
            Assert.That(defaultManager.TryBindContent(northRoute, out _), Is.False);
        }

        [Test]
        public void Manager_RejectsBindingAfterRuntimeStarts()
        {
            EventData dangerEvent = CreateHuntEvent("hunt:danger");
            PlayableHuntContentCatalog catalog = CreateCatalog(CreateTile("tile:start", TileType.Starting, 1), new List<HexTileData> { CreateTile("tile:plain", TileType.Plains, 1) }, new List<EventData> { dangerEvent }, CreateNoiseProfile(dangerEvent));
            Assert.That(TryCreateBundle(catalog, new List<PlayableHuntDestination>(), out string reason), Is.True, reason);
            HuntManager manager = CreateManager(33);
            manager.OnEnter(new List<HunterInstance>(), 1);

            Assert.That(manager.TryBindContent(bundle.DefaultRoute, out reason), Is.False);
            Assert.That(reason, Does.Contain("运行态"));
        }

        [Test]
        public void Bundle_RejectsNonResourceHarvestReference()
        {
            EventData dangerEvent = CreateHuntEvent("hunt:danger");
            ItemData weapon = Own(ScriptableObject.CreateInstance<ItemData>());
            weapon.ConfigureContentId("item:weapon");
            weapon.itemName = "weapon";
            weapon.itemType = ItemType.Weapon;
            PlayableSettlementItemRegistry.Configure(new[] { weapon });
            HexTileData resourceTile = CreateTile("tile:resource", TileType.Forest, 1);
            resourceTile.resourcePoints.Add(new ResourcePointConfig { resource = weapon, spawnWeight = 1, drawCount = 1, maxPerTile = 1 });
            PlayableHuntContentCatalog catalog = CreateCatalog(CreateTile("tile:start", TileType.Starting, 1), new List<HexTileData> { resourceTile }, new List<EventData> { dangerEvent }, CreateNoiseProfile(dangerEvent));

            Assert.That(TryCreateBundle(catalog, new List<PlayableHuntDestination>(), out string reason), Is.False);
            Assert.That(reason, Does.Contain("计划外资源"));
        }

        private bool TryCreateBundle(PlayableHuntContentCatalog catalog, IReadOnlyList<PlayableHuntDestination> destinations, out string reason)
        {
            return PlayableHuntContentBundle.TryCreateSnapshot(catalog, destinations, out bundle, out reason);
        }

        private static HuntManager CreateManager(int seed) => new(new EventSystem(new SettlementInstance(), new SystemRandomSource(seed)), seed);

        private HexTileData CreateTile(string id, TileType type, int weight)
        {
            HexTileData tile = Own(ScriptableObject.CreateInstance<HexTileData>());
            tile.ConfigureContentId(id);
            tile.tileName = id;
            tile.tileType = type;
            tile.spawnWeight = weight;
            return tile;
        }

        private EventData CreateHuntEvent(string id)
        {
            EventData huntEvent = Own(ScriptableObject.CreateInstance<EventData>());
            huntEvent.ConfigureContentId(id);
            huntEvent.name = id;
            huntEvent.eventName = id;
            huntEvent.category = EventCategory.Hunt;
            huntEvent.drawWeight = 1;
            huntEvent.minYear = 1;
            huntEvent.maxYear = 0;
            return huntEvent;
        }

        private PlayableHuntContentCatalog CreateCatalog(HexTileData startingTile, List<HexTileData> tiles, List<EventData> events, PlayableHuntNoiseProfile profile)
        {
            PlayableHuntContentCatalog catalog = Own(ScriptableObject.CreateInstance<PlayableHuntContentCatalog>());
            SetPrivateField(catalog, "startingTile", startingTile);
            SetPrivateField(catalog, "tilePool", tiles);
            SetPrivateField(catalog, "eventPool", events);
            SetPrivateField(catalog, "noiseProfile", profile);
            return catalog;
        }

        private static PlayableHuntDestination CreateDestination(string id, PlayableHuntContentCatalog catalog)
        {
            var destination = new PlayableHuntDestination();
            SetPrivateField(destination, "destinationId", id);
            SetPrivateField(destination, "displayName", id);
            SetPrivateField(destination, "huntContent", catalog);
            return destination;
        }

        private static PlayableHuntNoiseProfile CreateNoiseProfile(EventData dangerEvent)
        {
            var profile = new PlayableHuntNoiseProfile();
            SetPrivateField(profile, "profileId", "noise:test");
            SetPrivateField(profile, "dangerEvents", new List<EventData> { dangerEvent });
            return profile;
        }

        private T Own<T>(T ownedObject) where T : Object
        {
            ownedObjects.Add(ownedObject);
            return ownedObject;
        }

        private static void SetPrivateField(object target, string fieldName, object value) => target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
