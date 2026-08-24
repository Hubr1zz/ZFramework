using System.Collections.Generic;
using System.Reflection;
using HuntingInDarkness.Bootstrap;
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

        [TearDown]
        public void TearDown() => PlayableHuntContentRuntime.Configure(null);

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

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
