using System.Collections.Generic;
using System.Reflection;
using HuntingInDarkness.ContentTables;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;
using NUnit.Framework;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableEventTableCacheLifecycleTests
    {
        private static readonly FieldInfo cachedRecordsField = typeof(PlayableEventTableRuntime).GetField("cachedRecords", BindingFlags.Static | BindingFlags.NonPublic);

        [TearDown]
        public void TearDown()
        {
            PlayableEventTableRuntime.ClearCache();
            PlayableSymptomRuntime.Configure(null);
        }

        [Test]
        public void ClearCache_DestroysOwnedTransientEvents_AndAllowsReplacement()
        {
            PlayableEventTableRuntime.ClearCache();
            PlayableSymptomRuntime.Configure(null);
            SetCachedRecords("cache_lifecycle_a");

            EventData firstEvent = (EventData)PlayableEventTableRuntime.GetEvents()[0];
            Assert.That(firstEvent.name, Is.EqualTo("cache_lifecycle_a"));

            PlayableEventTableRuntime.ClearCache();
            Assert.That(firstEvent == null, Is.True, "ClearCache must destroy transient table events.");

            SetCachedRecords("cache_lifecycle_b");
            EventData replacement = (EventData)PlayableEventTableRuntime.GetEvents()[0];
            Assert.That(replacement.name, Is.EqualTo("cache_lifecycle_b"));
            Assert.That(ReferenceEquals(firstEvent, replacement), Is.False);
        }

        [Test]
        public void Rebuild_ReplacesOwnedTransientEvents()
        {
            IReadOnlyList<EventData> firstEvents = PlayableEventTableRuntime.Rebuild();
            Assert.That(firstEvents, Is.Not.Empty);
            EventData firstEvent = firstEvents[0];

            IReadOnlyList<EventData> replacementEvents = PlayableEventTableRuntime.Rebuild();

            Assert.That(firstEvent == null, Is.True, "Rebuild must release the previous transient cache.");
            Assert.That(replacementEvents, Is.Not.Empty);
            Assert.That(ReferenceEquals(firstEvent, replacementEvents[0]), Is.False);
        }

        private static void SetCachedRecords(string id)
        {
            cachedRecordsField.SetValue(null, new List<EventTableRecord>
            {
                new()
                {
                    id = id,
                    eventName = id,
                    eventType = nameof(GameEventType.Narrative),
                    category = nameof(EventCategory.Hunt),
                    displayText = id
                }
            });
        }
    }
}
