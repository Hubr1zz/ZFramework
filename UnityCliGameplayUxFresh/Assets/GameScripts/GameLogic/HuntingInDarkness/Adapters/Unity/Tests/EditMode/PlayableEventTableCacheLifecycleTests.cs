using System.Collections.Generic;
using System.Reflection;
using HuntingInDarkness.ContentTables;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableEventTableCacheLifecycleTests
    {
        private const string SymptomCatalogPath = "Assets/GameScripts/GameLogic/HuntingInDarkness/Content/Settlement/Symptoms/PlayableSymptomCatalog.asset";
        private static readonly FieldInfo cachedRecordsField = typeof(PlayableEventTableRuntime).GetField("cachedRecords", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo prepareGenerationMethod = typeof(PlayableEventTableRuntime).GetMethod("PrepareGeneration", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo swapGenerationMethod = typeof(PlayableEventTableRuntime).GetMethod("SwapGeneration", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo retireGenerationMethod = typeof(PlayableEventTableRuntime).GetMethod("RetireGeneration", BindingFlags.Static | BindingFlags.NonPublic);

        [SetUp]
        public void SetUp() => PlayableSymptomRuntime.Configure(AssetDatabase.LoadAssetAtPath<PlayableSymptomCatalog>(SymptomCatalogPath));

        [TearDown]
        public void TearDown()
        {
            PlayableEventTableRuntime.ClearCache();
            PlayableSymptomRuntime.Configure(null);
            PlayableBloodlineRuntime.Configure(null);
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

        [Test]
        public void StagedGeneration_CanRollbackWithoutDestroyingPublishedEvents()
        {
            IReadOnlyList<EventData> publishedEvents = PlayableEventTableRuntime.Rebuild();
            EventData publishedEvent = publishedEvents[0];
            object stagedGeneration = prepareGenerationMethod.Invoke(null, new object[] { PlayableSymptomRuntime.Catalog, PlayableBloodlineRuntime.Content });
            IReadOnlyList<EventData> stagedEvents = GetGenerationEvents(stagedGeneration);
            EventData stagedEvent = stagedEvents[0];
            Assert.That(publishedEvent != null, Is.True);

            object previousGeneration = swapGenerationMethod.Invoke(null, new[] { stagedGeneration });
            Assert.That(PlayableEventTableRuntime.GetEvents()[0], Is.SameAs(stagedEvent));
            object rejectedGeneration = swapGenerationMethod.Invoke(null, new[] { previousGeneration });
            retireGenerationMethod.Invoke(null, new[] { rejectedGeneration });

            Assert.That(PlayableEventTableRuntime.GetEvents()[0], Is.SameAs(publishedEvent));
            Assert.That(publishedEvent != null, Is.True, "Rollback must preserve the previously published generation.");
            Assert.That(stagedEvent == null, Is.True, "Rejected generation must release its owned transient events.");
        }

        [Test]
        public void DependencyChange_DoesNotRetirePublishedGenerationUntilExplicitRebuild()
        {
            IReadOnlyList<EventData> firstEvents = PlayableEventTableRuntime.Rebuild();
            EventData firstEvent = firstEvents[0];

            PlayableBloodlineRuntime.Configure(new DelegatingBloodlineContent(PlayableBloodlineRuntime.Content));
            IReadOnlyList<EventData> unchangedEvents = PlayableEventTableRuntime.GetEvents();

            Assert.That(unchangedEvents[0], Is.SameAs(firstEvent));
            Assert.That(firstEvent != null, Is.True);

            IReadOnlyList<EventData> replacementEvents = PlayableEventTableRuntime.Rebuild();

            Assert.That(firstEvent == null, Is.True);
            Assert.That(replacementEvents, Is.Not.Empty);
            Assert.That(replacementEvents[0], Is.Not.SameAs(firstEvent));
        }

        [Test]
        public void Rebuild_WithInvalidCandidate_PreservesPublishedGeneration()
        {
            IReadOnlyList<EventData> publishedEvents = PlayableEventTableRuntime.Rebuild();
            EventData publishedEvent = publishedEvents[0];
            PlayableBloodlineRuntime.Configure(new EmptyBloodlineContent());
            LogAssert.Expect(LogType.Error, "[ContentTable] 事件 random_bloodline_awakening 含无效选项或效果。");

            IReadOnlyList<EventData> rebuiltEvents = PlayableEventTableRuntime.Rebuild();

            Assert.That(rebuiltEvents, Is.SameAs(publishedEvents));
            Assert.That(rebuiltEvents[0], Is.SameAs(publishedEvent));
            Assert.That(publishedEvent != null, Is.True);
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

        private static IReadOnlyList<EventData> GetGenerationEvents(object generation)
        {
            PropertyInfo eventsProperty = generation.GetType().GetProperty("Events", BindingFlags.Instance | BindingFlags.Public);
            return (IReadOnlyList<EventData>)eventsProperty.GetValue(generation);
        }

        private sealed class DelegatingBloodlineContent : IHunterBloodlineContent
        {
            private readonly IHunterBloodlineContent inner;

            public DelegatingBloodlineContent(IHunterBloodlineContent inner)
            {
                this.inner = inner;
            }

            public IReadOnlyList<HunterBloodlineDefinition> Definitions => inner.Definitions;
            public bool TryGet(string bloodlineId, out HunterBloodlineDefinition definition) => inner.TryGet(bloodlineId, out definition);
        }

        private sealed class EmptyBloodlineContent : IHunterBloodlineContent
        {
            public IReadOnlyList<HunterBloodlineDefinition> Definitions => System.Array.Empty<HunterBloodlineDefinition>();

            public bool TryGet(string bloodlineId, out HunterBloodlineDefinition definition)
            {
                definition = null;
                return false;
            }
        }
    }
}
