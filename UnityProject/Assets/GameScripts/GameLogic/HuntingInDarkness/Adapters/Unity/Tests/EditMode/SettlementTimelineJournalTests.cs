using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class SettlementTimelineJournalTests
    {
        private readonly List<Object> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            PlayableSettlementItemRegistry.Configure(null);
            PlayableSettlementInventionRegistry.Configure(null);
            foreach (Object createdObject in createdObjects)
                if (createdObject != null)
                    Object.DestroyImmediate(createdObject);
            createdObjects.Clear();
        }

        [Test]
        public void RecordInvention_IsIdempotentAndPreservesCommittedYear()
        {
            var settlement = new SettlementInstance { CurrentYear = 4 };

            bool first = SettlementTimelineJournal.RecordInvention(settlement, " stonecraft ", " 石工 ");
            bool second = SettlementTimelineJournal.RecordInvention(settlement, "stonecraft", "新名称");

            Assert.That(first, Is.True);
            Assert.That(second, Is.False);
            Assert.That(settlement.Timeline, Has.Count.EqualTo(1));
            Assert.That(settlement.Timeline[0].Year, Is.EqualTo(4));
            Assert.That(settlement.Timeline[0].EventId, Is.EqualTo("invention:stonecraft"));
            Assert.That(settlement.Timeline[0].EventName, Is.EqualTo("石工"));
            Assert.That(settlement.Timeline[0].IsCompleted, Is.True);
        }

        [Test]
        public void EventUnlock_RecordsInventionWithoutViewMutation()
        {
            var settlement = new SettlementInstance { CurrentYear = 3 };
            InventionData ritual = ScriptableObject.CreateInstance<InventionData>();
            ritual.name = "Ritual";
            ritual.ConfigureContentId("ritual");
            ritual.inventionName = "仪式";
            createdObjects.Add(ritual);
            PlayableSettlementInventionRegistry.Configure(new[] { ritual });
            var eventSystem = new EventSystem(settlement, new HuntingInDarkness.GameCore.Foundation.SystemRandomSource(7));

            eventSystem.ApplyEffect(new EventEffect { effectType = EventEffectType.UnlockInvention, targetName = "ritual" }, null);

            Assert.That(settlement.IsInventionUnlocked("ritual"), Is.True);
            Assert.That(settlement.Timeline, Has.Count.EqualTo(1));
            Assert.That(settlement.Timeline[0].EventId, Is.EqualTo("invention:ritual"));
            Assert.That(settlement.Timeline[0].EventName, Is.EqualTo("仪式"));
            Assert.That(settlement.Timeline[0].EntryType, Is.EqualTo(TimelineEntryType.Invention));
        }

        [Test]
        public void EventUnlock_RejectsUnknownInventionWithoutPollutingPersistentState()
        {
            var settlement = new SettlementInstance();
            var eventSystem = new EventSystem(settlement, new HuntingInDarkness.GameCore.Foundation.SystemRandomSource(7));
            LogAssert.Expect(LogType.Warning, "[EventSystem] 无法解锁未注册发明：unknown_invention");

            eventSystem.ApplyEffect(new EventEffect { effectType = EventEffectType.UnlockInvention, targetName = "unknown_invention" }, null);

            Assert.That(settlement.UnlockedInventions, Is.Empty);
            Assert.That(settlement.Timeline, Is.Empty);
        }

        [Test]
        public void CampLedgerPresentation_ResolvesStoredResourceIdsToPlayerFacingNames()
        {
            ItemData stone = ScriptableObject.CreateInstance<ItemData>();
            stone.name = "broken_stone";
            stone.ConfigureContentId("broken_stone");
            stone.itemName = "碎石";
            createdObjects.Add(stone);
            PlayableSettlementItemRegistry.Configure(new[] { stone });
            string summary = CampLedgerPresentation.FormatResources(new[] { "broken_stone", "broken_stone" });

            Assert.That(summary, Is.EqualTo("碎石×2"));
        }

        [Test]
        public void EventMemory_RecordsStructuredChoiceAndRejectsConflictingFact()
        {
            var settlement = new SettlementInstance();
            var memory = new SettlementEventMemory
            {
                MemoryId = "memory:1",
                EventId = "event-1",
                EventName = "事件一",
                ResolutionMode = "Choice",
                OptionId = "observe",
                OptionText = "观察",
                HasCheck = true,
                CheckType = "Understanding",
                Success = true,
                Total = 8,
                Target = 7,
                ResultText = "你看懂了。",
                Effects = new List<SettlementEventMemoryEffect> { new() { EffectType = "AddResource", TargetName = "broken_stone", Applied = true } }
            };

            Assert.That(settlement.TryRecordEventMemory(memory, out string reason), Is.True, reason);
            Assert.That(settlement.TryRecordEventMemory(memory, out reason), Is.True, reason);
            Assert.That(settlement.EventMemories, Has.Count.EqualTo(1));
            memory.Success = false;
            Assert.That(settlement.TryRecordEventMemory(memory, out reason), Is.False);
            Assert.That(reason, Does.Contain("事实不一致"));
        }

        [Test]
        public void EventMemory_JsonRoundTripAndLegacySaveHaveSafeDefaults()
        {
            var settlement = new SettlementInstance
            {
                EventMemorySchemaVersion = SettlementInstance.CurrentEventMemorySchemaVersion,
                EventMemories = new List<SettlementEventMemory> { new() { MemoryId = "memory:1", EventId = "event-1", Success = false, RollValue = 4 } }
            };
            SettlementInstance restored = JsonUtility.FromJson<SettlementInstance>(JsonUtility.ToJson(settlement));
            Assert.That(restored.EventMemories, Has.Count.EqualTo(1));
            Assert.That(restored.EventMemories[0].MemoryId, Is.EqualTo("memory:1"));
            Assert.That(restored.EventMemories[0].Success, Is.False);

            SettlementInstance legacy = JsonUtility.FromJson<SettlementInstance>("{\"CurrentYear\":3,\"Timeline\":[]}");
            Assert.That(legacy.EventMemorySchemaVersion, Is.Zero);
            Assert.That(legacy.EventMemories == null || legacy.EventMemories.Count == 0, Is.True);
        }

        [Test]
        public void CampLedgerPresentation_FormatsEventMemoryOutcome()
        {
            ItemData stone = ScriptableObject.CreateInstance<ItemData>();
            stone.name = "broken_stone";
            stone.ConfigureContentId("broken_stone");
            stone.itemName = "碎石";
            createdObjects.Add(stone);
            PlayableSettlementItemRegistry.Configure(new[] { stone });
            string text = CampLedgerPresentation.FormatEventMemory(new SettlementEventMemory
            {
                ResolutionMode = "Choice",
                OptionText = "观察",
                HasCheck = true,
                CheckType = "Understanding",
                Total = 8,
                Target = 7,
                Success = true,
                ResultText = "你看懂了。",
                Effects = new List<SettlementEventMemoryEffect> { new() { EffectType = "AddResource", TargetName = "broken_stone", Applied = true } }
            });

            Assert.That(text, Does.Contain("选择：观察"));
            Assert.That(text, Does.Contain("理解 8/7"));
            Assert.That(text, Does.Contain("获得资源（碎石）"));

            text = CampLedgerPresentation.FormatEventMemory(new SettlementEventMemory { SelectionMode = EventResolutionSelectionMode.Automatic, OptionText = "直接带回" });
            Assert.That(text, Does.Contain("自动结算：直接带回"));
        }
    }
}
