using System.Collections.Generic;
using System.Reflection;
using HuntingInDarkness.Bootstrap;
using HuntingInDarkness.ContentTables;
using HuntingInDarkness.Data;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableCampaignContentAssemblerTests
    {
        private const string SettingsPath = "Assets/GameScripts/GameLogic/HuntingInDarkness/Resources/HuntingInDarkness/PlayableBootstrapSettings.asset";
        private static readonly FieldInfo installationFailureProbeField = typeof(PlayableCampaignContentAssembler).GetField("installationFailureProbe", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo resetAssemblerMethod = typeof(PlayableCampaignContentAssembler).GetMethod("ResetRuntimeState", BindingFlags.Static | BindingFlags.NonPublic);

        [TearDown]
        public void TearDown()
        {
            installationFailureProbeField.SetValue(null, null);
            resetAssemblerMethod.Invoke(null, null);
            PlayableEventTableRuntime.ClearCache();
            PlayableHuntContentRuntime.Configure(null);
            PlayableSymptomRuntime.Configure(null);
            PlayableSettlementItemRegistry.Configure(null);
            PlayableSettlementInventionRegistry.Configure(null);
            PlayableSettlementEventRegistry.Configure(null);
        }

        [Test]
        public void TryBuild_InvalidSettings_DoesNotMutateRuntime()
        {
            var sentinel = ScriptableObject.CreateInstance<PlayableHuntContentCatalog>();
            try
            {
                PlayableHuntContentRuntime.Configure(sentinel);

                bool built = PlayableCampaignContentAssembler.TryBuild(null, out _, out PlayableContentDiagnosticReport report);

                Assert.That(built, Is.False);
                Assert.That(report.HasErrors, Is.True);
                Assert.That(PlayableHuntContentRuntime.Catalog, Is.SameAs(sentinel));
            }
            finally
            {
                Object.DestroyImmediate(sentinel);
            }
        }

        [Test]
        public void TryBuild_ValidSettings_CreatesDeterministicCandidate()
        {
            PlayableBootstrapSettings settings = AssetDatabase.LoadAssetAtPath<PlayableBootstrapSettings>(SettingsPath);

            bool firstBuilt = PlayableCampaignContentAssembler.TryBuild(settings, out PlayableCampaignContentCandidate first, out PlayableContentDiagnosticReport firstReport);
            bool secondBuilt = PlayableCampaignContentAssembler.TryBuild(settings, out PlayableCampaignContentCandidate second, out PlayableContentDiagnosticReport secondReport);

            Assert.That(firstBuilt, Is.True, firstReport.ToString());
            Assert.That(secondBuilt, Is.True, secondReport.ToString());
            Assert.That(first.SettlementContent, Is.SameAs(second.SettlementContent));
            Assert.That(first.DefaultHuntContent, Is.SameAs(second.DefaultHuntContent));
            Assert.That(first.Destinations, Has.Count.EqualTo(second.Destinations.Count));
            for (int index = 0; index < first.Destinations.Count; index++)
                Assert.That(first.Destinations[index].DestinationId, Is.EqualTo(second.Destinations[index].DestinationId));
        }

        [Test]
        public void TryBuild_SettingsChangeAfterBuild_DoesNotChangeCandidate()
        {
            PlayableBootstrapSettings source = AssetDatabase.LoadAssetAtPath<PlayableBootstrapSettings>(SettingsPath);
            PlayableBootstrapSettings settings = Object.Instantiate(source);
            try
            {
                Assert.That(PlayableCampaignContentAssembler.TryBuild(settings, out PlayableCampaignContentCandidate candidate, out PlayableContentDiagnosticReport report), Is.True, report.ToString());
                PlayableSettlementContentCatalog settlementContent = candidate.SettlementContent;
                PlayableHuntContentCatalog huntContent = candidate.DefaultHuntContent;

                SetPrivateField(settings, "settlementContent", null);
                SetPrivateField(settings, "huntContent", null);

                Assert.That(candidate.SettlementContent, Is.SameAs(settlementContent));
                Assert.That(candidate.DefaultHuntContent, Is.SameAs(huntContent));
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void TryBuild_DuplicateDestinationIds_AreReported()
        {
            var settings = ScriptableObject.CreateInstance<PlayableBootstrapSettings>();
            var destinationCatalog = ScriptableObject.CreateInstance<PlayableHuntDestinationCatalog>();
            var first = new PlayableHuntDestination();
            var second = new PlayableHuntDestination();
            try
            {
                SetPrivateField(first, "destinationId", "duplicate");
                SetPrivateField(second, "destinationId", " duplicate ");
                SetPrivateField(destinationCatalog, "destinations", new List<PlayableHuntDestination> { first, second });
                SetPrivateField(settings, "huntDestinations", destinationCatalog);

                bool built = PlayableCampaignContentAssembler.TryBuild(settings, out _, out PlayableContentDiagnosticReport report);

                Assert.That(built, Is.False);
                Assert.That(report.Diagnostics, Has.Some.Matches<PlayableContentDiagnostic>(diagnostic => diagnostic.Code == "hunt.destination.id.duplicate"));
            }
            finally
            {
                Object.DestroyImmediate(destinationCatalog);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void NoiseCoverage_InfiniteEvent_CoversUnboundedCampaign()
        {
            var profile = new PlayableHuntNoiseProfile();
            EventData gameEvent = CreateDangerEvent("danger:always", 1, 0);
            try
            {
                SetPrivateField(profile, "dangerEvents", new List<EventData> { gameEvent });

                Assert.That(profile.TryValidateContinuousCoverage(1, out int firstMissingYear), Is.True);
                Assert.That(firstMissingYear, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(gameEvent);
            }
        }

        [Test]
        public void NoiseCoverage_GapBeforeInfiniteEvent_ReportsFirstMissingYear()
        {
            var profile = new PlayableHuntNoiseProfile();
            EventData earlyEvent = CreateDangerEvent("danger:early", 1, 2);
            EventData lateEvent = CreateDangerEvent("danger:late", 4, 0);
            try
            {
                SetPrivateField(profile, "dangerEvents", new List<EventData> { lateEvent, earlyEvent });

                Assert.That(profile.TryValidateContinuousCoverage(1, out int firstMissingYear), Is.False);
                Assert.That(firstMissingYear, Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(earlyEvent);
                Object.DestroyImmediate(lateEvent);
            }
        }

        [Test]
        public void NoiseCoverage_AdjacentIntervals_CoverFromDestinationOpeningYear()
        {
            var profile = new PlayableHuntNoiseProfile();
            EventData earlyEvent = CreateDangerEvent("danger:opening", 3, 5);
            EventData lateEvent = CreateDangerEvent("danger:late", 6, 0);
            try
            {
                SetPrivateField(profile, "dangerEvents", new List<EventData> { lateEvent, earlyEvent });

                Assert.That(profile.TryValidateContinuousCoverage(3, out int firstMissingYear), Is.True);
                Assert.That(firstMissingYear, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(earlyEvent);
                Object.DestroyImmediate(lateEvent);
            }
        }

        [Test]
        public void InstallFailureAfterProjection_RestoresRuntimeAndPublishedEventGeneration()
        {
            PlayableBootstrapSettings settings = AssetDatabase.LoadAssetAtPath<PlayableBootstrapSettings>(SettingsPath);
            var sentinelHuntContent = ScriptableObject.CreateInstance<PlayableHuntContentCatalog>();
            EventData stagedEvent = null;
            try
            {
                PlayableSymptomRuntime.Configure(settings.Symptoms);
                IReadOnlyList<EventData> previousEvents = PlayableEventTableRuntime.Rebuild();
                EventData previousEvent = previousEvents[0];
                PlayableHuntContentRuntime.Configure(sentinelHuntContent);
                PlayableSettlementItemRegistry.Configure(null);
                PlayableSettlementInventionRegistry.Configure(null);
                PlayableSettlementEventRegistry.Configure(null);
                Assert.That(PlayableCampaignContentAssembler.TryBuild(settings, out PlayableCampaignContentCandidate candidate, out PlayableContentDiagnosticReport buildReport), Is.True, buildReport.ToString());
                installationFailureProbeField.SetValue(null, new System.Func<string, bool>(stage =>
                {
                    if (stage != "after-settlement-projection") return false;
                    stagedEvent = PlayableEventTableRuntime.GetEvents()[0];
                    return true;
                }));

                bool installed = PlayableCampaignContentAssembler.Install(candidate, out PlayableContentDiagnosticReport installReport);

                Assert.That(installed, Is.False);
                Assert.That(installReport.HasErrors, Is.True);
                Assert.That(PlayableHuntContentRuntime.Catalog, Is.SameAs(sentinelHuntContent));
                Assert.That(PlayableEventTableRuntime.GetEvents()[0], Is.SameAs(previousEvent));
                Assert.That(previousEvent != null, Is.True);
                Assert.That(stagedEvent == null, Is.True);
                Assert.That(PlayableSettlementItemRegistry.Items, Is.Empty);
                Assert.That(PlayableSettlementInventionRegistry.Inventions, Is.Empty);

                installationFailureProbeField.SetValue(null, new System.Func<string, bool>(stage => stage == "after-event-prepare"));
                Assert.That(PlayableCampaignContentAssembler.Install(candidate, out PlayableContentDiagnosticReport retryReport), Is.False);
                Assert.That(retryReport.Diagnostics, Has.None.Matches<PlayableContentDiagnostic>(diagnostic => diagnostic.Code == "candidate.install.gate"));
                Assert.That(PlayableEventTableRuntime.GetEvents()[0], Is.SameAs(previousEvent));
            }
            finally
            {
                Object.DestroyImmediate(sentinelHuntContent);
            }
        }

        private static EventData CreateDangerEvent(string contentId, int minYear, int maxYear)
        {
            EventData gameEvent = ScriptableObject.CreateInstance<EventData>();
            gameEvent.ConfigureContentId(contentId);
            gameEvent.category = EventCategory.Hunt;
            gameEvent.drawWeight = 1;
            gameEvent.minYear = minYear;
            gameEvent.maxYear = maxYear;
            return gameEvent;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
