using System.Collections.Generic;
using System.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.ContentTables;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableInventionTableTests
    {
        private readonly List<Object> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object createdObject in createdObjects)
                if (createdObject != null)
                    Object.DestroyImmediate(createdObject);
            createdObjects.Clear();
        }

        [Test]
        public void Build_ResolvesStableGraphAndCombinesCosts()
        {
            ItemData stone = CreateItem("broken_stone", "碎石");
            InventionTableRecord faith = CreateRecord("faith", "信仰");
            faith.costs.Add(new InventionCostTableRecord { itemId = "broken_stone", count = 1 });
            faith.costs.Add(new InventionCostTableRecord { itemId = "broken_stone", count = 2 });
            InventionTableRecord ritual = CreateRecord("ritual", "仪式");
            ritual.prerequisiteIds.Add("faith");
            ritual.effects.Add(new InventionEffectTableRecord { kind = "ModifyWillpowerMaximum", target = "AvailableHunters", value = 1 });

            List<InventionData> inventions = Track(PlayableInventionTableRuntime.Build(new[] { faith, ritual }, new[] { stone }));

            Assert.That(inventions, Has.Count.EqualTo(2));
            Assert.That(inventions[0].ContentId, Is.EqualTo("faith"));
            Assert.That(inventions[0].costs, Has.Count.EqualTo(1));
            Assert.That(inventions[0].costs[0].resource, Is.SameAs(stone));
            Assert.That(inventions[0].costs[0].count, Is.EqualTo(3));
            Assert.That(inventions[1].prerequisites, Is.EqualTo(new[] { inventions[0] }));
            Assert.That(inventions[1].unlockEffects, Has.Count.EqualTo(1));
            Assert.That(inventions[1].unlockEffects[0].kind, Is.EqualTo(InventionEffectKind.ModifyWillpowerMaximum));
        }

        [Test]
        public void Build_RejectsUnknownTargetAndZeroStructuredEffects()
        {
            InventionTableRecord unknownTarget = CreateRecord("unknown_target", "未知目标");
            unknownTarget.effects.Add(new InventionEffectTableRecord { kind = "ModifyStrength", target = "Visitors", value = 1 });
            InventionTableRecord zeroValue = CreateRecord("zero_value", "零值效果");
            zeroValue.effects.Add(new InventionEffectTableRecord { kind = "ModifyWillpowerMaximum", target = "AvailableHunters", value = 0 });
            var errors = new List<string>();

            List<InventionData> inventions = PlayableInventionTableRuntime.Build(new[] { unknownTarget, zeroValue }, null, null, errors.Add);

            Assert.That(inventions, Is.Empty);
            Assert.That(errors.Exists(error => error.Contains("无效效果目标")), Is.True);
            Assert.That(errors.Exists(error => error.Contains("不能为 0")), Is.True);
        }

        [Test]
        public void Build_RejectsIdentityConflictsBrokenReferencesCyclesAndOverflow()
        {
            ItemData stone = CreateItem("broken_stone", "碎石");
            InventionData existing = ScriptableObject.CreateInstance<InventionData>();
            existing.name = "training_asset";
            existing.ConfigureContentId("training");
            existing.inventionName = "训练";
            createdObjects.Add(existing);
            InventionTableRecord colliding = CreateRecord("training", "另一个训练");
            InventionTableRecord missing = CreateRecord("missing", "断裂分支");
            missing.prerequisiteIds.Add("unknown");
            InventionTableRecord cycleA = CreateRecord("cycle_a", "循环甲");
            cycleA.prerequisiteIds.Add("cycle_b");
            InventionTableRecord cycleB = CreateRecord("cycle_b", "循环乙");
            cycleB.prerequisiteIds.Add("cycle_a");
            InventionTableRecord overflow = CreateRecord("overflow", "溢出成本");
            overflow.costs.Add(new InventionCostTableRecord { itemId = "broken_stone", count = int.MaxValue });
            overflow.costs.Add(new InventionCostTableRecord { itemId = "broken_stone", count = 1 });
            var errors = new List<string>();

            List<InventionData> inventions = PlayableInventionTableRuntime.Build(new[] { colliding, missing, cycleA, cycleB, overflow }, new[] { stone }, new[] { existing }, errors.Add);

            Assert.That(inventions, Is.Empty);
            Assert.That(errors.Exists(error => error.Contains("身份冲突")), Is.True);
            Assert.That(errors.Exists(error => error.Contains("未知发明")), Is.True);
            Assert.That(errors.Exists(error => error.Contains("循环")), Is.True);
            Assert.That(errors.Exists(error => error.Contains("溢出")), Is.True);
        }

        [Test]
        public void Build_RejectsDependentRecordWhenItsPrerequisiteIsInvalid()
        {
            ItemData stone = CreateItem("broken_stone", "碎石");
            InventionTableRecord invalid = CreateRecord("invalid", "无效前置");
            invalid.costs.Add(new InventionCostTableRecord { itemId = "missing_item", count = 1 });
            InventionTableRecord dependent = CreateRecord("dependent", "依赖者");
            dependent.prerequisiteIds.Add("invalid");

            List<InventionData> inventions = PlayableInventionTableRuntime.Build(new[] { invalid, dependent }, new[] { stone });

            Assert.That(inventions, Is.Empty);
        }

        [Test]
        public void RuntimeCache_RebuildsWhenDependencyObjectsChange()
        {
            ItemData firstStone = CreateItem("broken_stone", "第一块碎石");
            ItemData secondStone = CreateItem("broken_stone", "第二块碎石");
            var table = new TextAsset("{\"version\":1,\"inventions\":[{\"id\":\"faith\",\"inventionName\":\"信仰\",\"costs\":[{\"itemId\":\"broken_stone\",\"count\":1}],\"category\":\"Knowledge\"}]}");
            createdObjects.Add(table);

            List<InventionData> first = Track(new List<InventionData>(PlayableInventionTableRuntime.GetInventions(table, new[] { firstStone }, null)));
            List<InventionData> second = Track(new List<InventionData>(PlayableInventionTableRuntime.GetInventions(table, new[] { secondStone }, null)));

            Assert.That(first[0], Is.Not.SameAs(second[0]));
            Assert.That(second[0].costs[0].resource, Is.SameAs(secondStone));
        }

        [Test]
        public async Task RuntimeBranch_UnlocksThroughActionQueueAndAppliesRitualEffect()
        {
            ItemData stone = CreateItem("broken_stone", "碎石");
            ItemData organ = CreateItem("soft_organ", "柔软器官");
            InventionTableRecord faithRecord = CreateRecord("faith", "信仰");
            faithRecord.costs.Add(new InventionCostTableRecord { itemId = "broken_stone", count = 1 });
            InventionTableRecord ritualRecord = CreateRecord("ritual", "仪式");
            ritualRecord.prerequisiteIds.Add("faith");
            ritualRecord.costs.Add(new InventionCostTableRecord { itemId = "soft_organ", count = 1 });
            ritualRecord.effectDescription = "全体可出战猎人的意志点上限 +1。";
            ritualRecord.effects.Add(new InventionEffectTableRecord { kind = "ModifyWillpowerMaximum", target = "AvailableHunters", value = 1 });
            List<InventionData> inventions = Track(PlayableInventionTableRuntime.Build(new[] { faithRecord, ritualRecord }, new[] { stone, organ }));
            var settlement = new SettlementInstance();
            settlement.AddResource(stone, 1);
            settlement.AddResource(organ, 1);
            var hunter = new HunterInstance(null, 901) { Name = "守誓者", IsAlive = true, Willpower = 1, WillpowerMax = 1 };
            settlement.Hunters.Add(hunter);
            var inventionSystem = new InventionSystem(settlement) { AllInventions = inventions };
            using var session = new PlayableSettlementActionSession(settlement, new EmptyWeaponTrainingContent(), inventionSystem: inventionSystem);

            SettlementInventionCommandResult faithResult = await session.UnlockInventionAsync(inventions[0]);
            SettlementInventionCommandResult ritualResult = await session.UnlockInventionAsync(inventions[1]);

            Assert.That(faithResult.Succeeded, Is.True, faithResult.Reason);
            Assert.That(ritualResult.Succeeded, Is.True, ritualResult.Reason);
            Assert.That(settlement.GetResource("broken_stone"), Is.Zero);
            Assert.That(settlement.GetResource("soft_organ"), Is.Zero);
            Assert.That(hunter.WillpowerMax, Is.EqualTo(2));
            Assert.That(settlement.Timeline.ConvertAll(entry => entry.EventId), Is.EqualTo(new[] { "invention:faith", "invention:ritual" }));
        }

        private ItemData CreateItem(string id, string displayName)
        {
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            item.name = id;
            item.ConfigureContentId(id);
            item.itemName = displayName;
            item.itemType = ItemType.Resource;
            createdObjects.Add(item);
            return item;
        }

        private static InventionTableRecord CreateRecord(string id, string displayName)
        {
            return new InventionTableRecord { id = id, inventionName = displayName, category = "Knowledge" };
        }

        private List<InventionData> Track(List<InventionData> inventions)
        {
            createdObjects.AddRange(inventions);
            return inventions;
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
    }
}
