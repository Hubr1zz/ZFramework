using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CardGame.ActionQueue;
using Core;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using NUnit.Framework;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableSettlementActionSessionTests
    {
        [Test]
        public async Task TrainWeaponAsync_SuccessCommitsStateThenPublishesFactsInOrder()
        {
            SettlementInstance settlement = CreateSettlement(resourceAmount: 2);
            HunterInstance hunter = settlement.Hunters[0];
            var received = new List<string>();
            Action<ResourceChangedEvent> resourceHandler = evt => received.Add($"resource:{evt.OldAmount}>{evt.NewAmount}");
            Action<WeaponMasteryChangedEvent> masteryHandler = evt => received.Add($"mastery:{evt.OldValue}>{evt.NewValue}");
            Action<SettlementTransactionCommittedEvent> committedHandler = evt => received.Add($"committed:{evt.Kind}");
            EventBus.Subscribe(resourceHandler);
            EventBus.Subscribe(masteryHandler);
            EventBus.Subscribe(committedHandler);
            try
            {
                using var session = new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent());

                WeaponTrainingCommandResult result = await session.TrainWeaponAsync(hunter.InstanceId, "mastery_blade");

                Assert.That(result.Success, Is.True);
                Assert.That(result.MasteryOutcome.NewValue, Is.EqualTo(1));
                Assert.That(settlement.GetResource("碎石"), Is.EqualTo(1));
                Assert.That(received, Is.EqualTo(new[] { "resource:2>1", "mastery:0>1", "committed:WeaponTraining" }));
            }
            finally
            {
                EventBus.Unsubscribe(resourceHandler);
                EventBus.Unsubscribe(masteryHandler);
                EventBus.Unsubscribe(committedHandler);
            }
        }

        [Test]
        public async Task TrainWeaponAsync_InsufficientResourceDoesNotPartiallyCommit()
        {
            SettlementInstance settlement = CreateSettlement(resourceAmount: 0);
            HunterInstance hunter = settlement.Hunters[0];
            int committedCount = 0;
            Action<SettlementTransactionCommittedEvent> handler = _ => committedCount++;
            EventBus.Subscribe(handler);
            try
            {
                using var session = new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent());

                WeaponTrainingCommandResult result = await session.TrainWeaponAsync(hunter.InstanceId, "mastery_blade");

                Assert.That(result.Success, Is.False);
                Assert.That(result.Reason, Is.EqualTo("训练资源不足"));
                Assert.That(settlement.GetResource("碎石"), Is.Zero);
                Assert.That(hunter.WeaponMasteries, Is.Empty);
                Assert.That(committedCount, Is.Zero);
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task BeforeReactor_CanOverrideThisTrainingTerms()
        {
            SettlementInstance settlement = CreateSettlement(resourceAmount: 1);
            HunterInstance hunter = settlement.Hunters[0];
            using var session = new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent());
            session.Reactors.RegisterGlobal(new TrainingTermsReactor(resourceCost: 0, experience: 3));

            WeaponTrainingCommandResult result = await session.TrainWeaponAsync(hunter.InstanceId, "mastery_blade");

            Assert.That(result.Success, Is.True);
            Assert.That(result.MasteryOutcome.NewValue, Is.EqualTo(3));
            Assert.That(settlement.GetResource("碎石"), Is.EqualTo(1));
        }

        [Test]
        public async Task BeforeReactor_PreventionLeavesStateAndEventsUntouched()
        {
            SettlementInstance settlement = CreateSettlement(resourceAmount: 1);
            HunterInstance hunter = settlement.Hunters[0];
            int committedCount = 0;
            Action<SettlementTransactionCommittedEvent> handler = _ => committedCount++;
            EventBus.Subscribe(handler);
            try
            {
                using var session = new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent());
                session.Reactors.RegisterGlobal(new PreventTrainingReactor());

                WeaponTrainingCommandResult result = await session.TrainWeaponAsync(hunter.InstanceId, "mastery_blade");

                Assert.That(result.Success, Is.False);
                Assert.That(result.Reason, Is.EqualTo("训练被营地规则阻止"));
                Assert.That(settlement.GetResource("碎石"), Is.EqualTo(1));
                Assert.That(hunter.WeaponMasteries, Is.Empty);
                Assert.That(committedCount, Is.Zero);
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task TrainWeaponAsync_ForeignHunterIdIsRejected()
        {
            SettlementInstance settlement = CreateSettlement(resourceAmount: 1);
            using var session = new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent());

            WeaponTrainingCommandResult result = await session.TrainWeaponAsync(999, "mastery_blade");

            Assert.That(result.Success, Is.False);
            Assert.That(settlement.GetResource("碎石"), Is.EqualTo(1));
        }

        [Test]
        public async Task TrainWeaponAsync_MaximumMasteryDoesNotSpendOrPublish()
        {
            SettlementInstance settlement = CreateSettlement(resourceAmount: 1);
            HunterInstance hunter = settlement.Hunters[0];
            hunter.WeaponMasteries.Add(new WeaponMasteryState { MasteryId = "mastery_blade", DisplayName = "刃术", Experience = int.MaxValue });
            int committedCount = 0;
            Action<SettlementTransactionCommittedEvent> handler = _ => committedCount++;
            EventBus.Subscribe(handler);
            try
            {
                using var session = new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent());

                WeaponTrainingCommandResult result = await session.TrainWeaponAsync(hunter.InstanceId, "mastery_blade");

                Assert.That(result.Success, Is.False);
                Assert.That(result.Reason, Is.EqualTo("熟练度已达到上限"));
                Assert.That(settlement.GetResource("碎石"), Is.EqualTo(1));
                Assert.That(committedCount, Is.Zero);
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        private static SettlementInstance CreateSettlement(int resourceAmount)
        {
            var settlement = new SettlementInstance();
            settlement.Hunters.Add(new HunterInstance(null, 7) { Name = "训练者" });
            settlement.UnlockInvention("武器训练");
            settlement.AddResource("碎石", resourceAmount);
            return settlement;
        }

        private sealed class TestWeaponTrainingContent : IWeaponTrainingContent
        {
            private readonly WeaponMasteryFamilyDefinition family = new("mastery_blade", "刃术", Array.Empty<WeaponMasteryMilestoneDefinition>());

            public string RequiredInventionId => "武器训练";
            public string CostResourceId => "碎石";
            public int ResourceCost => 1;
            public int Experience => 1;

            public bool TryGetFamily(string masteryId, out WeaponMasteryFamilyDefinition result)
            {
                result = string.Equals(masteryId, family.Id, StringComparison.Ordinal) ? family : null;
                return result != null;
            }
        }

        private sealed class TrainingTermsReactor : GameActionReactor<TrainWeaponAction>
        {
            private readonly int resourceCost;
            private readonly int experience;

            public TrainingTermsReactor(int resourceCost, int experience)
            {
                this.resourceCost = resourceCost;
                this.experience = experience;
            }

            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;

            protected override void React(TrainWeaponAction action, ReactionContext context, ReactionResponse response)
            {
                action.SetResourceCost(resourceCost);
                action.SetExperience(experience);
            }
        }

        private sealed class PreventTrainingReactor : GameActionReactor<TrainWeaponAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;

            protected override void React(TrainWeaponAction action, ReactionContext context, ReactionResponse response)
            {
                response.Prevent("训练被营地规则阻止");
            }
        }
    }
}
