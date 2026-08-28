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
        public void HunterSuppression_ClassifiesConfiguredBandsAndClampsValues()
        {
            Assert.That(HunterSuppressionRules.Clamp(-1), Is.EqualTo(HunterSuppressionRules.Minimum));
            Assert.That(HunterSuppressionRules.Clamp(int.MinValue), Is.EqualTo(HunterSuppressionRules.Minimum));
            Assert.That(HunterSuppressionRules.Clamp(int.MaxValue), Is.EqualTo(HunterSuppressionRules.Maximum));
            Assert.That(HunterSuppressionRules.Classify(0), Is.EqualTo(HunterSuppressionState.Mad));
            Assert.That(HunterSuppressionRules.Classify(2), Is.EqualTo(HunterSuppressionState.Mad));
            Assert.That(HunterSuppressionRules.Classify(3), Is.EqualTo(HunterSuppressionState.Normal));
            Assert.That(HunterSuppressionRules.Classify(5), Is.EqualTo(HunterSuppressionState.Normal));
            Assert.That(HunterSuppressionRules.Classify(6), Is.EqualTo(HunterSuppressionState.Passive));
            Assert.That(HunterSuppressionRules.GetDisplayName(8), Is.EqualTo("消极"));
        }

        [Test]
        public void HunterSuppression_AddAcceptsOnlyPositiveValuesAndSaturates()
        {
            int saturated = HunterSuppressionRules.Increase(7, int.MaxValue);
            Assert.That(saturated, Is.EqualTo(HunterSuppressionRules.Maximum));
            int unchanged = HunterSuppressionRules.Increase(4, 0);
            Assert.That(unchanged, Is.EqualTo(4));
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
        public void AddInsanity_RejectsNonPositiveAndSaturatesAtMaximum()
        {
            var hunter = new HunterState { Insanity = 7 };
            SettlementEffectOutcome rejected = SettlementEffectRules.Apply(SettlementEffectKind.AddInsanity, "selected", 0, hunter, hunter, new[] { hunter }, _ => 0, (_, _) => { }, (_, _) => false, _ => { });

            Assert.That(rejected.Handled, Is.False);
            Assert.That(hunter.Insanity, Is.EqualTo(7));

            SettlementEffectOutcome applied = SettlementEffectRules.Apply(SettlementEffectKind.AddInsanity, "selected", int.MaxValue, hunter, hunter, new[] { hunter }, _ => 0, (_, _) => { }, (_, _) => false, _ => { });

            Assert.That(applied.Handled, Is.True);
            Assert.That(hunter.Insanity, Is.EqualTo(HunterSuppressionRules.Maximum));
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
