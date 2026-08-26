using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableSettlementConsumableActionTests
    {
        private readonly List<UnityEngine.Object> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object createdObject in createdObjects)
                if (createdObject != null)
                    UnityEngine.Object.DestroyImmediate(createdObject);
            createdObjects.Clear();
        }

        [Test]
        public async Task UseConsumableAsync_RecoversSelectedPartConsumesOneAndPublishesFactsInOrder()
        {
            SettlementInstance settlement = CreateSettlement(out HunterInstance hunter, out ItemData item);
            hunter.HP.arms = 1;
            settlement.AddStoredItem(item, 1);
            var received = new List<string>();
            Action<HunterConsumableUsedEvent> usedHandler = evt => received.Add($"used:{evt.BodyPart}:{evt.PreviousHealth}>{evt.CurrentHealth}");
            Action<SettlementTransactionCommittedEvent> commitHandler = evt => received.Add($"commit:{evt.Kind}");
            EventBus.Subscribe(usedHandler);
            EventBus.Subscribe(commitHandler);
            try
            {
                using var session = CreateSession(settlement, item);
                SettlementConsumableCommandResult result = await session.UseConsumableAsync(hunter.InstanceId, item, HunterBodyPart.Arms);

                Assert.That(result.Succeeded, Is.True, result.Reason);
                Assert.That(hunter.HP.arms, Is.EqualTo(2));
                Assert.That(hunter.HP.body, Is.EqualTo(hunter.MaxHP.body));
                Assert.That(settlement.GetStoredItem(item), Is.Zero);
                Assert.That(received, Is.EqualTo(new[] { "used:Arms:1>2", "commit:Consumable" }));
            }
            finally
            {
                EventBus.Unsubscribe(usedHandler);
                EventBus.Unsubscribe(commitHandler);
            }
        }

        [Test]
        public async Task UseConsumableAsync_PreventedActionLeavesStateUntouched()
        {
            SettlementInstance settlement = CreateSettlement(out HunterInstance hunter, out ItemData item);
            hunter.HP.body = 1;
            settlement.AddStoredItem(item, 1);
            using var session = CreateSession(settlement, item);
            session.Reactors.RegisterGlobal(new PreventConsumableReactor());

            SettlementConsumableCommandResult result = await session.UseConsumableAsync(hunter.InstanceId, item, HunterBodyPart.Torso);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(hunter.HP.body, Is.EqualTo(1));
            Assert.That(settlement.GetStoredItem(item), Is.EqualTo(1));
        }

        [Test]
        public async Task UseConsumableAsync_ConcurrentLastCopyCommitsOnlyOnce()
        {
            SettlementInstance settlement = CreateSettlement(out HunterInstance hunter, out ItemData item);
            hunter.HP.body = 1;
            settlement.AddStoredItem(item, 1);
            using var session = CreateSession(settlement, item);

            (SettlementConsumableCommandResult first, SettlementConsumableCommandResult second) = await UniTask.WhenAll(session.UseConsumableAsync(hunter.InstanceId, item, HunterBodyPart.Torso), session.UseConsumableAsync(hunter.InstanceId, item, HunterBodyPart.Torso));
            SettlementConsumableCommandResult[] results = { first, second };

            Assert.That(Array.FindAll(results, result => result.Succeeded), Has.Length.EqualTo(1));
            Assert.That(settlement.GetStoredItem(item), Is.Zero);
            Assert.That(hunter.HP.body, Is.EqualTo(2));
        }

        [Test]
        public async Task UseConsumableAsync_RejectsForeignItemAndUnrecoverablePart()
        {
            SettlementInstance settlement = CreateSettlement(out HunterInstance hunter, out ItemData item);
            ItemData foreign = CreateItem("foreign_consumable");
            settlement.AddStoredItem(foreign, 1);
            using var session = CreateSession(settlement, item);

            SettlementConsumableCommandResult foreignResult = await session.UseConsumableAsync(hunter.InstanceId, foreign, HunterBodyPart.Torso);
            SettlementConsumableCommandResult unknownHunterResult = await session.UseConsumableAsync(9999, item, HunterBodyPart.Torso);
            SettlementConsumableCommandResult fullResult = await session.UseConsumableAsync(hunter.InstanceId, item, HunterBodyPart.Torso);

            Assert.That(foreignResult.Succeeded, Is.False);
            Assert.That(unknownHunterResult.Succeeded, Is.False);
            Assert.That(fullResult.Succeeded, Is.False);
            Assert.That(settlement.GetStoredItem(foreign), Is.EqualTo(1));
            Assert.That(settlement.GetStoredItem(item), Is.Zero);
            Assert.That(hunter.HP.body, Is.EqualTo(hunter.MaxHP.body));
        }

        [Test]
        public async Task UseConsumableAsync_RejectsDeadRetiredAndMissingInventoryWithoutWrites()
        {
            SettlementInstance settlement = CreateSettlement(out HunterInstance hunter, out ItemData item);
            settlement.AddStoredItem(item, 1);
            using var session = CreateSession(settlement, item);

            hunter.IsAlive = false;
            SettlementConsumableCommandResult deadResult = await session.UseConsumableAsync(hunter.InstanceId, item, HunterBodyPart.Torso);
            hunter.IsAlive = true;
            hunter.Availability = HunterAvailabilityState.Retired;
            SettlementConsumableCommandResult retiredResult = await session.UseConsumableAsync(hunter.InstanceId, item, HunterBodyPart.Torso);
            hunter.Availability = HunterAvailabilityState.Active;
            Assert.That(settlement.SpendStoredItem(item, 1), Is.True);
            SettlementConsumableCommandResult emptyResult = await session.UseConsumableAsync(hunter.InstanceId, item, HunterBodyPart.Torso);

            Assert.That(deadResult.Succeeded, Is.False);
            Assert.That(retiredResult.Succeeded, Is.False);
            Assert.That(emptyResult.Succeeded, Is.False);
            Assert.That(hunter.HP.body, Is.EqualTo(hunter.MaxHP.body));
            Assert.That(settlement.GetStoredItem(item), Is.Zero);
        }

        [Test]
        public async Task UseConsumableAsync_CancelledBeforeExecutionLeavesStateUntouched()
        {
            SettlementInstance settlement = CreateSettlement(out HunterInstance hunter, out ItemData item);
            hunter.HP.body = 1;
            settlement.AddStoredItem(item, 1);
            using var session = CreateSession(settlement, item);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            SettlementConsumableCommandResult result = await session.UseConsumableAsync(hunter.InstanceId, item, HunterBodyPart.Torso, cancellation.Token);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(hunter.HP.body, Is.EqualTo(1));
            Assert.That(settlement.GetStoredItem(item), Is.EqualTo(1));
        }

        private SettlementInstance CreateSettlement(out HunterInstance hunter, out ItemData item)
        {
            var settlement = new SettlementInstance();
            hunter = new HunterInstance(null, 501) { Name = "伤员" };
            item = CreateItem("test_poultice");
            settlement.Hunters.Add(hunter);
            return settlement;
        }

        private ItemData CreateItem(string id)
        {
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            item.name = id;
            item.itemName = id;
            item.ConfigureContentId(id);
            item.itemType = ItemType.Consumable;
            item.ConfigureConsumableEffect(ConsumableEffectKind.RecoverBodyPart, 1);
            createdObjects.Add(item);
            return item;
        }

        private static PlayableSettlementActionSession CreateSession(SettlementInstance settlement, ItemData item)
            => new(settlement, EmptyWeaponTrainingContent.Instance, consumableContent: new PlayableSettlementConsumableContentAdapter(new[] { item }));

        private sealed class PreventConsumableReactor : GameActionReactor<UseSettlementConsumableAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            protected override void React(UseSettlementConsumableAction action, ReactionContext context, ReactionResponse response) => response.Prevent("消耗品使用被阻止");
        }

        private sealed class EmptyWeaponTrainingContent : IWeaponTrainingContent
        {
            public static EmptyWeaponTrainingContent Instance { get; } = new();
            public string RequiredInventionId => string.Empty;
            public string CostResourceId => string.Empty;
            public int ResourceCost => 0;
            public int Experience => 1;
            public bool TryGetFamily(string masteryId, out WeaponMasteryFamilyDefinition result) { result = null; return false; }
        }
    }
}
