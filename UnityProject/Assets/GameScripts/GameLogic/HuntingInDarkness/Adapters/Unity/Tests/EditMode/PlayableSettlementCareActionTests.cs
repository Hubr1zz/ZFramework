using System;
using System.Collections.Generic;
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
    public sealed class PlayableSettlementCareActionTests
    {
        [Test]
        public async Task RecruitHunterAsync_SuccessCommitsStateThenPublishesFactsInOrder()
        {
            var settlement = new SettlementInstance { CurrentYear = 3 };
            settlement.Hunters.Add(new HunterInstance(null, 100) { Name = "守火者" });
            settlement.AddResource("口粮", 2);
            HunterData template = CreateTemplate("流浪者");
            var received = new List<string>();
            Action<ResourceChangedEvent> resourceHandler = evt => received.Add($"resource:{evt.OldAmount}>{evt.NewAmount}");
            Action<HunterRecruitedEvent> recruitedHandler = evt => received.Add($"recruited:{evt.HunterName}");
            Action<HunterRosterChangedEvent> rosterHandler = _ => received.Add("roster");
            Action<SettlementTransactionCommittedEvent> commitHandler = evt => received.Add($"commit:{evt.Kind}");
            EventBus.Subscribe(resourceHandler);
            EventBus.Subscribe(recruitedHandler);
            EventBus.Subscribe(rosterHandler);
            EventBus.Subscribe(commitHandler);
            try
            {
                using var session = CreateSession(settlement, template, recruitmentCost: 1);

                RecruitHunterCommandResult result = await session.RecruitHunterAsync(template, "  余烬  ");

                Assert.That(result.Succeeded, Is.True, result.Reason);
                Assert.That(result.Hunter.Name, Is.EqualTo("余烬"));
                Assert.That(result.Hunter.InstanceId, Is.EqualTo(101));
                Assert.That(settlement.GetResource("口粮"), Is.EqualTo(1));
                Assert.That(settlement.LastRecruitmentYear, Is.EqualTo(3));
                Assert.That(settlement.Timeline[^1].EntryType, Is.EqualTo(TimelineEntryType.PlayerAdded));
                Assert.That(received, Is.EqualTo(new[] { "resource:2>1", "recruited:余烬", "roster", "commit:Recruitment" }));
            }
            finally
            {
                EventBus.Unsubscribe(resourceHandler);
                EventBus.Unsubscribe(recruitedHandler);
                EventBus.Unsubscribe(rosterHandler);
                EventBus.Unsubscribe(commitHandler);
                UnityEngine.Object.DestroyImmediate(template);
            }
        }

        [Test]
        public async Task RecruitHunterAsync_ReactorCanOverrideCostAndCapacity()
        {
            var settlement = new SettlementInstance { CurrentYear = 2 };
            settlement.Hunters.Add(new HunterInstance(null, 100) { Name = "守火者" });
            HunterData template = CreateTemplate("流浪者");
            try
            {
                using var session = CreateSession(settlement, template, recruitmentCost: 3, maximumLivingHunters: 1);
                session.Reactors.RegisterGlobal(new RecruitmentTermsReactor(0, 2));

                RecruitHunterCommandResult result = await session.RecruitHunterAsync(template, "新火");

                Assert.That(result.Succeeded, Is.True, result.Reason);
                Assert.That(settlement.GetAvailableHunters(), Has.Count.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(template);
            }
        }

        [Test]
        public async Task RecruitHunterAsync_RetiredLivingHunterStillCountsTowardCapacity()
        {
            var settlement = new SettlementInstance { CurrentYear = 2 };
            settlement.Hunters.Add(new HunterInstance(null, 100) { Name = "守火者" });
            settlement.Hunters.Add(new HunterInstance(null, 101) { Name = "退休者", Availability = HunterAvailabilityState.Retired });
            settlement.AddResource("口粮", 1);
            HunterData template = CreateTemplate("流浪者");
            try
            {
                using var session = CreateSession(settlement, template, recruitmentCost: 1, maximumLivingHunters: 2);

                Assert.That(session.CanRecruit(out string reason), Is.False);
                Assert.That(reason, Does.Contain("没有容纳"));
                RecruitHunterCommandResult result = await session.RecruitHunterAsync(template, "越界者");

                Assert.That(result.Succeeded, Is.False);
                Assert.That(settlement.Hunters, Has.Count.EqualTo(2));
                Assert.That(settlement.GetResource("口粮"), Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(template);
            }
        }

        [Test]
        public async Task RecruitHunterAsync_ForeignTemplateDoesNotSpendOrChangeRoster()
        {
            var settlement = new SettlementInstance { CurrentYear = 2 };
            settlement.Hunters.Add(new HunterInstance(null, 100) { Name = "守火者" });
            settlement.AddResource("口粮", 1);
            HunterData allowed = CreateTemplate("获准模板");
            HunterData foreign = CreateTemplate("外来模板");
            try
            {
                using var session = CreateSession(settlement, allowed, recruitmentCost: 1);

                RecruitHunterCommandResult result = await session.RecruitHunterAsync(foreign, "越界者");

                Assert.That(result.Succeeded, Is.False);
                Assert.That(settlement.GetResource("口粮"), Is.EqualTo(1));
                Assert.That(settlement.Hunters, Has.Count.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(foreign);
                UnityEngine.Object.DestroyImmediate(allowed);
            }
        }

        [Test]
        public async Task RecruitHunterAsync_ControlCharacterNameDoesNotSpendOrChangeRoster()
        {
            var settlement = new SettlementInstance { CurrentYear = 2 };
            settlement.Hunters.Add(new HunterInstance(null, 100) { Name = "守火者" });
            settlement.AddResource("口粮", 1);
            HunterData template = CreateTemplate("流浪者");
            try
            {
                using var session = CreateSession(settlement, template, recruitmentCost: 1);

                RecruitHunterCommandResult result = await session.RecruitHunterAsync(template, "伪装者\n第二行");

                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Reason, Does.Contain("控制字符"));
                Assert.That(settlement.GetResource("口粮"), Is.EqualTo(1));
                Assert.That(settlement.Hunters, Has.Count.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(template);
            }
        }

        [Test]
        public async Task RecruitHunterAsync_ConcurrentRequestsCommitOnlyFirst()
        {
            var settlement = new SettlementInstance { CurrentYear = 5 };
            settlement.Hunters.Add(new HunterInstance(null, 100) { Name = "守火者" });
            settlement.AddResource("口粮", 2);
            HunterData template = CreateTemplate("流浪者");
            try
            {
                using var session = CreateSession(settlement, template, recruitmentCost: 1);
                UniTask<RecruitHunterCommandResult> firstTask = session.RecruitHunterAsync(template, "甲");
                UniTask<RecruitHunterCommandResult> secondTask = session.RecruitHunterAsync(template, "乙");

                (RecruitHunterCommandResult first, RecruitHunterCommandResult second) = await UniTask.WhenAll(firstTask, secondTask);

                Assert.That(first.Succeeded, Is.True);
                Assert.That(second.Succeeded, Is.False);
                Assert.That(settlement.GetResource("口粮"), Is.EqualTo(1));
                Assert.That(settlement.GetAvailableHunters(), Has.Count.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(template);
            }
        }

        [Test]
        public async Task RecoverHunterAsync_SuccessPublishesResourceRecoveryAndCommit()
        {
            var settlement = new SettlementInstance();
            var hunter = new HunterInstance(null, 201) { Name = "伤员" };
            hunter.HP.body = 1;
            settlement.Hunters.Add(hunter);
            settlement.AddResource("口粮", 2);
            var received = new List<string>();
            Action<ResourceChangedEvent> resourceHandler = evt => received.Add($"resource:{evt.OldAmount}>{evt.NewAmount}");
            Action<HunterRecoveredEvent> recoveredHandler = evt => received.Add($"recovered:{evt.PreviousHealth}>{evt.CurrentHealth}");
            Action<SettlementTransactionCommittedEvent> commitHandler = evt => received.Add($"commit:{evt.Kind}");
            EventBus.Subscribe(resourceHandler);
            EventBus.Subscribe(recoveredHandler);
            EventBus.Subscribe(commitHandler);
            try
            {
                using var session = CreateSession(settlement, recoveryCost: 1, recoveryAmount: 1);

                RecoverHunterCommandResult result = await session.RecoverHunterAsync(hunter.InstanceId, HunterBodyPart.Torso);

                Assert.That(result.Succeeded, Is.True, result.Reason);
                Assert.That(hunter.HP.body, Is.EqualTo(2));
                Assert.That(settlement.GetResource("口粮"), Is.EqualTo(1));
                Assert.That(received, Is.EqualTo(new[] { "resource:2>1", "recovered:1>2", "commit:Recovery" }));
            }
            finally
            {
                EventBus.Unsubscribe(resourceHandler);
                EventBus.Unsubscribe(recoveredHandler);
                EventBus.Unsubscribe(commitHandler);
            }
        }

        [Test]
        public async Task RecoverHunterAsync_PreventedActionLeavesHealthAndResourceUntouched()
        {
            var settlement = new SettlementInstance();
            var hunter = new HunterInstance(null, 202) { Name = "伤员" };
            hunter.HP.head = 0;
            settlement.Hunters.Add(hunter);
            settlement.AddResource("口粮", 1);
            int commitCount = 0;
            Action<SettlementTransactionCommittedEvent> handler = _ => commitCount++;
            EventBus.Subscribe(handler);
            try
            {
                using var session = CreateSession(settlement, recoveryCost: 1, recoveryAmount: 1);
                session.Reactors.RegisterGlobal(new PreventRecoveryReactor());

                RecoverHunterCommandResult result = await session.RecoverHunterAsync(hunter.InstanceId, HunterBodyPart.Head);

                Assert.That(result.Succeeded, Is.False);
                Assert.That(hunter.HP.head, Is.Zero);
                Assert.That(settlement.GetResource("口粮"), Is.EqualTo(1));
                Assert.That(commitCount, Is.Zero);
            }
            finally
            {
                EventBus.Unsubscribe(handler);
            }
        }

        [Test]
        public async Task RecoverHunterAsync_ReactorCanMakeTreatmentFreeAndIncreaseRecovery()
        {
            var settlement = new SettlementInstance();
            var hunter = new HunterInstance(null, 205) { Name = "伤员" };
            hunter.HP.body = 0;
            settlement.Hunters.Add(hunter);
            using var session = CreateSession(settlement, recoveryCost: 3, recoveryAmount: 1);
            session.Reactors.RegisterGlobal(new RecoveryTermsReactor(0, 3));

            RecoverHunterCommandResult result = await session.RecoverHunterAsync(hunter.InstanceId, HunterBodyPart.Torso);

            Assert.That(result.Succeeded, Is.True, result.Reason);
            Assert.That(result.Recovery.RecoveredHealth, Is.EqualTo(3));
            Assert.That(hunter.HP.body, Is.EqualTo(3));
        }

        [Test]
        public async Task RecoverHunterAsync_RepeatedRequestDoesNotSpendAfterFullyHealed()
        {
            var settlement = new SettlementInstance();
            var hunter = new HunterInstance(null, 203) { Name = "伤员" };
            hunter.HP.head = hunter.MaxHP.head - 1;
            settlement.Hunters.Add(hunter);
            settlement.AddResource("口粮", 2);
            using var session = CreateSession(settlement, recoveryCost: 1, recoveryAmount: 5);

            RecoverHunterCommandResult first = await session.RecoverHunterAsync(hunter.InstanceId, HunterBodyPart.Head);
            RecoverHunterCommandResult second = await session.RecoverHunterAsync(hunter.InstanceId, HunterBodyPart.Head);

            Assert.That(first.Succeeded, Is.True);
            Assert.That(second.Succeeded, Is.False);
            Assert.That(settlement.GetResource("口粮"), Is.EqualTo(1));
            Assert.That(hunter.HP.head, Is.EqualTo(hunter.MaxHP.head));
        }

        [Test]
        public async Task RecoverHunterAsync_InvalidBodyPartDoesNotSpendOrChangeHealth()
        {
            var settlement = new SettlementInstance();
            var hunter = new HunterInstance(null, 204) { Name = "伤员" };
            hunter.HP.body = 1;
            settlement.Hunters.Add(hunter);
            settlement.AddResource("口粮", 1);
            using var session = CreateSession(settlement, recoveryCost: 1, recoveryAmount: 1);

            RecoverHunterCommandResult result = await session.RecoverHunterAsync(hunter.InstanceId, (HunterBodyPart)999);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Reason, Does.Contain("未知"));
            Assert.That(settlement.GetResource("口粮"), Is.EqualTo(1));
            Assert.That(hunter.HP.body, Is.EqualTo(1));
        }

        private static PlayableSettlementActionSession CreateSession(SettlementInstance settlement, HunterData template = null, int recruitmentCost = 0, int maximumLivingHunters = 6, int recoveryCost = 0, int recoveryAmount = 1)
        {
            var content = new TestCareContent(template, recruitmentCost, maximumLivingHunters, recoveryCost, recoveryAmount);
            return new PlayableSettlementActionSession(settlement, new TestWeaponTrainingContent(), careContent: content);
        }

        private static HunterData CreateTemplate(string name)
        {
            HunterData template = ScriptableObject.CreateInstance<HunterData>();
            template.name = name;
            template.hunterName = name;
            return template;
        }

        private sealed class TestCareContent : ISettlementCareContent
        {
            public TestCareContent(HunterData template, int recruitmentCost, int maximumLivingHunters, int recoveryCost, int recoveryAmount)
            {
                RecruitmentTemplates = template != null ? new[] { template } : Array.Empty<HunterData>();
                RecruitmentCost = recruitmentCost;
                MaximumLivingHunters = maximumLivingHunters;
                RecoveryCost = recoveryCost;
                RecoveryAmount = recoveryAmount;
            }

            public IReadOnlyList<HunterData> RecruitmentTemplates { get; }
            public string RecruitmentCostResourceId => "口粮";
            public int RecruitmentCost { get; }
            public int MaximumLivingHunters { get; }
            public string RecoveryCostResourceId => "口粮";
            public int RecoveryCost { get; }
            public int RecoveryAmount { get; }
        }

        private sealed class TestWeaponTrainingContent : IWeaponTrainingContent
        {
            public string RequiredInventionId => string.Empty;
            public string CostResourceId => string.Empty;
            public int ResourceCost => 0;
            public int Experience => 1;
            public bool TryGetFamily(string masteryId, out WeaponMasteryFamilyDefinition result)
            {
                result = null;
                return false;
            }
        }

        private sealed class RecruitmentTermsReactor : GameActionReactor<RecruitHunterAction>
        {
            private readonly int resourceCost;
            private readonly int maximumLivingHunters;

            public RecruitmentTermsReactor(int resourceCost, int maximumLivingHunters)
            {
                this.resourceCost = resourceCost;
                this.maximumLivingHunters = maximumLivingHunters;
            }

            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;

            protected override void React(RecruitHunterAction action, ReactionContext context, ReactionResponse response)
            {
                action.SetResourceCost(resourceCost);
                action.SetMaximumLivingHunters(maximumLivingHunters);
            }
        }

        private sealed class PreventRecoveryReactor : GameActionReactor<RecoverHunterAction>
        {
            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;

            protected override void React(RecoverHunterAction action, ReactionContext context, ReactionResponse response)
            {
                response.Prevent("休养被营地效果阻止");
            }
        }

        private sealed class RecoveryTermsReactor : GameActionReactor<RecoverHunterAction>
        {
            private readonly int resourceCost;
            private readonly int recoveryAmount;

            public RecoveryTermsReactor(int resourceCost, int recoveryAmount)
            {
                this.resourceCost = resourceCost;
                this.recoveryAmount = recoveryAmount;
            }

            public override ReactionTiming Timing => ReactionTiming.BeforeExecution;

            protected override void React(RecoverHunterAction action, ReactionContext context, ReactionResponse response)
            {
                action.SetResourceCost(resourceCost);
                action.SetRecoveryAmount(recoveryAmount);
            }
        }
    }
}
