using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HuntingInDarkness.Bootstrap;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunt;
using HuntingInDarkness.Hunt;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableHuntDestinationContentTests
    {
        private const string CatalogPath = "Assets/GameScripts/GameLogic/HuntingInDarkness/Content/Hunt/Destinations/PlayableHuntDestinationCatalog.asset";
        private const string SettingsPath = "Assets/AssetRaw/Configs/HuntingInDarkness/PlayableBootstrapSettings.asset";

        [TearDown]
        public void TearDown() => PlayableHuntDestinationRuntime.Configure(null, null);

        [Test]
        public void Catalog_ProvidesThreeDistinctRoutesAcrossCampaignYears()
        {
            PlayableHuntDestinationCatalog catalog = AssetDatabase.LoadAssetAtPath<PlayableHuntDestinationCatalog>(CatalogPath);

            Assert.That(catalog, Is.Not.Null);
            List<PlayableHuntDestination> firstYearDestinations = catalog.GetAvailable(1);
            List<PlayableHuntDestination> secondYearDestinations = catalog.GetAvailable(2);
            Assert.That(firstYearDestinations, Has.Count.EqualTo(2));
            Assert.That(secondYearDestinations, Has.Count.EqualTo(3));
            Assert.That(secondYearDestinations.ConvertAll(destination => destination.DestinationId), Is.EquivalentTo(new[] { "stone-forest-outskirts", "sunken-fungal-marsh", "echoing-broken-road" }));
            Assert.That(new HashSet<PlayableHuntContentCatalog>(secondYearDestinations.ConvertAll(destination => destination.HuntContent)), Has.Count.EqualTo(3));
        }

        [Test]
        public void Catalog_ProjectsConfiguredLockedRouteWithUnlockReason()
        {
            PlayableHuntDestinationCatalog catalog = AssetDatabase.LoadAssetAtPath<PlayableHuntDestinationCatalog>(CatalogPath);

            List<PlayableHuntDestinationAvailability> firstYear = catalog.GetAvailability(1);
            PlayableHuntDestinationAvailability locked = firstYear.Find(projection => projection.Destination.DestinationId == "echoing-broken-road");

            Assert.That(firstYear, Has.Count.EqualTo(3));
            Assert.That(firstYear.Count(projection => projection.IsAvailable), Is.EqualTo(2));
            Assert.That(locked.Destination, Is.Not.Null);
            Assert.That(locked.IsAvailable, Is.False);
            Assert.That(locked.Reason, Is.EqualTo("第 2 年后才能前往。"));

            PlayableHuntDestinationAvailability unlocked = catalog.GetAvailability(2).Find(projection => projection.Destination.DestinationId == "echoing-broken-road");
            Assert.That(unlocked.IsAvailable, Is.True, unlocked.Reason);
        }

        [Test]
        public void ResolveAvailableIndex_PrefersStableIdThenFirstAvailable()
        {
            PlayableHuntDestination locked = new();
            PlayableHuntDestination preferred = new();
            PlayableHuntDestination firstAvailable = new();
            var availability = new[]
            {
                new PlayableHuntDestinationAvailability(locked, false, "locked"),
                new PlayableHuntDestinationAvailability(preferred, true, string.Empty),
                new PlayableHuntDestinationAvailability(firstAvailable, true, string.Empty)
            };
            SetPrivateField(locked, "destinationId", "locked-route");
            SetPrivateField(preferred, "destinationId", "preferred-route");
            SetPrivateField(firstAvailable, "destinationId", "first-route");

            Assert.That(PlayableHuntDestinationCatalog.ResolveAvailableIndex(availability, "preferred-route"), Is.EqualTo(1));
            Assert.That(PlayableHuntDestinationCatalog.ResolveAvailableIndex(availability, "missing-route"), Is.EqualTo(1));
            Assert.That(PlayableHuntDestinationCatalog.ResolveAvailableIndex(new[] { availability[0] }, "preferred-route"), Is.EqualTo(-1));
        }

        [Test]
        public void EchoingBrokenRoad_ProvidesHigherNoiseAndMixedLateRouteContent()
        {
            PlayableHuntDestinationCatalog catalog = AssetDatabase.LoadAssetAtPath<PlayableHuntDestinationCatalog>(CatalogPath);
            PlayableHuntDestination destination = catalog.GetAvailable(2).Find(candidate => candidate.DestinationId == "echoing-broken-road");
            HunterData template = ScriptableObject.CreateInstance<HunterData>();
            try
            {
                Assert.That(destination, Is.Not.Null);
                Assert.That(destination.MinimumYear, Is.EqualTo(2));
                Assert.That(destination.HuntContent.TilePool, Has.Count.EqualTo(3));
                Assert.That(destination.HuntContent.EventPool, Has.Count.EqualTo(4));
                EventData routeEvent = destination.HuntContent.EventPool.Single(gameEvent => gameEvent.ContentId == "hunt_broken_road_echo");
                Assert.That(routeEvent.minYear, Is.EqualTo(2));
                Assert.That(routeEvent.options[0].checkPresentation, Is.EqualTo(EventCheckPresentationKind.OldMaid));
                Assert.That(routeEvent.options[0].checkCount, Is.EqualTo(1));
                Assert.That(destination.HuntContent.NoiseProfile.GetEligibleDangerEvents(2), Does.Contain(routeEvent));
                Assert.That(destination.HuntContent.NoiseProfile.TryCreatePlan(new[] { new HunterInstance(template) }, out NoiseCheckPlan plan), Is.True);
                Assert.That(plan.DangerCardCount, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(template);
            }
        }

        [Test]
        public void BootstrapSettings_ReferencesConfiguredDestinationCatalog()
        {
            PlayableBootstrapSettings settings = AssetDatabase.LoadAssetAtPath<PlayableBootstrapSettings>(SettingsPath);

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.ShowSettlementHud, Is.False, "生产配置不得启用旧屏幕空间营地 HUD。");
            Assert.That(settings.HuntDestinations, Is.Not.Null);
            Assert.That(settings.HuntDestinations.IsConfigured, Is.True);
        }

        [Test]
        public void AvailableRoutes_ProvidePlayableNoiseProfiles()
        {
            PlayableBootstrapSettings settings = AssetDatabase.LoadAssetAtPath<PlayableBootstrapSettings>(SettingsPath);
            HunterData template = ScriptableObject.CreateInstance<HunterData>();
            try
            {
                var hunters = new[] { new HunterInstance(template) };
                Assert.That(settings.HuntContent.NoiseProfile.IsConfigured, Is.True, "fallback hunt content");
                Assert.That(settings.HuntContent.NoiseProfile.GetEligibleDangerEvents(1), Is.Not.Empty, "fallback hunt content");
                foreach (PlayableHuntDestination destination in settings.HuntDestinations.GetAvailable(1))
                {
                    Assert.That(destination.HuntContent.NoiseProfile.IsConfigured, Is.True, destination.DestinationId);
                    Assert.That(destination.HuntContent.NoiseProfile.TryCreatePlan(hunters, out NoiseCheckPlan plan), Is.True, destination.DestinationId);
                    Assert.That(plan.DangerCardCount, Is.EqualTo(1), destination.DestinationId);
                    Assert.That(destination.HuntContent.NoiseProfile.GetEligibleDangerEvents(1), Is.Not.Empty, destination.DestinationId);
                }
            }
            finally
            {
                Object.DestroyImmediate(template);
            }
        }

        [Test]
        public void ProductionTiles_ProvideNineStableMixedResourcePoints()
        {
            string[] tileGuids = AssetDatabase.FindAssets("t:HexTileData", new[] { "Assets/GameScripts/GameLogic/HuntingInDarkness/Content/Hunt/Tiles" });
            var resourcePointIds = new HashSet<string>();
            foreach (string tileGuid in tileGuids)
            {
                HexTileData tile = AssetDatabase.LoadAssetAtPath<HexTileData>(AssetDatabase.GUIDToAssetPath(tileGuid));
                foreach (ResourcePointConfig point in tile?.resourcePoints ?? new List<ResourcePointConfig>())
                {
                    Assert.That(point.resourcePointId, Is.Not.Empty, tile.ContentId);
                    Assert.That(point.materialPool, Is.Not.Empty, point.resourcePointId);
                    foreach (ResourceMaterialConfig material in point.materialPool)
                    {
                        Assert.That(material.materialId, Is.Not.Empty, point.resourcePointId);
                        Assert.That(material.copies, Is.GreaterThan(0), point.resourcePointId);
                    }
                    resourcePointIds.Add(point.resourcePointId);
                }
            }

            Assert.That(resourcePointIds, Is.SupersetOf(new[]
            {
                "nose_mushroom", "bulbous_mushroom", "hair_grass", "mimic_stone", "biological_remains",
                "small_animal_nest", "strange_mound", "grave", "broken_statue"
            }));
        }

        [Test]
        public void CanSelect_ValidatesWithoutMutatingActiveRoute()
        {
            PlayableBootstrapSettings settings = AssetDatabase.LoadAssetAtPath<PlayableBootstrapSettings>(SettingsPath);
            PlayableHuntDestination destination = settings.HuntDestinations.GetAvailable(1)[0];
            PlayableHuntDestinationRuntime.Configure(settings.HuntDestinations, settings.HuntContent);

            bool canSelect = PlayableHuntDestinationRuntime.CanSelect(destination, 1, out string reason);

            Assert.That(canSelect, Is.True, reason);
            Assert.That(PlayableHuntDestinationRuntime.ActiveDestination, Is.Null);
        }

        [Test]
        public void TrySelectForDeparture_NullRouteRequiresSelectionWhenRouteIsAvailable()
        {
            PlayableBootstrapSettings settings = AssetDatabase.LoadAssetAtPath<PlayableBootstrapSettings>(SettingsPath);
            PlayableHuntDestination destination = settings.HuntDestinations.GetAvailable(1)[0];
            PlayableHuntDestinationRuntime.Configure(settings.HuntDestinations, settings.HuntContent);
            Assert.That(PlayableHuntDestinationRuntime.TrySelect(destination, 1, out string selectReason), Is.True, selectReason);

            bool canSelectFallback = PlayableHuntDestinationRuntime.CanSelectForDeparture(null, 1, out string canSelectReason);
            bool selectedFallback = PlayableHuntDestinationRuntime.TrySelectForDeparture(null, 1, out string fallbackReason);

            Assert.That(canSelectFallback, Is.False);
            Assert.That(canSelectReason, Is.EqualTo("请选择狩猎目的地。"));
            Assert.That(selectedFallback, Is.False);
            Assert.That(fallbackReason, Is.EqualTo("请选择狩猎目的地。"));
            Assert.That(PlayableHuntDestinationRuntime.ActiveDestination, Is.SameAs(destination));
        }

        [Test]
        public void TrySelectForDeparture_NullRouteRestoresFallbackWhenCurrentYearHasNoRoutes()
        {
            PlayableBootstrapSettings settings = AssetDatabase.LoadAssetAtPath<PlayableBootstrapSettings>(SettingsPath);
            PlayableHuntDestinationCatalog emptyCatalog = ScriptableObject.CreateInstance<PlayableHuntDestinationCatalog>();
            try
            {
                PlayableHuntDestinationRuntime.Configure(emptyCatalog, settings.HuntContent);

                Assert.That(emptyCatalog.GetAvailable(1), Is.Empty);
                bool selectedFallback = PlayableHuntDestinationRuntime.TrySelectForDeparture(null, 1, out string fallbackReason);

                Assert.That(selectedFallback, Is.True, fallbackReason);
                Assert.That(PlayableHuntDestinationRuntime.ActiveDestination, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(emptyCatalog);
            }
        }

        [TestCase(2, 0, 1)]
        [TestCase(1, 1, 2)]
        public void CanSelectForDeparture_NullRouteRejectsFallbackWithoutYearEligibleDangerEvent(int minimumYear, int maximumYear, int currentYear)
        {
            PlayableHuntDestinationCatalog emptyCatalog = ScriptableObject.CreateInstance<PlayableHuntDestinationCatalog>();
            PlayableHuntContentCatalog fallback = CreateFallbackContent(minimumYear, maximumYear, out List<Object> createdObjects, out _, out _);
            try
            {
                PlayableHuntDestinationRuntime.Configure(emptyCatalog, fallback);

                bool canSelect = PlayableHuntDestinationRuntime.CanSelectForDeparture(null, currentYear, out string reason);

                Assert.That(canSelect, Is.False);
                Assert.That(reason, Does.Contain("没有可用危险事件"));
            }
            finally
            {
                Object.DestroyImmediate(emptyCatalog);
                foreach (Object createdObject in createdObjects)
                    Object.DestroyImmediate(createdObject);
            }
        }

        [Test]
        public void NoiseProfile_RequiresExplicitWeightedHuntEventIdentity()
        {
            PlayableHuntContentCatalog fallback = CreateFallbackContent(1, 0, out List<Object> createdObjects, out EventData dangerEvent, out _);
            try
            {
                dangerEvent.category = EventCategory.Settlement;
                Assert.That(fallback.IsConfigured, Is.False, "非 Hunt 事件不能进入噪音牌堆");

                dangerEvent.category = EventCategory.Hunt;
                dangerEvent.drawWeight = 0;
                Assert.That(fallback.IsConfigured, Is.False, "零权重事件不能进入噪音牌堆");

                dangerEvent.drawWeight = 1;
                dangerEvent.ConfigureContentId(string.Empty);
                Assert.That(fallback.IsConfigured, Is.False, "Unity 资产名回退不能冒充稳定事件 ID");
            }
            finally
            {
                foreach (Object createdObject in createdObjects)
                    Object.DestroyImmediate(createdObject);
            }
        }

        private static PlayableHuntContentCatalog CreateFallbackContent(int minimumYear, int maximumYear, out List<Object> createdObjects, out EventData dangerEvent, out PlayableHuntNoiseProfile profile)
        {
            PlayableHuntContentCatalog content = ScriptableObject.CreateInstance<PlayableHuntContentCatalog>();
            HexTileData startingTile = ScriptableObject.CreateInstance<HexTileData>();
            HexTileData plainTile = ScriptableObject.CreateInstance<HexTileData>();
            dangerEvent = ScriptableObject.CreateInstance<EventData>();
            dangerEvent.name = "FallbackRiskEventAsset";
            dangerEvent.ConfigureContentId("fallback_risk_event");
            dangerEvent.eventName = "默认风险事件";
            dangerEvent.category = EventCategory.Hunt;
            dangerEvent.minYear = minimumYear;
            dangerEvent.maxYear = maximumYear;
            dangerEvent.drawWeight = 1;
            profile = new PlayableHuntNoiseProfile();
            SetPrivateField(profile, "profileId", "fallback-test");
            SetPrivateField(profile, "deckSize", 10);
            SetPrivateField(profile, "baseNoisePerHunter", 1);
            SetPrivateField(profile, "maxDangerCards", 8);
            SetPrivateField(profile, "dangerEvents", new List<EventData> { dangerEvent });
            SetPrivateField(content, "startingTile", startingTile);
            SetPrivateField(content, "tilePool", new List<HexTileData> { plainTile });
            SetPrivateField(content, "noiseProfile", profile);
            createdObjects = new List<Object> { content, startingTile, plainTile, dangerEvent };
            return content;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
