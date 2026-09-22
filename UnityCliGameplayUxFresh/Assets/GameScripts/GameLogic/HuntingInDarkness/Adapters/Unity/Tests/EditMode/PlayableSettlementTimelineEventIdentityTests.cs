using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableSettlementTimelineEventIdentityTests
    {
        [TearDown]
        public void TearDown() => PlayableSettlementEventRegistry.Configure(Array.Empty<EventData>());

        [Test]
        public void ReturnRandom_StoresStableEventAndSourceIdentityAfterAssetRename()
        {
            EventData gameEvent = CreateEvent("legacy-asset", "stable-event");
            var settlement = new SettlementInstance { CurrentYear = 1 };
            var timeline = new TimelineSystem(settlement, new FirstRandom()) { RandomEventPool = new() { gameEvent } };
            try
            {
                Assert.That(timeline.TryBindCalendar(new CampaignCalendarDefinition("single", new[] { new SeasonDefinition("season", "季", 0) }), out string bindReason), Is.True, bindReason);
                Assert.That(timeline.GetEventWorkItemsForYear(1), Is.Empty, "开局年度入口不得提前抽取回营随机事件。");

                IReadOnlyList<EventData> events = timeline.AdvanceCalendar(new HuntRecord { RecordId = "return-1", Year = 1 }, out _, out string reason);

                Assert.That(reason, Is.Empty);
                Assert.That(events, Is.EqualTo(new[] { gameEvent }));
                Assert.That(settlement.Timeline, Has.Count.EqualTo(1));
                Assert.That(settlement.Timeline[0].EventId, Is.EqualTo("stable-event"));
                Assert.That(settlement.Timeline[0].SourceHuntRecordId, Is.EqualTo("return-1"));
                gameEvent.name = "renamed-asset";
                Assert.That(timeline.ResolveEvent("stable-event"), Is.SameAs(gameEvent));
                Assert.That(timeline.ResolveEvent("legacy-asset"), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameEvent);
            }
        }

        [Test]
        public void ScheduledChoice_IsDueInYearThreeExactlyOnce()
        {
            EventData gameEvent = CreateEvent("main-face-echo-asset", "main_face_echo");
            gameEvent.category = EventCategory.Scheduled;
            gameEvent.eventType = GameEventType.Choice;
            gameEvent.options.Add(new EventOption { optionText = "辨认", checkType = CheckType.Understanding, checkTarget = 7 });
            gameEvent.options.Add(new EventOption { optionText = "带回", checkType = CheckType.None });
            var settlement = new SettlementInstance { CurrentYear = 1 };
            var timeline = new TimelineSystem(settlement, new FirstRandom()) { RandomEventPool = new() { gameEvent } };
            try
            {
                Assert.That(timeline.TryScheduleEventAfterYears("main_face_echo", 2, out string scheduleReason), Is.True, scheduleReason);

                List<SettlementEventWork> firstWorks = timeline.GetEventWorkItemsForYear(3);
                Assert.That(firstWorks.FindAll(work => work.TimelineEntry != null && work.TimelineEntry.EntryType == TimelineEntryType.Scheduled), Has.Count.EqualTo(1));
                Assert.That(firstWorks.Find(work => work.TimelineEntry != null && work.TimelineEntry.EntryType == TimelineEntryType.Scheduled).Event, Is.SameAs(gameEvent));

                List<SettlementEventWork> secondWorks = timeline.GetEventWorkItemsForYear(3);
                Assert.That(secondWorks.Exists(work => work.TimelineEntry != null && work.TimelineEntry.EntryType == TimelineEntryType.Scheduled), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameEvent);
            }
        }

        [Test]
        public void Migration_ConvertsUniqueLegacyAssetNameAndIgnoresDynamicEntries()
        {
            EventData gameEvent = CreateEvent("legacy-asset", "stable-event");
            var eventEntry = new AnnalEntry { EventId = "legacy-asset", EventName = "旧名称", EntryType = TimelineEntryType.Random };
            var dynamicEntry = new AnnalEntry { EventId = "death:7", EventName = "死亡", EntryType = TimelineEntryType.RosterChanged };
            var settlement = new SettlementInstance { Timeline = new() { eventEntry, dynamicEntry } };
            try
            {
                PlayableSettlementEventRegistry.Configure(new[] { gameEvent });

                bool changed = PlayableSettlementEventRegistry.MigratePersistentState(settlement);
                bool repeated = PlayableSettlementEventRegistry.MigratePersistentState(settlement);

                Assert.That(changed, Is.True);
                Assert.That(repeated, Is.False);
                Assert.That(eventEntry.EventId, Is.EqualTo("stable-event"));
                Assert.That(dynamicEntry.EventId, Is.EqualTo("death:7"));
                Assert.That(settlement.TimelineEventIdentitySchemaVersion, Is.EqualTo(PlayableSettlementEventRegistry.CurrentIdentitySchemaVersion));
                Assert.That(settlement.TimelineEventIdentityMigrationDiagnostic, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameEvent);
            }
        }

        [Test]
        public void Migration_PreservesUnknownIdentityWithoutAdvancingSchema()
        {
            var settlement = new SettlementInstance
            {
                Timeline = new() { new AnnalEntry { EventId = "missing-event", EntryType = TimelineEntryType.Scheduled } }
            };
            PlayableSettlementEventRegistry.Configure(Array.Empty<EventData>());

            bool changed = PlayableSettlementEventRegistry.MigratePersistentState(settlement);

            Assert.That(changed, Is.False);
            Assert.That(settlement.Timeline[0].EventId, Is.EqualTo("missing-event"));
            Assert.That(settlement.TimelineEventIdentitySchemaVersion, Is.Zero);
            Assert.That(settlement.TimelineEventIdentityMigrationDiagnostic, Is.Not.Empty);
        }

        [Test]
        public void Registry_RejectsCanonicalAndLegacyAliasCollision()
        {
            EventData first = CreateEvent("legacy-b", "stable-a");
            EventData second = CreateEvent("asset-b", "legacy-b");
            try
            {
                PlayableSettlementEventRegistry.Configure(new[] { first, second });

                Assert.That(PlayableSettlementEventRegistry.IsValid, Is.False);
                Assert.That(PlayableSettlementEventRegistry.Diagnostic, Does.Contain("legacy-b"));
                Assert.That(PlayableSettlementEventRegistry.TryResolveUnique("legacy-b", out _), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void Registry_RejectsTimelineEventWithoutExplicitContentId()
        {
            EventData gameEvent = ScriptableObject.CreateInstance<EventData>();
            gameEvent.name = "implicit-id";
            try
            {
                PlayableSettlementEventRegistry.Configure(new[] { gameEvent });

                Assert.That(PlayableSettlementEventRegistry.IsValid, Is.False);
                Assert.That(PlayableSettlementEventRegistry.Diagnostic, Does.Contain("ContentId"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameEvent);
            }
        }

        [Test]
        public void Migration_RejectsFutureSchemaWithoutChangingTimeline()
        {
            var entry = new AnnalEntry { EventId = "future-event", EntryType = TimelineEntryType.Random };
            var settlement = new SettlementInstance
            {
                TimelineEventIdentitySchemaVersion = PlayableSettlementEventRegistry.CurrentIdentitySchemaVersion + 1,
                Timeline = new() { entry }
            };

            bool changed = PlayableSettlementEventRegistry.MigratePersistentState(settlement);

            Assert.That(changed, Is.False);
            Assert.That(entry.EventId, Is.EqualTo("future-event"));
            Assert.That(settlement.TimelineEventIdentitySchemaVersion, Is.EqualTo(PlayableSettlementEventRegistry.CurrentIdentitySchemaVersion + 1));
            Assert.That(settlement.TimelineEventIdentityMigrationDiagnostic, Does.Contain("高于当前版本"));
        }

        [Test]
        public void StandaloneNode_DoesNotCompleteSettlementTimeline()
        {
            EventData gameEvent = CreateEvent("shared-asset", "shared-event");
            var entry = new AnnalEntry { EventId = "shared-event", EventName = "共享事件", EntryType = TimelineEntryType.Random };
            var settlement = new SettlementInstance { Timeline = new() { entry } };
            var eventSystem = new EventSystem(settlement, new FirstRandom());
            try
            {
                eventSystem.ResolveNarrativeNodeStandalone(gameEvent);

                Assert.That(entry.IsCompleted, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameEvent);
            }
        }

        [Test]
        public async Task ExactWork_CompletesOnlyBoundOccurrenceForRepeatedContent()
        {
            EventData gameEvent = CreateEvent("repeat-asset", "repeat-event", "stone");
            var first = new AnnalEntry { Year = 2, EventId = "repeat-event", EventName = "重复事件", EntryType = TimelineEntryType.Random };
            var second = new AnnalEntry { Year = 3, EventId = "repeat-event", EventName = "重复事件", EntryType = TimelineEntryType.Random };
            var settlement = new SettlementInstance { Timeline = new() { first, second } };
            var eventSystem = new EventSystem(settlement, new FirstRandom());
            try
            {
                using var session = new PlayableSettlementActionSession(settlement, new EmptyWeaponTrainingContent(), eventSystem);

                SettlementEventCommandResult firstResult = await session.ResolveEventsAsync(new[] { new SettlementEventWork(gameEvent, first) });
                Assert.That(firstResult.Succeeded, Is.True, firstResult.Reason);
                Assert.That(first.IsCompleted, Is.True);
                Assert.That(second.IsCompleted, Is.False);
                Assert.That(settlement.GetResource("stone"), Is.EqualTo(1));

                SettlementEventCommandResult secondResult = await session.ResolveEventsAsync(new[] { new SettlementEventWork(gameEvent, second) });
                Assert.That(secondResult.Succeeded, Is.True, secondResult.Reason);
                Assert.That(second.IsCompleted, Is.True);
                Assert.That(settlement.GetResource("stone"), Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameEvent);
            }
        }

        [Test]
        public async Task ExactWorks_WithRepeatedContentInOneBatchCompleteBothOccurrences()
        {
            EventData gameEvent = CreateEvent("repeat-batch-asset", "repeat-batch-event", "stone");
            var first = new AnnalEntry { Year = 2, EventId = gameEvent.ContentId, EntryType = TimelineEntryType.Random };
            var second = new AnnalEntry { Year = 3, EventId = gameEvent.ContentId, EntryType = TimelineEntryType.Random };
            var settlement = new SettlementInstance { Timeline = new() { first, second } };
            var eventSystem = new EventSystem(settlement, new FirstRandom());
            try
            {
                using var session = new PlayableSettlementActionSession(settlement, new EmptyWeaponTrainingContent(), eventSystem);

                SettlementEventCommandResult result = await session.ResolveEventsAsync(new[] { new SettlementEventWork(gameEvent, first), new SettlementEventWork(gameEvent, second) });

                Assert.That(result.Succeeded, Is.True, result.Reason);
                Assert.That(first.IsCompleted, Is.True);
                Assert.That(second.IsCompleted, Is.True);
                Assert.That(settlement.GetResource("stone"), Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameEvent);
            }
        }

        [Test]
        public async Task ExactWork_RejectsForeignEntryBeforeApplyingEffects()
        {
            EventData gameEvent = CreateEvent("foreign-asset", "foreign-event", "stone");
            var foreignEntry = new AnnalEntry { EventId = "foreign-event", EntryType = TimelineEntryType.Random };
            var settlement = new SettlementInstance();
            var eventSystem = new EventSystem(settlement, new FirstRandom());
            try
            {
                using var session = new PlayableSettlementActionSession(settlement, new EmptyWeaponTrainingContent(), eventSystem);

                SettlementEventCommandResult result = await session.ResolveEventsAsync(new[] { new SettlementEventWork(gameEvent, foreignEntry) });

                Assert.That(result.Succeeded, Is.False);
                Assert.That(settlement.GetResource("stone"), Is.Zero);
                Assert.That(foreignEntry.IsCompleted, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameEvent);
            }
        }

        [Test]
        public async Task ExactWorks_RejectWholeBatchBeforeApplyingValidPrefix()
        {
            EventData gameEvent = CreateEvent("atomic-asset", "atomic-event", "stone");
            var validEntry = new AnnalEntry { EventId = gameEvent.ContentId, EntryType = TimelineEntryType.Random };
            var foreignEntry = new AnnalEntry { EventId = gameEvent.ContentId, EntryType = TimelineEntryType.Random };
            var settlement = new SettlementInstance { Timeline = new() { validEntry } };
            var eventSystem = new EventSystem(settlement, new FirstRandom());
            try
            {
                using var session = new PlayableSettlementActionSession(settlement, new EmptyWeaponTrainingContent(), eventSystem);

                SettlementEventCommandResult result = await session.ResolveEventsAsync(new[] { new SettlementEventWork(gameEvent, validEntry), new SettlementEventWork(gameEvent, foreignEntry) });

                Assert.That(result.Succeeded, Is.False);
                Assert.That(validEntry.IsCompleted, Is.False);
                Assert.That(settlement.GetResource("stone"), Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameEvent);
            }
        }

        [Test]
        public void RestoreProjection_FailedPresentationWithChildCheckpointCanPrepareRetry()
        {
            EventData child = CreateEvent("child-asset", "child-event");
            var settlement = new SettlementInstance();
            settlement.CommitEventChainOccurrence("chain", -1, new[] { child.ContentId }, 2, 7);
            var projection = new SettlementEventRestoreProjection(settlement, id => id == child.ContentId ? child : null);
            try
            {
                Assert.That(projection.Complete(false), Is.False);
                Assert.That(projection.HasRecoverableCheckpoint, Is.True);

                SettlementEventRestorePlan retry = projection.Prepare();

                Assert.That(retry.Succeeded, Is.True, retry.FailureReason);
                Assert.That(retry.WorkItems, Has.Count.EqualTo(1));
                Assert.That(retry.WorkItems[0].RestoredOccurrence.EventId, Is.EqualTo(child.ContentId));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(child);
            }
        }

        private static EventData CreateEvent(string assetName, string contentId, string resourceId = null)
        {
            EventData gameEvent = ScriptableObject.CreateInstance<EventData>();
            gameEvent.name = assetName;
            gameEvent.eventName = assetName;
            gameEvent.category = EventCategory.Random;
            gameEvent.ConfigureContentId(contentId);
            if (!string.IsNullOrWhiteSpace(resourceId))
                gameEvent.immediateEffects.Add(new EventEffect { effectType = EventEffectType.AddResource, targetName = resourceId, value = 1 });
            return gameEvent;
        }

        private sealed class EmptyWeaponTrainingContent : IWeaponTrainingContent
        {
            public string RequiredInventionId => string.Empty;
            public string CostResourceId => string.Empty;
            public int ResourceCost => 0;
            public int Experience => 0;
            public bool TryGetFamily(string masteryId, out WeaponMasteryFamilyDefinition family)
            {
                family = null;
                return false;
            }
        }

        private sealed class FirstRandom : IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
        }
    }
}
