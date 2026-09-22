using System;
using HuntingInDarkness.GameCore.Hunt;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests
{
    public sealed class HuntNoiseRulesTests
    {
        [Test]
        public void CreatePlan_AddsBaseNoiseAndEquipmentNoise()
        {
            NoiseCheckPlan plan = HuntNoiseRules.CreatePlan(3, new[] { 1, 1 }, new HuntNoiseDefinition(12, 1, 5));

            Assert.That(plan.NoiseScore, Is.EqualTo(5));
            Assert.That(plan.DangerCardCount, Is.EqualTo(5));
            Assert.That(plan.DeckSize, Is.EqualTo(12));
            Assert.That(plan.IsEnabled, Is.True);
            Assert.That(plan.IsDangerCard(1), Is.True);
            Assert.That(plan.IsDangerCard(5), Is.True);
            Assert.That(plan.IsDangerCard(6), Is.False);
        }

        [Test]
        public void CreatePlan_EquipmentCanReduceNoiseWithoutGoingBelowZero()
        {
            NoiseCheckPlan quieter = HuntNoiseRules.CreatePlan(3, new[] { -2 }, new HuntNoiseDefinition(12, 1, 5));
            NoiseCheckPlan silent = HuntNoiseRules.CreatePlan(1, new[] { int.MinValue }, new HuntNoiseDefinition(12, 1, 5));

            Assert.That(quieter.NoiseScore, Is.EqualTo(1));
            Assert.That(quieter.DangerCardCount, Is.EqualTo(1));
            Assert.That(silent.NoiseScore, Is.Zero);
            Assert.That(silent.DangerCardCount, Is.Zero);
        }

        [Test]
        public void CreatePlan_ClampsDangerCardsToConfiguredLimits()
        {
            NoiseCheckPlan plan = HuntNoiseRules.CreatePlan(10, new[] { 8 }, new HuntNoiseDefinition(4, 3, 20));

            Assert.That(plan.NoiseScore, Is.EqualTo(38));
            Assert.That(plan.DangerCardCount, Is.EqualTo(4));
            Assert.That(plan.DangerCardCount, Is.LessThanOrEqualTo(plan.DeckSize));
        }

        [Test]
        public void CreatePlan_NormalizesNegativeInputsToSafeEmptyPlan()
        {
            NoiseCheckPlan plan = HuntNoiseRules.CreatePlan(-1, new[] { -2 }, new HuntNoiseDefinition(-3, -4, -5));

            Assert.That(plan.NoiseScore, Is.Zero);
            Assert.That(plan.DangerCardCount, Is.Zero);
            Assert.That(plan.DeckSize, Is.Zero);
            Assert.That(plan.IsEnabled, Is.False);
            Assert.That(plan.IsDangerCard(1), Is.False);
        }

        [Test]
        public void CreatePlan_SaturatesOverflowWithoutWrapping()
        {
            NoiseCheckPlan plan = HuntNoiseRules.CreatePlan(int.MaxValue, new[] { int.MaxValue }, new HuntNoiseDefinition(int.MaxValue, int.MaxValue, int.MaxValue));

            Assert.That(plan.NoiseScore, Is.EqualTo(int.MaxValue));
            Assert.That(plan.DangerCardCount, Is.EqualTo(int.MaxValue));
            Assert.That(plan.DeckSize, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void CreatePlan_NullEquipmentNoiseIsSafe()
        {
            NoiseCheckPlan plan = HuntNoiseRules.CreatePlan(2, null, new HuntNoiseDefinition(8, 2, 3));

            Assert.That(plan.NoiseScore, Is.EqualTo(4));
            Assert.That(plan.DangerCardCount, Is.EqualTo(3));
        }

        [Test]
        public void CreatePlan_EmptyDeckDisablesDangerCards()
        {
            NoiseCheckPlan plan = HuntNoiseRules.CreatePlan(4, Array.Empty<int>(), new HuntNoiseDefinition(0, 2, 3));

            Assert.That(plan.IsEnabled, Is.False);
            Assert.That(plan.DangerCardCount, Is.Zero);
            Assert.That(plan.IsDangerCard(1), Is.False);
        }

        [Test]
        public void ApplyNoiseModifier_RecalculatesDangerWithoutEscapingLimits()
        {
            NoiseCheckPlan plan = HuntNoiseRules.CreatePlan(2, null, new HuntNoiseDefinition(10, 1, 7));

            NoiseCheckPlan quieter = HuntNoiseRules.ApplyNoiseModifier(plan, -5, 7);
            NoiseCheckPlan louder = HuntNoiseRules.ApplyNoiseModifier(plan, int.MaxValue, 7);

            Assert.That(quieter.NoiseScore, Is.Zero);
            Assert.That(quieter.DangerCardCount, Is.Zero);
            Assert.That(louder.NoiseScore, Is.EqualTo(int.MaxValue));
            Assert.That(louder.DangerCardCount, Is.EqualTo(7));
        }
    }
}
