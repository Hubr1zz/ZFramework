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
using UnityEngine.TestTools;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableCampaignContentAssemblerTests
    {
        private const string SettingsPath = "Assets/GameScripts/GameLogic/HuntingInDarkness/Resources/HuntingInDarkness/PlayableBootstrapSettings.asset";
        private static readonly FieldInfo installationFailureProbeField = typeof(PlayableCampaignContentAssembler).GetField("installationFailureProbe", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo resetAssemblerMethod = typeof(PlayableCampaignContentAssembler).GetMethod("ResetRuntimeState", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo resetSettlementContentRuntimeMethod = typeof(PlayableSettlementContentRuntime).GetMethod("ResetRuntimeState", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly PropertyInfo candidateSettlementPlanProperty = typeof(PlayableCampaignContentCandidate).GetProperty("SettlementPlan", BindingFlags.Instance | BindingFlags.NonPublic);

        [TearDown]
        public void TearDown()
        {
            installationFailureProbeField.SetValue(null, null);
            resetAssemblerMethod.Invoke(null, null);
            resetSettlementContentRuntimeMethod.Invoke(null, null);
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

        [Test]
        public void InstallFailureAfterSettlementPrepare_ReleasesOwnedObjectsWithoutPublishing()
        {
            PlayableBootstrapSettings settings = AssetDatabase.LoadAssetAtPath<PlayableBootstrapSettings>(SettingsPath);
            ItemData generatedItem = null;
            InventionData generatedInvention = null;
            HunterData generatedHunter = null;
            HunterData externalHunter = settings.SettlementContent.RecruitmentTemplates[0];
            PlayableSymptomRuntime.Configure(settings.Symptoms);
            Assert.That(PlayableCampaignContentAssembler.TryBuild(settings, out PlayableCampaignContentCandidate candidate, out PlayableContentDiagnosticReport buildReport), Is.True, buildReport.ToString());
            installationFailureProbeField.SetValue(null, new System.Func<string, bool>(stage =>
            {
                if (stage != "after-settlement-prepare") return false;
                object plan = candidateSettlementPlanProperty.GetValue(candidate);
                generatedItem = FindByName(GetPlanList<ItemData>(plan, "Items"), "black_salt");
                generatedInvention = FindByName(GetPlanList<InventionData>(plan, "Inventions"), "paper-and-pen");
                generatedHunter = FindByName(GetPlanList<HunterData>(plan, "RecruitmentTemplates"), "ember_keeper_yao");
                Assert.That(generatedHunter, Is.Not.Null);
                return true;
            }));

            bool installed = PlayableCampaignContentAssembler.Install(candidate, out PlayableContentDiagnosticReport report);

            Assert.That(installed, Is.False);
            Assert.That(report.HasErrors, Is.True);
            Assert.That(generatedItem == null, Is.True, "被拒绝计划必须释放表生成 ItemData。");
            Assert.That(generatedInvention == null, Is.True, "被拒绝计划必须释放表生成 InventionData。");
            Assert.That(generatedHunter == null, Is.True, "被拒绝计划必须释放表生成 HunterData。");
            Assert.That(externalHunter != null, Is.True, "序列化 HunterData 资产不得被计划回收。");
            Assert.That(PlayableSettlementItemRegistry.Items, Is.Empty);
            Assert.That(candidateSettlementPlanProperty.GetValue(candidate), Is.Null);
        }

        [Test]
        public void Install_PublishesOneSettlementPlanAndReusesItsObjectGraph()
        {
            PlayableBootstrapSettings settings = AssetDatabase.LoadAssetAtPath<PlayableBootstrapSettings>(SettingsPath);
            PlayableSymptomRuntime.Configure(settings.Symptoms);
            Assert.That(PlayableCampaignContentAssembler.TryBuild(settings, out PlayableCampaignContentCandidate candidate, out PlayableContentDiagnosticReport buildReport), Is.True, buildReport.ToString());

            bool installed = PlayableCampaignContentAssembler.Install(candidate, out PlayableContentDiagnosticReport installReport);

            Assert.That(installed, Is.True, installReport.ToString());
            object plan = candidateSettlementPlanProperty.GetValue(candidate);
            IReadOnlyList<ItemData> planItems = GetPlanList<ItemData>(plan, "Items");
            ItemData planItem = FindByName(planItems, "black_salt");
            InventionData planInvention = FindByName(GetPlanList<InventionData>(plan, "Inventions"), "paper-and-pen");
            HunterData planHunter = FindByName(GetPlanList<HunterData>(plan, "RecruitmentTemplates"), "ember_keeper_yao");
            HunterData externalHunter = settings.SettlementContent.RecruitmentTemplates[0];
            Assert.That(planItem, Is.Not.Null);
            Assert.That(planInvention, Is.Not.Null);
            Assert.That(planHunter, Is.Not.Null);
            Assert.That(FindByName(PlayableSettlementItemRegistry.Items, "black_salt"), Is.SameAs(planItem));
            var firstManager = new SettlementManager(101);
            var secondManager = new SettlementManager(202);
            Assert.That(PlayableSettlementContentRuntime.TryApplyTo(firstManager), Is.True);
            Assert.That(PlayableSettlementContentRuntime.TryApplyTo(secondManager), Is.True);
            Assert.That(FindByName(PlayableSettlementItemRegistry.Items, "black_salt"), Is.SameAs(planItem));
            Assert.That(firstManager.Data.Hunters, Is.Not.Empty);
            Assert.That(secondManager.Data.Hunters, Is.Not.Empty);
            resetSettlementContentRuntimeMethod.Invoke(null, null);
            Assert.That(planItem == null, Is.True);
            Assert.That(planInvention == null, Is.True);
            Assert.That(planHunter == null, Is.True);
            Assert.That(externalHunter != null, Is.True);
            Assert.That(PlayableSettlementItemRegistry.Items, Is.Empty);
            Assert.That(PlayableSettlementInventionRegistry.Inventions, Is.Empty);
        }

        [Test]
        public void PublishedPlan_RejectsFutureSchemasBeforeMutatingSettlement()
        {
            PlayableBootstrapSettings settings = AssetDatabase.LoadAssetAtPath<PlayableBootstrapSettings>(SettingsPath);
            PlayableSymptomRuntime.Configure(settings.Symptoms);
            Assert.That(PlayableCampaignContentAssembler.TryBuild(settings, out PlayableCampaignContentCandidate candidate, out PlayableContentDiagnosticReport buildReport), Is.True, buildReport.ToString());
            Assert.That(PlayableCampaignContentAssembler.Install(candidate, out PlayableContentDiagnosticReport installReport), Is.True, installReport.ToString());
            var manager = new SettlementManager(303);
            manager.Data.CurrentYear = 7;
            manager.Data.CampaignPacingSchemaVersion = SettlementInstance.CurrentCampaignPacingSchemaVersion + 1;

            bool applied = PlayableSettlementContentRuntime.TryApplyTo(manager);

            Assert.That(applied, Is.False);
            Assert.That(manager.Data.CurrentYear, Is.EqualTo(7));
            Assert.That(manager.Data.CampaignPacingSchemaVersion, Is.EqualTo(SettlementInstance.CurrentCampaignPacingSchemaVersion + 1));
            Assert.That(manager.Data.Hunters, Is.Empty);
            Assert.That(manager.Timeline.RandomEventPool, Is.Empty);
        }

        [Test]
        public void PublishedPlan_RejectsIndependentRegistryReconfigurationWithoutDrift()
        {
            PlayableBootstrapSettings settings = AssetDatabase.LoadAssetAtPath<PlayableBootstrapSettings>(SettingsPath);
            PlayableSymptomRuntime.Configure(settings.Symptoms);
            Assert.That(PlayableCampaignContentAssembler.TryBuild(settings, out PlayableCampaignContentCandidate candidate, out PlayableContentDiagnosticReport buildReport), Is.True, buildReport.ToString());
            Assert.That(PlayableCampaignContentAssembler.Install(candidate, out PlayableContentDiagnosticReport installReport), Is.True, installReport.ToString());
            IReadOnlyList<ItemData> items = PlayableSettlementContentRuntime.Items;
            IReadOnlyList<InventionData> inventions = PlayableSettlementContentRuntime.Inventions;
            IReadOnlyList<EventData> events = PlayableSettlementContentRuntime.Events;

            Assert.Throws<System.InvalidOperationException>(() => PlayableSettlementItemRegistry.Configure(null));
            Assert.Throws<System.InvalidOperationException>(() => PlayableSettlementInventionRegistry.Configure(null));
            Assert.Throws<System.InvalidOperationException>(() => PlayableSettlementEventRegistry.Configure(null));
            Assert.Throws<System.InvalidOperationException>(() => PlayableSettlementContentRuntime.Configure(null));

            Assert.That(PlayableSettlementContentRuntime.Items, Is.SameAs(items));
            Assert.That(PlayableSettlementContentRuntime.Inventions, Is.SameAs(inventions));
            Assert.That(PlayableSettlementContentRuntime.Events, Is.SameAs(events));
            Assert.That(PlayableSettlementItemRegistry.Items, Is.SameAs(items));
            Assert.That(PlayableSettlementInventionRegistry.Inventions, Is.SameAs(inventions));
            Assert.That(PlayableSettlementItemRegistry.TryGet(items[0].ContentId, out ItemData resolvedItem), Is.True);
            Assert.That(resolvedItem, Is.SameAs(items[0]));
            Assert.That(PlayableSettlementInventionRegistry.TryGet(inventions[0].ContentId, out InventionData resolvedInvention), Is.True);
            Assert.That(resolvedInvention, Is.SameAs(inventions[0]));
            Assert.That(PlayableSettlementEventRegistry.TryResolveCanonical(events[0].ContentId, out EventData resolvedEvent), Is.True);
            Assert.That(resolvedEvent, Is.SameAs(events[0]));
        }

        [Test]
        public void PublishedPlan_LeasesEventGenerationUntilPlanRetires()
        {
            PlayableBootstrapSettings settings = AssetDatabase.LoadAssetAtPath<PlayableBootstrapSettings>(SettingsPath);
            PlayableSymptomRuntime.Configure(settings.Symptoms);
            Assert.That(PlayableCampaignContentAssembler.TryBuild(settings, out PlayableCampaignContentCandidate candidate, out PlayableContentDiagnosticReport buildReport), Is.True, buildReport.ToString());
            Assert.That(PlayableCampaignContentAssembler.Install(candidate, out PlayableContentDiagnosticReport installReport), Is.True, installReport.ToString());
            IReadOnlyList<EventData> leasedEvents = PlayableEventTableRuntime.GetEvents();
            EventData leasedEvent = leasedEvents[0];

            LogAssert.Expect(LogType.Error, "[PlayableEventTable] 活动营地内容计划仍在使用当前事件世代，拒绝重建缓存。");
            Assert.That(PlayableEventTableRuntime.Rebuild(), Is.SameAs(leasedEvents));
            LogAssert.Expect(LogType.Error, "[PlayableEventTable] 活动营地内容计划仍在使用当前事件世代，拒绝清理缓存。");
            PlayableEventTableRuntime.ClearCache();

            Assert.That(PlayableEventTableRuntime.GetEvents(), Is.SameAs(leasedEvents));
            Assert.That(leasedEvent != null, Is.True);
            resetSettlementContentRuntimeMethod.Invoke(null, null);
            PlayableEventTableRuntime.ClearCache();
            Assert.That(leasedEvent == null, Is.True);
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

        private static IReadOnlyList<T> GetPlanList<T>(object plan, string propertyName)
        {
            Assert.That(plan, Is.Not.Null);
            PropertyInfo property = plan.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            return (IReadOnlyList<T>)property.GetValue(plan);
        }

        private static T FindByName<T>(IReadOnlyList<T> assets, string name) where T : Object
        {
            foreach (T asset in assets)
                if (asset != null && asset.name == name)
                    return asset;
            return null;
        }
    }
}
