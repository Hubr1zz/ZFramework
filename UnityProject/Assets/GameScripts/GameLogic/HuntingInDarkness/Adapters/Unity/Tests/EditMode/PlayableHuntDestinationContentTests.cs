using System.Collections.Generic;
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
        private const string SettingsPath = "Assets/GameScripts/GameLogic/HuntingInDarkness/Resources/HuntingInDarkness/PlayableBootstrapSettings.asset";

        [TearDown]
        public void TearDown() => PlayableHuntDestinationRuntime.Configure(null, null);

        [Test]
        public void Catalog_ProvidesTwoDistinctAvailableRoutes()
        {
            PlayableHuntDestinationCatalog catalog = AssetDatabase.LoadAssetAtPath<PlayableHuntDestinationCatalog>(CatalogPath);

            Assert.That(catalog, Is.Not.Null);
            List<PlayableHuntDestination> destinations = catalog.GetAvailable(1);
            Assert.That(destinations, Has.Count.GreaterThanOrEqualTo(2));
            Assert.That(destinations[0].DestinationId, Is.Not.EqualTo(destinations[1].DestinationId));
            Assert.That(destinations[0].HuntContent, Is.Not.SameAs(destinations[1].HuntContent));
        }

        [Test]
        public void BootstrapSettings_ReferencesConfiguredDestinationCatalog()
        {
            PlayableBootstrapSettings settings = AssetDatabase.LoadAssetAtPath<PlayableBootstrapSettings>(SettingsPath);

            Assert.That(settings, Is.Not.Null);
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
