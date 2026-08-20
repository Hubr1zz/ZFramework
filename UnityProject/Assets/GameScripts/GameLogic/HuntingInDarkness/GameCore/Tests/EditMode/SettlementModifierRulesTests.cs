using System.Collections.Generic;
using HuntingInDarkness.GameCore.Settlement;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests
{
    public sealed class SettlementModifierRulesTests
    {
        [Test]
        public void TryReconcileHunter_IsIdempotentAndTracksValueChanges()
        {
            var hunter = new HunterState { WillpowerMax = 2, Willpower = 2 };
            var modifier = CreateModifier(1);

            Assert.That(SettlementModifierRules.TryReconcileHunter(hunter, new[] { modifier }, null, out string reason), Is.True, reason);
            Assert.That(hunter.WillpowerMax, Is.EqualTo(3));
            Assert.That(SettlementModifierRules.TryReconcileHunter(hunter, new[] { modifier }, null, out reason), Is.True, reason);
            Assert.That(hunter.WillpowerMax, Is.EqualTo(3));

            modifier.Value = 3;
            Assert.That(SettlementModifierRules.TryReconcileHunter(hunter, new[] { modifier }, null, out reason), Is.True, reason);
            Assert.That(hunter.WillpowerMax, Is.EqualTo(5));
            Assert.That(SettlementModifierRules.TryReconcileHunter(hunter, new List<SettlementModifierState>(), null, out reason), Is.True, reason);
            Assert.That(hunter.WillpowerMax, Is.EqualTo(2));
        }

        [Test]
        public void TryReconcileHunter_SaturationRemovesOnlyActualContribution()
        {
            var hunter = new HunterState { WillpowerMax = 1, Willpower = 1 };
            SettlementModifierState modifier = CreateModifier(-5);

            Assert.That(SettlementModifierRules.TryReconcileHunter(hunter, new[] { modifier }, null, out string reason), Is.True, reason);
            Assert.That(hunter.WillpowerMax, Is.Zero);
            Assert.That(hunter.SettlementModifierContributions[0].Value, Is.EqualTo(-1));
            Assert.That(SettlementModifierRules.TryReconcileHunter(hunter, new List<SettlementModifierState>(), null, out reason), Is.True, reason);
            Assert.That(hunter.WillpowerMax, Is.EqualTo(1));
        }

        private static SettlementModifierState CreateModifier(int value)
        {
            return new SettlementModifierState
            {
                ModifierId = "ritual:willpower",
                SourceKind = SettlementModifierSourceKind.Invention,
                SourceId = "ritual",
                Kind = InventionEffectKind.ModifyWillpowerMaximum,
                Target = InventionEffectTarget.AllLivingAndFutureHunters,
                ConfiguredValue = 1,
                Value = value
            };
        }
    }
}
