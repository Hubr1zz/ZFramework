using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CardGame.ActionQueue;
using Core;
using GameplayBase;
using HuntingInDarkness.ActionFlow.Campaign;
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
    public sealed class PlayableHuntContentBundleTests
    {
        private const string SymptomCatalogPath = "Assets/GameScripts/GameLogic/HuntingInDarkness/Content/Settlement/Symptoms/PlayableSymptomCatalog.asset";
        private const string OutskirtsPath = "Assets/GameScripts/GameLogic/HuntingInDarkness/Content/Hunt/Destinations/StoneForestOutskirts.asset";
        private readonly List<Object> ownedObjects = new();
        private PlayableHuntContentBundle bundle;

        [SetUp]
        public void SetUp()
        {
            PlayableSymptomRuntime.Configure(AssetDatabase.LoadAssetAtPath<PlayableSymptomCatalog>(SymptomCatalogPath));
            PlayableEventTableRuntime.ClearCache();
        }

        [TearDown]
        public void TearDown()
        {
            bundle?.Dispose();
            PlayableSettlementItemRegistry.Configure(null);
            PlayableSettlementInventionRegistry.Configure(null);
            PlayableEventTableRuntime.ClearCache();
            PlayableSymptomRuntime.Configure(null);
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

        [Test]
        public void Bundle_ResolvesTableMaterialByStableId()
        {
            EventData dangerEvent = CreateHuntEvent("hunt:danger");
            ItemData resource = Own(ScriptableObject.CreateInstance<ItemData>());
            resource.ConfigureContentId("item:table-herb");
            resource.itemName = "table herb";
            resource.itemType = ItemType.Resource;
            PlayableSettlementItemRegistry.Configure(new[] { resource });
            HexTileData resourceTile = CreateTile("tile:resource", TileType.Forest, 1);
            resourceTile.resourcePoints.Add(new ResourcePointConfig
            {
                resourcePointId = "point:table-herb",
                displayName = "table herb patch",
                materialPool = new List<ResourceMaterialConfig> { new() { materialId = resource.ContentId, copies = 2 } },
                spawnWeight = 1,
                drawCount = 1,
                maxPerTile = 1
            });
            PlayableHuntContentCatalog catalog = CreateCatalog(CreateTile("tile:start", TileType.Starting, 1), new List<HexTileData> { resourceTile }, new List<EventData> { dangerEvent }, CreateNoiseProfile(dangerEvent));

            Assert.That(TryCreateBundle(catalog, new List<PlayableHuntDestination>(), out string reason), Is.True, reason);
            ResourceMaterialConfig material = bundle.DefaultRoute.TilePool[0].resourcePoints[0].materialPool[0];
            Assert.That(material.material, Is.SameAs(resource));
            Assert.That(material.materialId, Is.EqualTo(resource.ContentId));
        }

        [Test]
        public void Bundle_RejectsUnknownTableMaterialId()
        {
            EventData dangerEvent = CreateHuntEvent("hunt:danger");
            HexTileData resourceTile = CreateTile("tile:resource", TileType.Forest, 1);
            resourceTile.resourcePoints.Add(new ResourcePointConfig
            {
                resourcePointId = "point:unknown",
                materialPool = new List<ResourceMaterialConfig> { new() { materialId = "missing-material", copies = 1 } },
                spawnWeight = 1,
                drawCount = 1,
                maxPerTile = 1
            });
            PlayableHuntContentCatalog catalog = CreateCatalog(CreateTile("tile:start", TileType.Starting, 1), new List<HexTileData> { resourceTile }, new List<EventData> { dangerEvent }, CreateNoiseProfile(dangerEvent));

            Assert.That(TryCreateBundle(catalog, new List<PlayableHuntDestination>(), out string reason), Is.False);
            Assert.That(reason, Does.Contain("计划外素材"));
        }

        [Test]
        public void BundleId_IsDeterministicAndTracksOrderedNestedRules()
        {
            EventData dangerEvent = CreateHuntEvent("hunt:danger");
            ItemData resource = Own(ScriptableObject.CreateInstance<ItemData>());
            resource.ConfigureContentId("item:herb");
            resource.itemName = "herb";
            resource.itemType = ItemType.Resource;
            ItemData unreferencedItem = Own(ScriptableObject.CreateInstance<ItemData>());
            unreferencedItem.ConfigureContentId("item:unreferenced");
            unreferencedItem.itemName = "unreferenced";
            unreferencedItem.itemType = ItemType.Resource;
            unreferencedItem.stackLimit = 1;
            PlayableSettlementItemRegistry.Configure(new[] { resource, unreferencedItem });
            InventionData invention = Own(ScriptableObject.CreateInstance<InventionData>());
            invention.ConfigureContentId("invention:harvest");
            invention.inventionName = "harvest";
            invention.actionEffects.Add(new InventionActionEffect { effectId = "effect:harvest", kind = InventionActionEffectKind.ModifyHarvestHitChance, targetKeyword = "herb", value = 0.1f });
            PlayableSettlementInventionRegistry.Configure(new[] { invention });
            HexTileData first = CreateTile("tile:first", TileType.Forest, 3);
            first.resourcePoints.Add(new ResourcePointConfig { resource = resource, spawnWeight = 2, drawCount = 1, maxPerTile = 1 });
            HexTileData second = CreateTile("tile:second", TileType.Ruins, 5);
            var orderedTiles = new List<HexTileData> { first, second };
            PlayableHuntContentCatalog catalog = CreateCatalog(CreateTile("tile:start", TileType.Starting, 1), orderedTiles, new List<EventData> { dangerEvent }, CreateNoiseProfile(dangerEvent));

            Assert.That(TryCreateBundle(catalog, new List<PlayableHuntDestination>(), out string reason), Is.True, reason);
            Assert.That(PlayableHuntContentBundle.TryCreateSnapshot(catalog, new List<PlayableHuntDestination>(), out PlayableHuntContentBundle equivalent, out reason), Is.True, reason);
            Assert.That(equivalent.BundleId, Is.EqualTo(bundle.BundleId));

            SetPrivateField(catalog, "tilePool", new List<HexTileData> { second, first });
            Assert.That(PlayableHuntContentBundle.TryCreateSnapshot(catalog, new List<PlayableHuntDestination>(), out PlayableHuntContentBundle reordered, out reason), Is.True, reason);
            Assert.That(reordered.BundleId, Is.Not.EqualTo(bundle.BundleId));

            SetPrivateField(catalog, "tilePool", orderedTiles);
            first.resourcePoints[0].drawCount = 4;
            Assert.That(PlayableHuntContentBundle.TryCreateSnapshot(catalog, new List<PlayableHuntDestination>(), out PlayableHuntContentBundle changedRule, out reason), Is.True, reason);
            Assert.That(changedRule.BundleId, Is.Not.EqualTo(bundle.BundleId));

            first.resourcePoints[0].drawCount = 1;
            unreferencedItem.stackLimit = 2;
            Assert.That(PlayableHuntContentBundle.TryCreateSnapshot(catalog, new List<PlayableHuntDestination>(), out PlayableHuntContentBundle changedRegistry, out reason), Is.True, reason);
            Assert.That(changedRegistry.BundleId, Is.Not.EqualTo(bundle.BundleId));

            unreferencedItem.stackLimit = 1;
            unreferencedItem.ConfigureHuntNoise(-1);
            Assert.That(PlayableHuntContentBundle.TryCreateSnapshot(catalog, new List<PlayableHuntDestination>(), out PlayableHuntContentBundle changedEquipmentNoise, out reason), Is.True, reason);
            Assert.That(changedEquipmentNoise.BundleId, Is.Not.EqualTo(bundle.BundleId));

            unreferencedItem.ConfigureHuntNoise(0);
            invention.actionEffects[0].value = 0.2f;
            Assert.That(PlayableHuntContentBundle.TryCreateSnapshot(catalog, new List<PlayableHuntDestination>(), out PlayableHuntContentBundle changedInvention, out reason), Is.True, reason);
            Assert.That(changedInvention.BundleId, Is.Not.EqualTo(bundle.BundleId));

            equivalent.Dispose();
            reordered.Dispose();
            changedRule.Dispose();
            changedRegistry.Dispose();
            changedEquipmentNoise.Dispose();
            changedInvention.Dispose();
        }

        [Test]
        public void Bundle_RejectsNonCanonicalChainedEventObject()
        {
            EventData dangerEvent = CreateHuntEvent("hunt:danger");
            EventData canonicalChild = CreateHuntEvent("hunt:child");
            EventData foreignChild = CreateHuntEvent("hunt:child");
            dangerEvent.chainedEvents.Add(foreignChild);
            PlayableHuntContentCatalog catalog = CreateCatalog(CreateTile("tile:start", TileType.Starting, 1), new List<HexTileData> { CreateTile("tile:plain", TileType.Plains, 1) }, new List<EventData> { dangerEvent, canonicalChild }, CreateNoiseProfile(dangerEvent));

            Assert.That(TryCreateBundle(catalog, new List<PlayableHuntDestination>(), out string reason), Is.False);
            Assert.That(reason.Contains("重复稳定 ID") || reason.Contains("canonical 事件闭包"), Is.True, reason);
        }

        [Test]
        public void Route_ResolvesReachableTriggeredChildAndGrandchildWithoutAddingRoots()
        {
            EventData parent = ResolveTableEvent("hunt_rust_burial");
            EventData danger = ResolveTableEvent("hunt_echoing_tracks");
            PlayableHuntContentCatalog catalog = CreateCatalog(CreateTile("tile:reachable-start", TileType.Starting, 1), new List<HexTileData> { CreateTile("tile:reachable-plain", TileType.Plains, 1) }, new List<EventData> { parent }, CreateNoiseProfile(danger));
            EventData child = ResolveTableEvent("hunt_rust_burial_open_eyes");
            EventData grandchild = ResolveTableEvent("triggered_face_safe_path");
            child.chainedEvents.Add(grandchild);
            try
            {
                Assert.That(TryCreateBundle(catalog, new List<PlayableHuntDestination>(), out string reason), Is.True, reason);
                PlayableHuntRoutePlan route = bundle.DefaultRoute;
                Assert.That(route.HuntEvents.Count, Is.EqualTo(15));
                Assert.That(route.HuntEvents.Contains(child), Is.False);
                Assert.That(route.TryResolveEvent(parent.ContentId, out EventData resolvedParent), Is.True);
                Assert.That(resolvedParent, Is.SameAs(parent));
                Assert.That(route.TryResolveEvent(child.ContentId, out EventData resolvedChild), Is.True);
                Assert.That(resolvedChild, Is.SameAs(child));
                Assert.That(route.TryResolveEvent(grandchild.ContentId, out EventData resolvedGrandchild), Is.True);
                Assert.That(resolvedGrandchild, Is.SameAs(grandchild));
            }
            finally
            {
                child.chainedEvents.Remove(grandchild);
            }
        }

        [Test]
        public void Route_RejectsReachableObjectsSharingAnId()
        {
            EventData parent = ResolveTableEvent("hunt_rust_burial");
            EventData child = ResolveTableEvent("hunt_rust_burial_open_eyes");
            EventData duplicate = CreateHuntEvent(child.ContentId);
            EventData danger = ResolveTableEvent("hunt_echoing_tracks");
            PlayableHuntContentCatalog catalog = CreateCatalog(CreateTile("tile:duplicate-start", TileType.Starting, 1), new List<HexTileData> { CreateTile("tile:duplicate-plain", TileType.Plains, 1) }, new List<EventData> { parent }, CreateNoiseProfile(danger));
            parent.options[1].successChain.Add(duplicate);
            try
            {
                Assert.That(TryCreateBundle(catalog, new List<PlayableHuntDestination>(), out string reason), Is.False);
                Assert.That(reason, Does.Contain("重复稳定 ID"));
            }
            finally
            {
                parent.options[1].successChain.Remove(duplicate);
            }
        }

        [Test]
        public void Route_CycleTerminatesAndRetiredRouteStopsResolving()
        {
            EventData parent = ResolveTableEvent("hunt_rust_burial");
            EventData child = ResolveTableEvent("hunt_rust_burial_open_eyes");
            EventData grandchild = ResolveTableEvent("triggered_face_safe_path");
            EventData danger = ResolveTableEvent("hunt_echoing_tracks");
            PlayableHuntContentCatalog catalog = CreateCatalog(CreateTile("tile:cycle-start", TileType.Starting, 1), new List<HexTileData> { CreateTile("tile:cycle-plain", TileType.Plains, 1) }, new List<EventData> { parent }, CreateNoiseProfile(danger));
            child.chainedEvents.Add(grandchild);
            grandchild.chainedEvents.Add(child);
            try
            {
                Assert.That(TryCreateBundle(catalog, new List<PlayableHuntDestination>(), out string reason), Is.True, reason);
                PlayableHuntRoutePlan route = bundle.DefaultRoute;
                Assert.That(route.TryResolveEvent(grandchild.ContentId, out _), Is.True);
                bundle.Dispose();
                Assert.That(route.TryResolveEvent(child.ContentId, out _), Is.False);
            }
            finally
            {
                child.chainedEvents.Remove(grandchild);
                grandchild.chainedEvents.Remove(child);
            }
        }

        [Test]
        public async Task CampaignRequest_PreservesRouteIdentityAndReactorPreventionLeavesHostUntouched()
        {
            EventData dangerEvent = CreateHuntEvent("hunt:danger");
            PlayableHuntContentCatalog catalog = CreateCatalog(CreateTile("tile:start", TileType.Starting, 1), new List<HexTileData> { CreateTile("tile:plain", TileType.Plains, 1) }, new List<EventData> { dangerEvent }, CreateNoiseProfile(dangerEvent));
            Assert.That(TryCreateBundle(catalog, new List<PlayableHuntDestination>(), out string reason), Is.True, reason);
            var context = new CampaignHuntEntryContext(bundle.DefaultRoute, 3, "departure-token");
            CampaignPhaseTransitionRequest request = CampaignPhaseTransitionRequest.ForHunt(context);
            var host = new RecordingRequestHost();
            using var session = new PlayableCampaignActionSession(host);
            var reactor = new PreventExactRouteReactor(bundle.DefaultRoute);
            System.IDisposable prevention = session.Reactors.RegisterGlobal(reactor);

            CampaignPhaseTransitionResult prevented = await session.TransitionAsync(request);

            Assert.That(prevented.Succeeded, Is.False);
            Assert.That(reactor.ObservedRoute, Is.SameAs(bundle.DefaultRoute));
            Assert.That(host.RequestCount, Is.Zero);

            prevention.Dispose();
            CampaignPhaseTransitionResult committed = await session.TransitionAsync(request);

            Assert.That(committed.Succeeded && committed.Changed, Is.True);
            Assert.That(host.RequestCount, Is.EqualTo(1));
            Assert.That(host.LastRequest.HuntContext.RoutePlan, Is.SameAs(bundle.DefaultRoute));

            var bossHost = new RecordingRequestHost(GamePhase.BossFight);
            using var bossSession = new PlayableCampaignActionSession(bossHost);
            CampaignPhaseTransitionResult invalidSource = await bossSession.TransitionAsync(request);

            Assert.That(invalidSource.Succeeded, Is.False);
            Assert.That(invalidSource.Reason, Does.Contain("营地阶段"));
            Assert.That(bossHost.CurrentPhase, Is.EqualTo(GamePhase.BossFight));
            Assert.That(bossHost.RequestCount, Is.Zero);

            var legacyHost = new RecordingLegacyHost();
            using var legacySession = new PlayableCampaignActionSession(legacyHost);
            CampaignPhaseTransitionResult unsupportedHost = await legacySession.TransitionAsync(request);

            Assert.That(unsupportedHost.Succeeded, Is.False);
            Assert.That(unsupportedHost.Reason, Does.Contain("不支持狩猎入场上下文"));
            Assert.That(legacyHost.CurrentPhase, Is.EqualTo(GamePhase.Settlement));
            Assert.That(legacyHost.RequestCount, Is.Zero);
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

        private static EventData ResolveTableEvent(string id) => PlayableEventTableRuntime.GetEvents().Single(gameEvent => gameEvent.ContentId == id);

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

        private sealed class RecordingRequestHost : ICampaignPhaseTransitionHost, ICampaignPhaseTransitionRequestHost
        {
            public RecordingRequestHost(GamePhase currentPhase = GamePhase.Settlement)
            {
                CurrentPhase = currentPhase;
            }

            public GamePhase CurrentPhase { get; private set; }
            public int RequestCount { get; private set; }
            public CampaignPhaseTransitionRequest LastRequest { get; private set; }

            public bool TryApplyPhaseTransition(CampaignPhaseTransitionRequest request, out string reason)
            {
                RequestCount++;
                LastRequest = request;
                CurrentPhase = request.TargetPhase;
                reason = string.Empty;
                return true;
            }

            public bool TryApplyPhaseTransition(GamePhase targetPhase, out string reason)
            {
                reason = "不应使用旧阶段入口";
                return false;
            }

            public bool TryBeginEncounter(CampaignEncounterRequest request, out string reason)
            {
                reason = string.Empty;
                return false;
            }
        }

        private sealed class PreventExactRouteReactor : GameActionReactor<TransitionCampaignPhaseAction>
        {
            private readonly PlayableHuntRoutePlan expectedRoute;

            public PreventExactRouteReactor(PlayableHuntRoutePlan expectedRoute)
            {
                this.expectedRoute = expectedRoute;
            }

            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            public PlayableHuntRoutePlan ObservedRoute { get; private set; }

            protected override void React(TransitionCampaignPhaseAction action, ReactionContext context, ReactionResponse response)
            {
                ObservedRoute = action.Request.HuntContext.RoutePlan;
                if (ReferenceEquals(ObservedRoute, expectedRoute)) response.Prevent("测试阻止精确路线");
            }
        }

        private sealed class RecordingLegacyHost : ICampaignPhaseTransitionHost
        {
            public GamePhase CurrentPhase { get; private set; } = GamePhase.Settlement;
            public int RequestCount { get; private set; }

            public bool TryApplyPhaseTransition(GamePhase targetPhase, out string reason)
            {
                RequestCount++;
                CurrentPhase = targetPhase;
                reason = string.Empty;
                return true;
            }

            public bool TryBeginEncounter(CampaignEncounterRequest request, out string reason)
            {
                reason = string.Empty;
                return false;
            }
        }
    }
}
