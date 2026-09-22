using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.GameCore.Settlement;
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
                Effects = new List<EventResolutionMemoryEffect> { new() { EffectType = "AddResource", TargetName = "broken_stone", Applied = true } }
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
                EventMemories = new List<EventResolutionMemory> { new() { MemoryId = "memory:1", EventId = "event-1", Success = false, RollValue = 4 } }
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
                Effects = new List<EventResolutionMemoryEffect> { new() { EffectType = "AddResource", TargetName = "broken_stone", Applied = true } }
            });

            Assert.That(text, Does.Contain("选择：观察"));
            Assert.That(text, Does.Contain("理解 8/7"));
            Assert.That(text, Does.Contain("获得资源（碎石）"));

            text = CampLedgerPresentation.FormatEventMemory(new SettlementEventMemory { SelectionMode = EventResolutionSelectionMode.Automatic, OptionText = "直接带回" });
            Assert.That(text, Does.Contain("自动结算：直接带回"));
        }

        [Test]
        public void FatalInjuryMemory_RoundTripsAllFieldsAndFormatsOnlyPlayerFacingDetails()
        {
            var source = new EventResolutionMemory
            {
                MemoryId = "hunt-event-memory:expedition-1:1:hunt_crushing_slab",
                EventId = "hunt_crushing_slab",
                EventName = "塌方",
                SourceContextId = "expedition-1",
                OccurrenceSequence = 1,
                Success = true,
                Effects = new List<EventResolutionMemoryEffect>
                {
                    new()
                    {
                        EffectIndex = 0,
                        EffectType = EventEffectType.FatalInjury.ToString(),
                        ResolvedTargetId = "arms",
                        StateChanged = true,
                        PreviousValue = 4,
                        CurrentValue = 1,
                        HasDeathCard = true,
                        DeathCard = DeathCardType.Survive,
                        PermanentInjuryId = "broken-arm",
                        DeathDeckId = "technical-deck-id",
                        FacedownPosition = 7,
                        HunterDied = false,
                        Applied = true
                    }
                }
            };

            EventResolutionMemory restored = JsonUtility.FromJson<EventResolutionMemory>(JsonUtility.ToJson(source));
            Assert.That(restored.SourceContextId, Is.EqualTo("expedition-1"));
            Assert.That(restored.OccurrenceSequence, Is.EqualTo(1));
            Assert.That(restored.Effects[0].DeathCard, Is.EqualTo(DeathCardType.Survive));
            Assert.That(restored.Effects[0].PermanentInjuryId, Is.EqualTo("broken-arm"));
            string text = CampLedgerPresentation.FormatEventMemory(restored);
            Assert.That(text, Does.Contain("死亡牌：存活"));
            Assert.That(text, Does.Contain("部位 arms"));
            Assert.That(text, Does.Contain("剩余生命 1"));
            Assert.That(text, Does.Contain("永久损伤：broken-arm"));
            Assert.That(text, Does.Contain("猎人存活"));
            Assert.That(text, Does.Not.Contain("technical-deck-id"));
            Assert.That(text, Does.Not.Contain("7"));
        }

        [Test]
        public void HuntRecordMemoryRules_RejectLegacyCrossContextForgedSequenceAndOverflow()
        {
            var legacy = new HuntRecord
            {
                RecordId = "expedition-legacy",
                ReturnSchemaVersion = 3,
                EventMemorySchemaVersion = 3,
                Memories = new List<EventResolutionMemory> { CreateHuntMemory("expedition-legacy", 1, "root") }
            };
            Assert.That(EventResolutionMemoryRules.TryValidateHuntRecord(legacy, out _), Is.False);

            HuntRecord valid = new()
            {
                RecordId = "expedition-1",
                ReturnSchemaVersion = HuntRecord.CurrentReturnSchemaVersion,
                EventMemorySchemaVersion = HuntRecord.CurrentEventMemorySchemaVersion,
                Memories = new List<EventResolutionMemory> { CreateHuntMemory("expedition-1", 1, "root") }
            };
            Assert.That(EventResolutionMemoryRules.TryValidateHuntRecord(valid, out string reason), Is.True, reason);
            valid.Memories[0].SourceContextId = "expedition-other";
            Assert.That(EventResolutionMemoryRules.TryValidateHuntRecord(valid, out _), Is.False);
            valid.Memories[0].SourceContextId = valid.RecordId;
            valid.Memories[0].MemoryId = "hunt-event-memory:expedition-1:99:root";
            Assert.That(EventResolutionMemoryRules.TryValidateHuntRecord(valid, out _), Is.False);

            valid.Memories = new List<EventResolutionMemory>();
            for (int index = 0; index < EventResolutionMemoryRules.MaximumMemories + 1; index++)
                valid.Memories.Add(CreateHuntMemory(valid.RecordId, index + 1, $"event-{index}"));
            Assert.That(EventResolutionMemoryRules.TryValidateHuntRecord(valid, out _), Is.False);
        }

        private static EventResolutionMemory CreateHuntMemory(string expeditionId, int sequence, string eventId)
        {
            return new EventResolutionMemory
            {
                MemoryId = $"hunt-event-memory:{expeditionId}:{sequence}:{eventId}",
                EventId = eventId,
                SourceContextId = expeditionId,
                OccurrenceSequence = sequence,
                Success = true
            };
        }
    }
}
