using System;
using System.Collections.Generic;
using System.Reflection;
using Core;
using HuntingInDarkness.ContentTables;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace HuntingInDarkness.Tests
{
    public sealed class PlayableCampaignPacingTests
    {
        private const string SymptomCatalogPath = "Assets/GameScripts/GameLogic/HuntingInDarkness/Content/Settlement/Symptoms/PlayableSymptomCatalog.asset";
        private static readonly MethodInfo resetSettlementContentRuntimeMethod = typeof(PlayableSettlementContentRuntime).GetMethod("ResetRuntimeState", BindingFlags.Static | BindingFlags.NonPublic);

        [SetUp]
        public void SetUp() => PlayableSymptomRuntime.Configure(AssetDatabase.LoadAssetAtPath<PlayableSymptomCatalog>(SymptomCatalogPath));

        [TearDown]
        public void TearDown()
        {
            resetSettlementContentRuntimeMethod.Invoke(null, null);
            PlayableEventTableRuntime.ClearCache();
            PlayableSymptomRuntime.Configure(null);
        }

        [Test]
        public void AdvanceCalendar_AdvancesSingleSeasonCalendarForEveryAcceptedReturn()
        {
            var settlement = new SettlementInstance { CurrentYear = 1, HuntsPerYear = 2 };
            var timeline = CreateTimeline(settlement);

            timeline.AdvanceCalendar(new HuntRecord { RecordId = "first", Year = 1 }, out _, out _);

            Assert.That(settlement.CurrentYear, Is.EqualTo(2));
            Assert.That(settlement.HuntsCompletedThisYear, Is.Zero);
            Assert.That(settlement.HuntHistory, Has.Count.EqualTo(1));

            timeline.AdvanceCalendar(new HuntRecord { RecordId = "second", Year = 2 }, out _, out _);

            Assert.That(settlement.CurrentYear, Is.EqualTo(3));
            Assert.That(settlement.HuntsCompletedThisYear, Is.Zero);
            Assert.That(settlement.HuntHistory, Has.Count.EqualTo(2));
        }

        [Test]
        public void BoundTimeline_UsesConfiguredThreeSeasonCalendar()
        {
            var settlement = new SettlementInstance { CurrentYear = 3 };
            var timeline = CreateTimeline(settlement);
            EventData returnEvent = CreateRandomEvent("return-event");
            timeline.RandomEventPool = new List<EventData> { returnEvent };
            var calendar = new CampaignCalendarDefinition("three_season_test", new[]
            {
                new SeasonDefinition("season_a", "A", 0),
                new SeasonDefinition("season_b", "B", 1),
                new SeasonDefinition("season_c", "C", 2)
            });
            Assert.That(timeline.TryBindCalendar(calendar, out string bindReason), Is.True, bindReason);

            try
            {
                IReadOnlyList<EventData> firstEvents = timeline.AdvanceCalendar(new HuntRecord { RecordId = "season-a", Year = 3 }, out CampaignCalendarAdvancePlan first, out string reason);
                Assert.That(reason, Is.Empty);
                Assert.That(firstEvents, Is.EqualTo(new[] { returnEvent }));
                Assert.That(first.YearAdvanced, Is.False);
                Assert.That(settlement.CurrentYear, Is.EqualTo(3));
                Assert.That(settlement.CurrentSeasonIndex, Is.EqualTo(1));

                IReadOnlyList<EventData> secondEvents = timeline.AdvanceCalendar(new HuntRecord { RecordId = "season-b", Year = 3 }, out CampaignCalendarAdvancePlan second, out reason);
                Assert.That(reason, Is.Empty);
                Assert.That(secondEvents, Is.EqualTo(new[] { returnEvent }));
                Assert.That(second.YearAdvanced, Is.False);
                IReadOnlyList<EventData> thirdEvents = timeline.AdvanceCalendar(new HuntRecord { RecordId = "season-c", Year = 3 }, out CampaignCalendarAdvancePlan third, out reason);
                Assert.That(reason, Is.Empty);
                Assert.That(thirdEvents, Is.EqualTo(new[] { returnEvent }));
                Assert.That(third.YearAdvanced, Is.True);
                Assert.That(settlement.CurrentYear, Is.EqualTo(4));
                Assert.That(settlement.CurrentSeasonIndex, Is.Zero);
                Assert.That(settlement.Timeline.FindAll(entry => entry.EntryType == TimelineEntryType.Random), Has.Count.EqualTo(3));
                Assert.That(settlement.Timeline.ConvertAll(entry => entry.SourceHuntRecordId), Is.EquivalentTo(new[] { "season-a", "season-b", "season-c" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(returnEvent);
            }
        }

        [Test]
        public void AdvanceCalendar_DoesNotPublishFactsOutsideActionQueue()
        {
            var settlement = new SettlementInstance { CurrentYear = 1, HuntsPerYear = 2 };
            var timeline = CreateTimeline(settlement);
            int receivedCount = 0;
            Action<HuntCompletedEvent> handler = _ => receivedCount++;
            EventBus.Subscribe(handler);
            try
            {
                timeline.AdvanceCalendar(new HuntRecord { RecordId = "published-first", Year = 1, HuntersDeployed = 2, HuntersLost = 1, CollectedResources = { "碎石", "碎石" } }, out _, out _);

                Assert.That(receivedCount, Is.Zero);
                Assert.That(settlement.CurrentYear, Is.EqualTo(2));
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public void AdvanceCalendar_IgnoresLegacyPacingFields()
        {
            var settlement = new SettlementInstance { CurrentYear = 3, HuntsPerYear = 0 };
            var timeline = CreateTimeline(settlement);

            timeline.AdvanceCalendar(new HuntRecord { RecordId = "legacy-fields", Year = 3 }, out _, out _);

            Assert.That(settlement.CurrentYear, Is.EqualTo(4));
            Assert.That(settlement.HuntsCompletedThisYear, Is.Zero);
        }

        [Test]
        public void AdvanceCalendar_RejectsDuplicateRecordIdWithoutRepeatingYear()
        {
            var settlement = new SettlementInstance { CurrentYear = 4 };
            var timeline = CreateTimeline(settlement);
            var record = new HuntRecord { RecordId = "hunt-01", Year = 4 };

            timeline.AdvanceCalendar(record, out _, out _);
            timeline.AdvanceCalendar(new HuntRecord { RecordId = "hunt-01", Year = 4 }, out _, out _);

            Assert.That(settlement.CurrentYear, Is.EqualTo(5));
            Assert.That(settlement.HuntHistory, Has.Count.EqualTo(1));
        }

        [Test]
        public void AdvanceCalendar_ReusesExistingReturnRandomByStableRecordId()
        {
            var settlement = new SettlementInstance { CurrentYear = 4 };
            settlement.Timeline.Add(new AnnalEntry { Year = 5, EventId = "replacement", EntryType = TimelineEntryType.Random, SourceHuntRecordId = "recovered-return" });
            EventData returnEvent = ScriptableObject.CreateInstance<EventData>();
            returnEvent.name = "replacement";
            returnEvent.category = EventCategory.Random;
            returnEvent.minYear = 1;
            returnEvent.maxYear = 99;
            returnEvent.drawWeight = 1;
            var timeline = CreateTimeline(settlement);
            timeline.RandomEventPool = new List<EventData> { returnEvent };

            try
            {
                timeline.AdvanceCalendar(new HuntRecord { RecordId = "recovered-return", Year = 4 }, out _, out _);
                var projection = new SettlementEventRestoreProjection(settlement, timeline.ResolveEvent);
                SettlementEventRestorePlan restorePlan = projection.Prepare();

                Assert.That(settlement.CurrentYear, Is.EqualTo(5));
                Assert.That(settlement.Timeline.FindAll(entry => entry != null && entry.EntryType == TimelineEntryType.Random && entry.SourceHuntRecordId == "recovered-return"), Has.Count.EqualTo(1));
                Assert.That(restorePlan.Succeeded, Is.True);
                Assert.That(restorePlan.Events, Has.Count.EqualTo(1));
                Assert.That(restorePlan.Events[0], Is.SameAs(returnEvent));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(returnEvent);
            }
        }

        [Test]
        public void AdvanceCalendar_RejectsDifferentStableIdFromPastYear()
        {
            var settlement = new SettlementInstance { CurrentYear = 7 };
            var timeline = CreateTimeline(settlement);
            timeline.AdvanceCalendar(new HuntRecord { RecordId = "ordered", Year = 7 }, out _, out _);

            timeline.AdvanceCalendar(new HuntRecord { RecordId = "different-but-stale", Year = 7 }, out _, out _);

            Assert.That(settlement.CurrentYear, Is.EqualTo(8));
            Assert.That(settlement.HuntHistory, Has.Count.EqualTo(1));
        }

        [Test]
        public void ContentCatalog_NormalizesLegacyPacingFields()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PlayableSettlementContentCatalog>("Assets/GameScripts/GameLogic/HuntingInDarkness/Content/Settlement/PlayableSettlementContentCatalog.asset");
            var manager = new SettlementManager(1);
            manager.Data.HuntsPerYear = 0;
            manager.Data.HuntsCompletedThisYear = 99;

            bool applied = catalog.ApplyTo(manager);

            Assert.That(applied, Is.True);
            Assert.That(manager.Data.CurrentYear, Is.EqualTo(1));
            Assert.That(manager.Data.HuntsPerYear, Is.EqualTo(1));
            Assert.That(manager.Data.HuntsCompletedThisYear, Is.Zero);
            Assert.That(manager.Data.CampaignPacingMigrationDiagnostic, Is.Not.Empty);
        }

        [Test]
        public void ContentCatalog_MigratesLegacySeasonProgressWithoutGeneratingEvents()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PlayableSettlementContentCatalog>("Assets/GameScripts/GameLogic/HuntingInDarkness/Content/Settlement/PlayableSettlementContentCatalog.asset");
            var manager = new SettlementManager(1);
            manager.Data.CurrentYear = 6;
            manager.Data.HuntsPerYear = 2;
            manager.Data.HuntsCompletedThisYear = 1;

            Assert.That(catalog.ApplyTo(manager), Is.True);
            Assert.That(manager.Data.CurrentYear, Is.EqualTo(6));
            Assert.That(manager.Data.CurrentSeasonIndex, Is.EqualTo(1));
            Assert.That(manager.Data.CampaignPacingSchemaVersion, Is.EqualTo(SettlementInstance.CurrentCampaignPacingSchemaVersion));
            Assert.That(manager.Data.Timeline, Is.Empty);
            Assert.That(manager.Data.HuntsCompletedThisYear, Is.Zero);
            Assert.That(manager.Data.HuntsPerYear, Is.EqualTo(1));

            Assert.That(catalog.ApplyTo(manager), Is.True);
            Assert.That(manager.Data.CurrentYear, Is.EqualTo(6));
            Assert.That(manager.Data.CurrentSeasonIndex, Is.EqualTo(1));
        }

        [Test]
        public void ContentCatalog_InstallsStandardCalendarAndSchemaTwoState()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PlayableSettlementContentCatalog>("Assets/GameScripts/GameLogic/HuntingInDarkness/Content/Settlement/PlayableSettlementContentCatalog.asset");
            var manager = new SettlementManager(1);

            Assert.That(catalog.ApplyTo(manager), Is.True);
            Assert.That(manager.Calendar.CalendarId, Is.EqualTo("standard_two_season_v1"));
            Assert.That(manager.Calendar.Seasons, Has.Count.EqualTo(2));
            Assert.That(manager.Data.CampaignCalendarId, Is.EqualTo("standard_two_season_v1"));
            Assert.That(manager.Data.CurrentSeasonIndex, Is.Zero);
            Assert.That(manager.Data.CampaignPacingSchemaVersion, Is.EqualTo(SettlementInstance.CurrentCampaignPacingSchemaVersion));
        }

        [Test]
        public void ContentCatalog_SchemaOnePreservesYearButResetsSeason()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PlayableSettlementContentCatalog>("Assets/GameScripts/GameLogic/HuntingInDarkness/Content/Settlement/PlayableSettlementContentCatalog.asset");
            var manager = new SettlementManager(1);
            manager.Data.CurrentYear = 9;
            manager.Data.CampaignPacingSchemaVersion = 1;
            manager.Data.CurrentSeasonIndex = 1;

            Assert.That(catalog.ApplyTo(manager), Is.True);
            Assert.That(manager.Data.CurrentYear, Is.EqualTo(9));
            Assert.That(manager.Data.CurrentSeasonIndex, Is.Zero);
            Assert.That(manager.Data.CampaignCalendarId, Is.EqualTo("standard_two_season_v1"));
        }

        [Test]
        public void ContentCatalog_RejectsInvalidYearBeforePacingMutation()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PlayableSettlementContentCatalog>("Assets/GameScripts/GameLogic/HuntingInDarkness/Content/Settlement/PlayableSettlementContentCatalog.asset");
            var manager = new SettlementManager(1);
            manager.Data.CurrentYear = 0;
            manager.Data.CampaignPacingSchemaVersion = 0;

            LogAssert.Expect(LogType.Error, "[SettlementManager] 营地当前年份无效。");
            Assert.That(catalog.ApplyTo(manager), Is.False);
            Assert.That(manager.Data.CurrentYear, Is.Zero);
            Assert.That(manager.Data.CampaignCalendarId, Is.Null.Or.Empty);
            Assert.That(manager.Data.CampaignPacingSchemaVersion, Is.Zero);
        }

        [Test]
        public void PendingHuntReturn_RoundTripsWithoutBecomingHistory()
        {
            var data = new SettlementInstance
            {
                CurrentYear = 3,
                PendingHuntReturn = new HuntRecord { RecordId = "pending-save", Year = 3 }
            };

            SettlementInstance restored = JsonUtility.FromJson<SettlementInstance>(JsonUtility.ToJson(data));

            Assert.That(restored.PendingHuntReturn.RecordId, Is.EqualTo("pending-save"));
            Assert.That(restored.HuntHistory, Is.Empty);
        }

        [Test]
        public void CalendarState_RoundTripsWithoutChangingActiveHuntSchema()
        {
            var data = new SettlementInstance
            {
                CurrentYear = 8,
                CampaignCalendarId = "standard_two_season_v1",
                CurrentSeasonIndex = 1,
                CampaignPacingSchemaVersion = SettlementInstance.CurrentCampaignPacingSchemaVersion
            };

            SettlementInstance restored = JsonUtility.FromJson<SettlementInstance>(JsonUtility.ToJson(data));

            Assert.That(restored.CurrentYear, Is.EqualTo(8));
            Assert.That(restored.CampaignCalendarId, Is.EqualTo("standard_two_season_v1"));
            Assert.That(restored.CurrentSeasonIndex, Is.EqualTo(1));
            Assert.That(restored.CampaignPacingSchemaVersion, Is.EqualTo(SettlementInstance.CurrentCampaignPacingSchemaVersion));
        }

        [Test]
        public void ReturnEventSource_RoundTripsAsStableRecordIdentity()
        {
            var data = new SettlementInstance();
            data.Timeline.Add(new AnnalEntry { Year = 2, EventId = "return-event", EntryType = TimelineEntryType.Random, SourceHuntRecordId = "hunt-record-2" });

            SettlementInstance restored = JsonUtility.FromJson<SettlementInstance>(JsonUtility.ToJson(data));

            Assert.That(restored.Timeline, Has.Count.EqualTo(1));
            Assert.That(restored.Timeline[0].SourceHuntRecordId, Is.EqualTo("hunt-record-2"));
        }

        private static TimelineSystem CreateTimeline(SettlementInstance settlement)
        {
            var timeline = new TimelineSystem(settlement, new FirstRandom());
            Assert.That(timeline.TryBindCalendar(new CampaignCalendarDefinition("single_season_test", new[]
            {
                new SeasonDefinition("season_default", "默认季", 0)
            }), out string reason), Is.True, reason);
            return timeline;
        }

        private static EventData CreateRandomEvent(string contentId)
        {
            EventData gameEvent = ScriptableObject.CreateInstance<EventData>();
            gameEvent.name = contentId;
            gameEvent.eventName = contentId;
            gameEvent.category = EventCategory.Random;
            gameEvent.minYear = 1;
            gameEvent.maxYear = 99;
            gameEvent.drawWeight = 1;
            gameEvent.ConfigureContentId(contentId);
            return gameEvent;
        }

        private sealed class FirstRandom : HuntingInDarkness.GameCore.Foundation.IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
        }
    }
}
