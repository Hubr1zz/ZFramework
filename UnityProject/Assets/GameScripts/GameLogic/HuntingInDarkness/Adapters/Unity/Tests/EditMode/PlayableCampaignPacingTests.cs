using System;
using System.Collections.Generic;
using System.Reflection;
using Core;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HuntingInDarkness.Tests
{
    public sealed class PlayableCampaignPacingTests
    {
        private static readonly MethodInfo resetSettlementContentRuntimeMethod = typeof(PlayableSettlementContentRuntime).GetMethod("ResetRuntimeState", BindingFlags.Static | BindingFlags.NonPublic);

        [TearDown]
        public void TearDown() => resetSettlementContentRuntimeMethod.Invoke(null, null);

        [Test]
        public void AdvanceYear_AdvancesExactlyOnceForEveryAcceptedReturn()
        {
            var settlement = new SettlementInstance { CurrentYear = 1, HuntsPerYear = 2 };
            var timeline = new TimelineSystem(settlement, new FirstRandom());

            timeline.AdvanceYear(new HuntRecord { RecordId = "first", Year = 1 });

            Assert.That(settlement.CurrentYear, Is.EqualTo(2));
            Assert.That(settlement.HuntsCompletedThisYear, Is.Zero);
            Assert.That(settlement.HuntHistory, Has.Count.EqualTo(1));

            timeline.AdvanceYear(new HuntRecord { RecordId = "second", Year = 2 });

            Assert.That(settlement.CurrentYear, Is.EqualTo(3));
            Assert.That(settlement.HuntsCompletedThisYear, Is.Zero);
            Assert.That(settlement.HuntHistory, Has.Count.EqualTo(2));
        }

        [Test]
        public void AdvanceYear_DoesNotPublishFactsOutsideActionQueue()
        {
            var settlement = new SettlementInstance { CurrentYear = 1, HuntsPerYear = 2 };
            var timeline = new TimelineSystem(settlement, new FirstRandom());
            int receivedCount = 0;
            Action<HuntCompletedEvent> handler = _ => receivedCount++;
            EventBus.Subscribe(handler);
            try
            {
                timeline.AdvanceYear(new HuntRecord { RecordId = "published-first", Year = 1, HuntersDeployed = 2, HuntersLost = 1, CollectedResources = { "碎石", "碎石" } });

                Assert.That(receivedCount, Is.Zero);
                Assert.That(settlement.CurrentYear, Is.EqualTo(2));
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public void AdvanceYear_IgnoresLegacyPacingFields()
        {
            var settlement = new SettlementInstance { CurrentYear = 3, HuntsPerYear = 0 };
            var timeline = new TimelineSystem(settlement, new FirstRandom());

            timeline.AdvanceYear(new HuntRecord { RecordId = "legacy-fields", Year = 3 });

            Assert.That(settlement.CurrentYear, Is.EqualTo(4));
            Assert.That(settlement.HuntsCompletedThisYear, Is.Zero);
        }

        [Test]
        public void AdvanceYear_RejectsDuplicateRecordIdWithoutRepeatingYear()
        {
            var settlement = new SettlementInstance { CurrentYear = 4 };
            var timeline = new TimelineSystem(settlement, new FirstRandom());
            var record = new HuntRecord { RecordId = "hunt-01", Year = 4 };

            timeline.AdvanceYear(record);
            timeline.AdvanceYear(new HuntRecord { RecordId = "hunt-01", Year = 4 });

            Assert.That(settlement.CurrentYear, Is.EqualTo(5));
            Assert.That(settlement.HuntHistory, Has.Count.EqualTo(1));
        }

        [Test]
        public void AdvanceYear_ReusesExistingAnnualRandomSlotAfterInterruptedAttempt()
        {
            var settlement = new SettlementInstance { CurrentYear = 4 };
            settlement.Timeline.Add(new AnnalEntry { Year = 5, EventId = "replacement", EntryType = TimelineEntryType.Random });
            EventData annualEvent = ScriptableObject.CreateInstance<EventData>();
            annualEvent.name = "replacement";
            annualEvent.category = EventCategory.Random;
            annualEvent.minYear = 1;
            annualEvent.maxYear = 99;
            annualEvent.drawWeight = 1;
            var timeline = new TimelineSystem(settlement, new FirstRandom()) { RandomEventPool = new List<EventData> { annualEvent } };

            try
            {
                timeline.AdvanceYear(new HuntRecord { RecordId = "recovered-return", Year = 4 });
                var projection = new SettlementEventRestoreProjection(settlement, timeline.ResolveEvent);
                SettlementEventRestorePlan restorePlan = projection.Prepare();

                Assert.That(settlement.CurrentYear, Is.EqualTo(5));
                Assert.That(settlement.Timeline.FindAll(entry => entry != null && entry.Year == 5 && entry.EntryType == TimelineEntryType.Random), Has.Count.EqualTo(1));
                Assert.That(restorePlan.Succeeded, Is.True);
                Assert.That(restorePlan.Events, Has.Count.EqualTo(1));
                Assert.That(restorePlan.Events[0], Is.SameAs(annualEvent));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(annualEvent);
            }
        }

        [Test]
        public void AdvanceYear_RejectsDifferentStableIdFromPastYear()
        {
            var settlement = new SettlementInstance { CurrentYear = 7 };
            var timeline = new TimelineSystem(settlement, new FirstRandom());
            timeline.AdvanceYear(new HuntRecord { RecordId = "ordered", Year = 7 });

            timeline.AdvanceYear(new HuntRecord { RecordId = "different-but-stale", Year = 7 });

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
        public void ContentCatalog_MigratesOneLegacyHuntExactlyOnce()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PlayableSettlementContentCatalog>("Assets/GameScripts/GameLogic/HuntingInDarkness/Content/Settlement/PlayableSettlementContentCatalog.asset");
            var manager = new SettlementManager(1);
            manager.Data.CurrentYear = 6;
            manager.Data.HuntsPerYear = 2;
            manager.Data.HuntsCompletedThisYear = 1;

            Assert.That(catalog.ApplyTo(manager), Is.True);
            Assert.That(manager.Data.CurrentYear, Is.EqualTo(7));
            Assert.That(manager.Data.CampaignPacingSchemaVersion, Is.EqualTo(SettlementInstance.CurrentCampaignPacingSchemaVersion));
            Assert.That(manager.Data.Timeline.Exists(entry => entry != null && entry.Year == 7), Is.True);
            Assert.That(manager.Data.HuntsCompletedThisYear, Is.Zero);
            Assert.That(manager.Data.HuntsPerYear, Is.EqualTo(1));

            Assert.That(catalog.ApplyTo(manager), Is.True);
            Assert.That(manager.Data.CurrentYear, Is.EqualTo(7));
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

        private sealed class FirstRandom : HuntingInDarkness.GameCore.Foundation.IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
        }
    }
}
