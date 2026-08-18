using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableRecruitmentServiceTests
    {
        [Test]
        public void TryRecruit_SpendsConfiguredCostRecordsAnnalAndUsesAvailableId()
        {
            var settlement = new SettlementInstance { CurrentYear = 2 };
            settlement.Hunters.Add(new HunterInstance(null, 100) { Name = "守夜人" });
            settlement.AddResource("蘑菇肉", 1);
            var management = new HunterManagementSystem(settlement, new SystemRandomSource(1));
            var template = ScriptableObject.CreateInstance<HunterData>();
            var costItem = ScriptableObject.CreateInstance<ItemData>();
            template.hunterName = "流浪者";
            costItem.itemName = "蘑菇肉";
            var service = new PlayableRecruitmentService(() => settlement, () => management, new[] { template }, costItem, 1, 6);

            try
            {
                bool recruited = service.TryRecruit(template, "  余烬  ", out HunterInstance hunter, out string reason);

                Assert.That(recruited, Is.True, reason);
                Assert.That(hunter.Name, Is.EqualTo("余烬"));
                Assert.That(hunter.InstanceId, Is.EqualTo(101));
                Assert.That(settlement.GetResource("蘑菇肉"), Is.Zero);
                Assert.That(settlement.LastRecruitmentYear, Is.EqualTo(2));
                Assert.That(settlement.Timeline, Has.Count.EqualTo(1));
                Assert.That(settlement.Timeline[0].EventName, Is.EqualTo("余烬 加入营地"));
                Assert.That(settlement.Timeline[0].EntryType, Is.EqualTo(TimelineEntryType.PlayerAdded));
                Assert.That(service.TryRecruit(template, "第二人", out _, out string secondReason), Is.False);
                Assert.That(secondReason, Does.Contain("本年"));
            }
            finally
            {
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(costItem);
            }
        }

        [Test]
        public void TryRecruit_EmptyCampProvidesFreeRecoveryEvenAfterSameYearRecruitment()
        {
            var settlement = new SettlementInstance { CurrentYear = 4, LastRecruitmentYear = 4 };
            settlement.Hunters.Add(new HunterInstance(null, 100) { Name = "逝者", IsAlive = false });
            var management = new HunterManagementSystem(settlement, new SystemRandomSource(1));
            var template = ScriptableObject.CreateInstance<HunterData>();
            var costItem = ScriptableObject.CreateInstance<ItemData>();
            costItem.itemName = "蘑菇肉";
            var service = new PlayableRecruitmentService(() => settlement, () => management, new List<HunterData> { template }, costItem, 1, 6);

            try
            {
                Assert.That(service.GetCurrentCost(), Is.Zero);
                Assert.That(service.TryRecruit(template, "续火者", out HunterInstance hunter, out string reason), Is.True, reason);
                Assert.That(hunter.IsAlive, Is.True);
                Assert.That(settlement.GetAliveHunters(), Has.Count.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(costItem);
            }
        }
    }
}
