using System.Reflection;
using Core;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Bootstrap;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.ViewLayer.Settlement;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableSettlementWorldSpacePortInstallerTests
    {
        private GameObject host;
        private GameObject managerHost;
        private PlayableBootstrapSettings settings;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("WorldSpacePortInstallerTest");
            managerHost = new GameObject("WorldSpacePortInstallerManagerTest");
            managerHost.SetActive(false);
            settings = ScriptableObject.CreateInstance<PlayableBootstrapSettings>();
            SetPrivateField(settings, "showSettlementHud", false);
            SetPrivateField(settings, "huntDestinations", ScriptableObject.CreateInstance<PlayableHuntDestinationCatalog>());
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null)
                Object.DestroyImmediate(host);
            if (managerHost != null)
                Object.DestroyImmediate(managerHost);
            if (settings != null)
            {
                if (settings.HuntDestinations != null)
                    Object.DestroyImmediate(settings.HuntDestinations);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void EnsureInstalled_HudDisabled_RegistersOneOfEachWorldSpacePort()
        {
            GameManager manager = managerHost.AddComponent<GameManager>();

            Assert.That(settings.ShowSettlementHud, Is.False);
            Assert.DoesNotThrow(() => PlayableGameBootstrap.EnsureRequiredWorldSpacePorts(host, manager, settings));
            Assert.DoesNotThrow(() => PlayableGameBootstrap.EnsureRequiredWorldSpacePorts(host, manager, settings));

            PlayableHuntDestinationView[] destinationViews = host.GetComponents<PlayableHuntDestinationView>();
            PlayableSettlementEventView[] eventViews = host.GetComponents<PlayableSettlementEventView>();
            Assert.That(destinationViews, Has.Length.EqualTo(1));
            Assert.That(eventViews, Has.Length.EqualTo(1));
            Assert.That(GetPrivateField<IPlayableHuntDepartureInput>(manager, "playableHuntDepartureInput"), Is.SameAs(destinationViews[0]));
            Assert.That(GetPrivateField<IPlayableEventInput>(manager, "playableEventInput"), Is.SameAs(eventViews[0]));
            Assert.That(GetPrivateField<PlayableHuntDestinationCatalog>(destinationViews[0], "catalog"), Is.SameAs(settings.HuntDestinations));
        }

        private static T GetPrivateField<T>(object target, string fieldName) => (T)target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        private static void SetPrivateField(object target, string fieldName, object value) => target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
