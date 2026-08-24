using System.Collections.Generic;
using HuntingInDarkness.Bootstrap;
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
    }
}
