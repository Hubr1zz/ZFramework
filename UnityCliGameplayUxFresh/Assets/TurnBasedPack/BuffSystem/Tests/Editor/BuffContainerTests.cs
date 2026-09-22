using System;
using GameFramework.Buffs.Formula;
using NUnit.Framework;

namespace GameFramework.Buffs.Tests
{
    public sealed class BuffContainerTests
    {
        private static readonly BuffClock TurnClock = new("TurnEnd");

        [Test]
        public void StackAndRefresh_ClampsStacksAndRefreshesDuration()
        {
            var owner = new object();
            var container = new BuffContainer(owner);
            var definition = new BuffDefinition(
                new BuffKey("Burn"),
                maxStacks: 3,
                stackPolicy: BuffStackPolicy.StackAndRefreshDuration,
                defaultDuration: new BuffDuration(TurnClock, 2));

            BuffApplyResult first = container.Apply(new BuffApplyRequest(definition, stacks: 2));
            container.Advance(TurnClock, 1);
            BuffApplyResult second = container.Apply(new BuffApplyRequest(definition, stacks: 2));

            Assert.That(second.Instance, Is.SameAs(first.Instance));
            Assert.That(second.Instance.Stacks, Is.EqualTo(3));
            Assert.That(second.Instance.RemainingDuration, Is.EqualTo(2d));
        }

        [Test]
        public void Duration_ExpiresOnlyOnMatchingClock()
        {
            var container = new BuffContainer(new object());
            var definition = new BuffDefinition(
                new BuffKey("Timed"),
                defaultDuration: new BuffDuration(TurnClock, 2));
            BuffInstance instance = container.Apply(new BuffApplyRequest(definition)).Instance;

            container.Advance(new BuffClock("GameSeconds"), 100);
            Assert.That(instance.IsActive, Is.True);

            container.Advance(TurnClock, 2);
            Assert.That(instance.IsActive, Is.False);
            Assert.That(container.Active, Is.Empty);
        }

        [Test]
        public void LifecycleCallbacks_CannotMutateContainerReentrantly()
        {
            var container = new BuffContainer(new object());
            var first = new BuffDefinition(new BuffKey("First"));
            var second = new BuffDefinition(new BuffKey("Second"));
            container.Changed += (_, _) => container.Apply(new BuffApplyRequest(second));

            Assert.Throws<InvalidOperationException>(() =>
                container.Apply(new BuffApplyRequest(first)));
        }

        [Test]
        public void ChargeTrigger_AllChannels_ExpiresAfterEveryChannelIsConsumed()
        {
            var trigger = new ChargeTrigger(TriggerExhaustionPolicy.AllChannels, 1, 1);

            Assert.That(trigger.TryConsume(0), Is.True);
            Assert.That(trigger.IsExhausted, Is.False);
            Assert.That(trigger.TryConsume(1), Is.True);
            Assert.That(trigger.IsExhausted, Is.True);
        }

        [Test]
        public void ConfigurableFormula_ModifiersTargetFormulaParameterAndLayer()
        {
            FormulaExpression expression = new FormulaParser().Parse("a * (b + c * d) + e");
            var addLayer = new ModifierLayerKey("Add");
            var multiplyLayer = new ModifierLayerKey("Multiply");

            var formula = new FormulaDefinition("Damage", expression)
                .ConfigureParameter("a", new ModifierPipeline()
                    .AddLayer(multiplyLayer, ModifierReducers.Multiply))
                .ConfigureParameter("c", new ModifierPipeline()
                    .AddLayer(addLayer, ModifierReducers.Add));

            var inputs = new FormulaInputs()
                .Set("a", 2).Set("b", 3).Set("c", 4).Set("d", 5).Set("e", 6);
            var modifiers = new StatModifierCollection();
            modifiers.Add(
                new FormulaKey("Damage"),
                new FormulaParameterKey("c"),
                addLayer,
                1);
            modifiers.Add(
                new FormulaKey("Damage"),
                new FormulaParameterKey("a"),
                multiplyLayer,
                2);

            Assert.That(formula.Evaluate(inputs, modifiers), Is.EqualTo(118d));
        }

        [Test]
        public void FormulaParser_RejectsUnknownFunctionAndTrailingTokens()
        {
            var parser = new FormulaParser();
            Assert.Throws<FormatException>(() => parser.Parse("unknown(a)"));
            Assert.Throws<FormatException>(() => parser.Parse("a + b extra"));
        }

        [Test]
        public void BuffStatBinding_RebuildsOnStacksAndRemovesExactContribution()
        {
            var buffKey = new BuffKey("Power");
            var formulaKey = new FormulaKey("Damage");
            var parameter = new FormulaParameterKey("a");
            var layer = new ModifierLayerKey("Add");
            var definition = new BuffDefinition(
                buffKey,
                maxStacks: 3,
                stackPolicy: BuffStackPolicy.Stack);
            var container = new BuffContainer(new object());
            var modifiers = new StatModifierCollection();
            var catalog = new BuffStatModifierCatalog().Register(
                buffKey,
                new StatModifierTemplate(formulaKey, parameter, layer, valuePerStack: 2));
            var formula = new FormulaDefinition(formulaKey, FormulaExpression.Parameter(parameter))
                .ConfigureParameter(parameter, new ModifierPipeline().AddLayer(layer, ModifierReducers.Add));
            var inputs = new FormulaInputs().Set(parameter, 10);

            using var binding = new BuffStatBinding(container, modifiers, catalog);
            BuffInstance instance = container.Apply(new BuffApplyRequest(definition)).Instance;
            Assert.That(formula.Evaluate(inputs, modifiers), Is.EqualTo(12d));

            container.Apply(new BuffApplyRequest(definition));
            Assert.That(formula.Evaluate(inputs, modifiers), Is.EqualTo(14d));

            container.Remove(instance);
            Assert.That(formula.Evaluate(inputs, modifiers), Is.EqualTo(10d));
        }

        [Test]
        public void Removing_CanRejectDispelWithoutRejectingExplicitRemoval()
        {
            var container = new BuffContainer(new object());
            var definition = new BuffDefinition(
                new BuffKey("Undispellable"),
                tags: new[] { "Positive" });
            BuffInstance instance = container.Apply(new BuffApplyRequest(definition)).Instance;
            container.Removing += (_, args) =>
            {
                if (args.Cause == BuffRemovalCause.Dispel)
                    args.Reject("Cannot be dispelled.");
            };

            Assert.That(container.RemoveByTag("Positive"), Is.Zero);
            Assert.That(instance.IsActive, Is.True);
            Assert.That(container.Remove(instance), Is.True);
        }

        [Test]
        public void CustomMergeStrategy_CanReplaceStandardStackPolicy()
        {
            var definition = new BuffDefinition(
                new BuffKey("Custom"),
                maxStacks: 10,
                mergeStrategy: new KeepGreaterStacksStrategy());
            var container = new BuffContainer(new object());

            BuffInstance instance = container.Apply(
                new BuffApplyRequest(definition, stacks: 2)).Instance;
            container.Apply(new BuffApplyRequest(definition, stacks: 5));

            Assert.That(instance.Stacks, Is.EqualTo(5));
        }

        private sealed class KeepGreaterStacksStrategy : IBuffMergeStrategy
        {
            public BuffMergeResult Merge(BuffInstance existing, BuffApplyRequest incoming)
            {
                return new BuffMergeResult(
                    BuffMergeAction.UpdateExisting,
                    Math.Max(existing.Stacks, incoming.Stacks),
                    incoming.Duration);
            }
        }
    }
}
