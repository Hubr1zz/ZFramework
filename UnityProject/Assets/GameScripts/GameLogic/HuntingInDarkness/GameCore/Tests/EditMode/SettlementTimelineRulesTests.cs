using HuntingInDarkness.GameCore.Settlement;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests
{
    public sealed class SettlementTimelineRulesTests
    {
        [Test]
        public void IsAvailableForYear_AcceptsConfiguredSingleYear()
        {
            Assert.That(SettlementTimelineRules.IsAvailableForYear(2, 2, 2), Is.True);
            Assert.That(SettlementTimelineRules.IsAvailableForYear(3, 2, 2), Is.False);
        }

        [Test]
        public void IsAvailableForYear_ZeroMaximumHasNoUpperBound()
        {
            Assert.That(SettlementTimelineRules.IsAvailableForYear(99, 2, 0), Is.True);
        }

        [Test]
        public void CompleteHunt_ResetsProgressOnlyWhenQuotaIsReached()
        {
            SettlementTimelineRules.HuntProgress first = SettlementTimelineRules.CompleteHunt(0, 2);
            SettlementTimelineRules.HuntProgress second = SettlementTimelineRules.CompleteHunt(first.HuntsCompletedThisYear, 2);

            Assert.That(first.HuntsCompletedThisYear, Is.EqualTo(1));
            Assert.That(first.ShouldAdvanceYear, Is.False);
            Assert.That(second.HuntsCompletedThisYear, Is.Zero);
            Assert.That(second.ShouldAdvanceYear, Is.True);
        }

        [Test]
        public void AddResourceEffect_ReportsBeforeAndAfterAmounts()
        {
            int amount = 1;

            SettlementEffectOutcome outcome = SettlementEffectRules.Apply(SettlementEffectKind.AddResource, "蘑菇肉", 1, null, null, new HunterState[0], _ => amount, (_, value) => amount += value, (_, _) => false, _ => { });

            Assert.That(outcome.Handled, Is.True);
            Assert.That(outcome.ResourceChanged, Is.True);
            Assert.That(outcome.OldAmount, Is.EqualTo(1));
            Assert.That(outcome.NewAmount, Is.EqualTo(2));
            Assert.That(amount, Is.EqualTo(2));
        }

        [Test]
        public void RemoveResourceEffect_ReportsFailureWhenResourceIsInsufficient()
        {
            int amount = 1;

            SettlementEffectOutcome outcome = SettlementEffectRules.Apply(SettlementEffectKind.RemoveResource, "蘑菇肉", 2, null, null, new HunterState[0], _ => amount, (_, _) => { }, (_, _) => false, _ => { });

            Assert.That(outcome.Handled, Is.False);
            Assert.That(outcome.Reason, Does.Contain("资源不足"));
            Assert.That(amount, Is.EqualTo(1));
        }

        [Test]
        public void HunterEffect_ReportsFailureWhenConfiguredTargetDoesNotExist()
        {
            SettlementEffectOutcome outcome = SettlementEffectRules.Apply(SettlementEffectKind.AddCourage, "selected", 1, null, null, new HunterState[0], _ => 0, (_, _) => { }, (_, _) => false, _ => { });

            Assert.That(outcome.Handled, Is.False);
            Assert.That(outcome.Reason, Does.Contain("未找到效果目标"));
        }

        [Test]
        public void DelayedEventPlan_UsesStableIdAndFutureYear()
        {
            bool created = DelayedEventRules.TryCreatePlan(4, 3, " future_event ", out DelayedEventPlan plan, out string reason);

            Assert.That(created, Is.True, reason);
            Assert.That(plan.EventId, Is.EqualTo("future_event"));
            Assert.That(plan.DueYear, Is.EqualTo(7));
        }

        [Test]
        public void DelayedEventPlan_RejectsImmediateOrOverflowingSchedule()
        {
            Assert.That(DelayedEventRules.TryCreatePlan(4, 0, "event", out _, out _), Is.False);
            Assert.That(DelayedEventRules.TryCreatePlan(int.MaxValue, 1, "event", out _, out _), Is.False);
        }
    }
}
