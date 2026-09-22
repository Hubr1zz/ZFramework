using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableSettlementInventionActionTests
    {
        [Test]
        public async Task UnlockInventionAsync_SuccessCommitsResourcesUnlockAndFacts()
        {
            TestContext context = CreateContext(2);
            var received = new List<string>();
            Action<ResourceChangedEvent> resourceHandler = evt => received.Add($"resource:{evt.OldAmount}>{evt.NewAmount}");
            Action<SettlementInventionUnlockedEvent> inventionHandler = evt => received.Add($"invention:{evt.InventionId}:{evt.DisplayName}");
            Action<SettlementTransactionCommittedEvent> transactionHandler = evt => received.Add($"transaction:{evt.Kind}");
            EventBus.Subscribe(resourceHandler);
            EventBus.Subscribe(inventionHandler);
            EventBus.Subscribe(transactionHandler);
            try
            {
                using var session = new PlayableSettlementActionSession(context.Settlement, new EmptyWeaponTrainingContent(), inventionSystem: context.System);

                SettlementInventionCommandResult result = await session.UnlockInventionAsync(context.Invention);

                Assert.That(result.Succeeded, Is.True);
                Assert.That(result.InventionId, Is.EqualTo("stonecraft"));
                Assert.That(result.DisplayName, Is.EqualTo("石工"));
                Assert.That(context.Settlement.GetResource("碎石"), Is.Zero);
                Assert.That(context.System.IsUnlocked(context.Invention), Is.True);
                Assert.That(context.Settlement.Timeline, Has.Count.EqualTo(1));
                Assert.That(context.Settlement.Timeline[0].EventId, Is.EqualTo("invention:stonecraft"));
                Assert.That(context.Settlement.Timeline[0].EventName, Is.EqualTo("石工"));
                Assert.That(context.Settlement.Timeline[0].EntryType, Is.EqualTo(TimelineEntryType.Invention));
                Assert.That(context.Settlement.Timeline[0].IsCompleted, Is.True);
                Assert.That(received, Is.EqualTo(new[] { "resource:2>0", "invention:stonecraft:石工", "transaction:Invention" }));
            }
            finally
            {
                EventBus.Unsubscribe(resourceHandler);
                EventBus.Unsubscribe(inventionHandler);
                EventBus.Unsubscribe(transactionHandler);
                context.Dispose();
            }
        }

        [Test]
        public async Task UnlockInventionAsync_ConcurrentRequestsSpendOnlyOnce()
        {
            TestContext context = CreateContext(2);
            try
            {
                using var session = new PlayableSettlementActionSession(context.Settlement, new EmptyWeaponTrainingContent(), inventionSystem: context.System);

                Task<SettlementInventionCommandResult> first = session.UnlockInventionAsync(context.Invention).AsTask();
                Task<SettlementInventionCommandResult> second = session.UnlockInventionAsync(context.Invention).AsTask();
                SettlementInventionCommandResult[] results = await Task.WhenAll(first, second);

                Assert.That(Array.FindAll(results, result => result.Succeeded).Length, Is.EqualTo(1));
                Assert.That(context.Settlement.GetResource("碎石"), Is.Zero);
                Assert.That(context.Settlement.Timeline, Has.Count.EqualTo(1));
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public async Task UnlockInventionAsync_PreventedReactorLeavesStateUntouched()
        {
            TestContext context = CreateContext(2);
            try
            {
                using var session = new PlayableSettlementActionSession(context.Settlement, new EmptyWeaponTrainingContent(), inventionSystem: context.System);
                session.Reactors.RegisterGlobal(new PreventInventionReactor());

                SettlementInventionCommandResult result = await session.UnlockInventionAsync(context.Invention);

                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Reason, Is.EqualTo("测试规则阻止发明"));
                Assert.That(context.Settlement.GetResource("碎石"), Is.EqualTo(2));
                Assert.That(context.System.IsUnlocked(context.Invention), Is.False);
                Assert.That(context.Settlement.Timeline, Is.Empty);
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public async Task UnlockInventionAsync_ForeignInventionCannotMutateSettlement()
        {
            TestContext context = CreateContext(2);
            InventionData foreign = ScriptableObject.CreateInstance<InventionData>();
            foreign.ConfigureContentId("foreign_invention");
            foreign.inventionName = "外来发明";
            try
            {
                using var session = new PlayableSettlementActionSession(context.Settlement, new EmptyWeaponTrainingContent(), inventionSystem: context.System);

                SettlementInventionCommandResult result = await session.UnlockInventionAsync(foreign);

                Assert.That(result.Succeeded, Is.False);
                Assert.That(context.Settlement.GetResource("碎石"), Is.EqualTo(2));
                Assert.That(context.Settlement.IsInventionUnlocked(foreign.ContentId), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(foreign);
                context.Dispose();
            }
        }

        [Test]
        public async Task UnlockInventionAsync_StructuredEffectUsesPerHunterReactorBoundary()
        {
            TestContext context = CreateContext(2);
            var first = new HunterInstance(null, 11) { Name = "甲", Willpower = 2, WillpowerMax = 2 };
            var second = new HunterInstance(null, 12) { Name = "乙", Willpower = 2, WillpowerMax = 2 };
            context.Settlement.Hunters.Add(second);
            context.Settlement.Hunters.Add(first);
            context.Invention.unlockEffects.Add(new InventionPassiveEffect { kind = InventionEffectKind.ModifyWillpowerMaximum, target = InventionEffectTarget.AvailableHunters, value = 1 });
            try
            {
                using var session = new PlayableSettlementActionSession(context.Settlement, new EmptyWeaponTrainingContent(), inventionSystem: context.System);
                var reactor = new ModifyAndPreventHunterEffectReactor(11, 12);
                session.Reactors.RegisterGlobal(reactor);

                SettlementInventionCommandResult result = await session.UnlockInventionAsync(context.Invention);

                Assert.That(result.Succeeded, Is.True, result.Reason);
                Assert.That(first.WillpowerMax, Is.EqualTo(4));
                Assert.That(second.WillpowerMax, Is.EqualTo(2));
                Assert.That(reactor.ObservedHunterIds, Is.EqualTo(new[] { 11, 12 }));
                Assert.That(context.System.IsUnlocked(context.Invention), Is.True);
                Assert.That(context.Settlement.GetResource("碎石"), Is.Zero);
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public async Task UnlockInventionAsync_StructuredEffectSkipsIneligibleHunterAtExecution()
        {
            TestContext context = CreateContext(2);
            var first = new HunterInstance(null, 21) { Name = "先执行者", Willpower = 2, WillpowerMax = 2 };
            var retiredDuringChain = new HunterInstance(null, 22) { Name = "链中退役者", Willpower = 2, WillpowerMax = 2 };
            context.Settlement.Hunters.Add(first);
            context.Settlement.Hunters.Add(retiredDuringChain);
            context.Invention.unlockEffects.Add(new InventionPassiveEffect { kind = InventionEffectKind.ModifyWillpowerMaximum, target = InventionEffectTarget.AvailableHunters, value = 1 });
            try
            {
                using var session = new PlayableSettlementActionSession(context.Settlement, new EmptyWeaponTrainingContent(), inventionSystem: context.System);
                session.Reactors.RegisterGlobal(new RetireNextHunterReactor(21, retiredDuringChain));

                SettlementInventionCommandResult result = await session.UnlockInventionAsync(context.Invention);

                Assert.That(result.Succeeded, Is.True, result.Reason);
                Assert.That(first.WillpowerMax, Is.EqualTo(3));
                Assert.That(retiredDuringChain.WillpowerMax, Is.EqualTo(2));
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public async Task UnlockInventionAsync_CampaignEffectPersistsReactorValueForFutureHunter()
        {
            TestContext context = CreateContext(2);
            var existing = new HunterInstance(null, 31) { WillpowerMax = 2, Willpower = 2 };
            context.Settlement.Hunters.Add(existing);
            context.Invention.unlockEffects.Add(new InventionPassiveEffect { lifetime = InventionEffectLifetime.Campaign, modifierId = "stonecraft:willpower", kind = InventionEffectKind.ModifyWillpowerMaximum, target = InventionEffectTarget.AllLivingAndFutureHunters, value = 1 });
            try
            {
                using var session = new PlayableSettlementActionSession(context.Settlement, new EmptyWeaponTrainingContent(), inventionSystem: context.System);
                session.Reactors.RegisterGlobal(new DoubleCampaignModifierReactor());

                SettlementInventionCommandResult result = await session.UnlockInventionAsync(context.Invention);
                var future = new HunterInstance(null, 32) { WillpowerMax = 2, Willpower = 2 };

                Assert.That(result.Succeeded, Is.True, result.Reason);
                Assert.That(existing.WillpowerMax, Is.EqualTo(4));
                Assert.That(context.Settlement.ActiveModifiers[0].Value, Is.EqualTo(2));
                Assert.That(context.Settlement.ActiveModifiers[0].HasValueOverride, Is.True);
                Assert.That(PlayableSettlementModifierRuntime.TryReconcileHunter(context.Settlement, future, out string reason), Is.True, reason);
                Assert.That(future.WillpowerMax, Is.EqualTo(4));
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public async Task UnlockInventionAsync_PreventedCampaignEffectLeavesTransactionUntouched()
        {
            TestContext context = CreateContext(2);
            context.Invention.unlockEffects.Add(new InventionPassiveEffect { lifetime = InventionEffectLifetime.Campaign, modifierId = "stonecraft:willpower", kind = InventionEffectKind.ModifyWillpowerMaximum, target = InventionEffectTarget.AllLivingAndFutureHunters, value = 1 });
            try
            {
                using var session = new PlayableSettlementActionSession(context.Settlement, new EmptyWeaponTrainingContent(), inventionSystem: context.System);
                session.Reactors.RegisterGlobal(new PreventCampaignModifierReactor());

                SettlementInventionCommandResult result = await session.UnlockInventionAsync(context.Invention);

                Assert.That(result.Succeeded, Is.False);
                Assert.That(context.Settlement.GetResource("碎石"), Is.EqualTo(2));
                Assert.That(context.Settlement.IsInventionUnlocked("stonecraft"), Is.False);
                Assert.That(context.Settlement.ActiveModifiers, Is.Empty);
                Assert.That(context.Settlement.Timeline, Is.Empty);
            }
            finally
            {
                context.Dispose();
            }
        }

        private static TestContext CreateContext(int resourceAmount)
        {
            var settlement = new SettlementInstance();
            settlement.AddResource("碎石", resourceAmount);
            ItemData resource = ScriptableObject.CreateInstance<ItemData>();
            resource.itemName = "碎石";
            resource.itemType = ItemType.Resource;
            InventionData invention = ScriptableObject.CreateInstance<InventionData>();
            invention.name = "stonecraft";
            invention.ConfigureContentId("stonecraft");
            invention.inventionName = "石工";
            invention.costs.Add(new InventionCost { resource = resource, count = 1 });
            invention.costs.Add(new InventionCost { resource = resource, count = 1 });
            var system = new InventionSystem(settlement);
            system.AllInventions.Add(invention);
            return new TestContext(settlement, system, invention, resource);
        }

        private sealed class TestContext : IDisposable
        {
            private readonly ItemData resource;

            public TestContext(SettlementInstance settlement, InventionSystem system, InventionData invention, ItemData resource)
            {
                Settlement = settlement;
                System = system;
                Invention = invention;
                this.resource = resource;
            }

            public SettlementInstance Settlement { get; }
            public InventionSystem System { get; }
            public InventionData Invention { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Invention);
                UnityEngine.Object.DestroyImmediate(resource);
            }
        }

        private sealed class PreventInventionReactor : GameActionReactor<UnlockSettlementInventionAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            protected override void React(UnlockSettlementInventionAction action, ReactionContext context, ReactionResponse response) => response.Prevent("测试规则阻止发明");
        }

        private sealed class DoubleCampaignModifierReactor : GameActionReactor<PrepareSettlementInventionModifierAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            protected override void React(PrepareSettlementInventionModifierAction action, ReactionContext context, ReactionResponse response) => action.SetValue(2);
        }

        private sealed class PreventCampaignModifierReactor : GameActionReactor<PrepareSettlementInventionModifierAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            protected override void React(PrepareSettlementInventionModifierAction action, ReactionContext context, ReactionResponse response) => response.Prevent("测试阻止持续效果");
        }

        private sealed class ModifyAndPreventHunterEffectReactor : GameActionReactor<ApplySettlementInventionEffectAction>
        {
            private readonly int modifiedHunterId;
            private readonly int preventedHunterId;

            public ModifyAndPreventHunterEffectReactor(int modifiedHunterId, int preventedHunterId)
            {
                this.modifiedHunterId = modifiedHunterId;
                this.preventedHunterId = preventedHunterId;
            }

            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
            public List<int> ObservedHunterIds { get; } = new();

            protected override void React(ApplySettlementInventionEffectAction action, ReactionContext context, ReactionResponse response)
            {
                ObservedHunterIds.Add(action.Hunter.InstanceId);
                if (action.Hunter.InstanceId == modifiedHunterId) action.SetValue(2);
                if (action.Hunter.InstanceId == preventedHunterId) response.Prevent("测试角色免疫发明效果");
            }
        }

        private sealed class RetireNextHunterReactor : GameActionReactor<ApplySettlementInventionEffectAction>
        {
            private readonly int triggerHunterId;
            private readonly HunterInstance hunterToRetire;

            public RetireNextHunterReactor(int triggerHunterId, HunterInstance hunterToRetire)
            {
                this.triggerHunterId = triggerHunterId;
                this.hunterToRetire = hunterToRetire;
            }

            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;

            protected override void React(ApplySettlementInventionEffectAction action, ReactionContext context, ReactionResponse response)
            {
                if (action.Hunter.InstanceId == triggerHunterId) hunterToRetire.Availability = HunterAvailabilityState.Retired;
            }
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
