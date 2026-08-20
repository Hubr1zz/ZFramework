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
    }
}
