using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GameFramework.Buffs.Formula
{
    public sealed class StatModifier
    {
        internal StatModifier(
            long id,
            long order,
            FormulaKey formula,
            FormulaParameterKey parameter,
            ModifierLayerKey layer,
            double value,
            int priority,
            object source)
        {
            Id = id;
            Order = order;
            Formula = formula;
            Parameter = parameter;
            Layer = layer;
            Value = value;
            Priority = priority;
            Source = source;
        }

        public long Id { get; }
        public long Order { get; }
        public FormulaKey Formula { get; }
        public FormulaParameterKey Parameter { get; }
        public ModifierLayerKey Layer { get; }
        public double Value { get; }
        public int Priority { get; }
        public object Source { get; }
    }

    public sealed class StatModifierCollection
    {
        private readonly List<StatModifier> _modifiers = new();
        private readonly ReadOnlyCollection<StatModifier> _modifierView;
        private readonly Dictionary<ModifierAddress, List<StatModifier>> _byAddress = new();
        private long _nextId = 1;
        private long _nextOrder;

        public StatModifierCollection()
        {
            _modifierView = _modifiers.AsReadOnly();
        }

        public IReadOnlyList<StatModifier> Active => _modifierView;

        public IDisposable Add(
            FormulaKey formula,
            FormulaParameterKey parameter,
            ModifierLayerKey layer,
            double value,
            int priority = 0,
            object source = null)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value), "Modifier value must be finite.");

            var modifier = new StatModifier(
                _nextId++, _nextOrder++, formula, parameter, layer, value, priority, source);
            _modifiers.Add(modifier);
            var address = new ModifierAddress(formula, parameter, layer);
            if (!_byAddress.TryGetValue(address, out List<StatModifier> matches))
            {
                matches = new List<StatModifier>();
                _byAddress.Add(address, matches);
            }
            matches.Add(modifier);
            return new ModifierHandle(this, modifier);
        }

        internal double Evaluate(
            double baseValue,
            FormulaKey formula,
            FormulaParameterKey parameter,
            ModifierPipeline pipeline)
        {
            double current = baseValue;
            foreach (ModifierLayerDefinition layer in pipeline.Layers)
            {
                var address = new ModifierAddress(formula, parameter, layer.Key);
                if (_byAddress.TryGetValue(address, out List<StatModifier> matches) && matches.Count > 0)
                    current = layer.Reducer.Reduce(current, matches);
            }
            return current;
        }

        internal void ValidateLayers(
            FormulaKey formula,
            FormulaParameterKey parameter,
            ModifierPipeline pipeline)
        {
            foreach (ModifierAddress address in _byAddress.Keys)
            {
                if (address.Formula.Equals(formula) &&
                    address.Parameter.Equals(parameter) &&
                    !pipeline.Contains(address.Layer))
                {
                    throw new InvalidOperationException(
                        $"Modifier layer '{address.Layer}' is not configured for parameter '{parameter}' " +
                        $"in formula '{formula}'.");
                }
            }
        }

        private void Remove(StatModifier modifier)
        {
            _modifiers.Remove(modifier);
            var address = new ModifierAddress(modifier.Formula, modifier.Parameter, modifier.Layer);
            if (!_byAddress.TryGetValue(address, out List<StatModifier> matches))
                return;
            matches.Remove(modifier);
            if (matches.Count == 0)
                _byAddress.Remove(address);
        }

        private readonly struct ModifierAddress : IEquatable<ModifierAddress>
        {
            public ModifierAddress(
                FormulaKey formula,
                FormulaParameterKey parameter,
                ModifierLayerKey layer)
            {
                Formula = formula;
                Parameter = parameter;
                Layer = layer;
            }

            public FormulaKey Formula { get; }
            public FormulaParameterKey Parameter { get; }
            public ModifierLayerKey Layer { get; }
            public bool Equals(ModifierAddress other) =>
                Formula.Equals(other.Formula) && Parameter.Equals(other.Parameter) && Layer.Equals(other.Layer);
            public override bool Equals(object obj) => obj is ModifierAddress other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Formula.GetHashCode();
                    hash = (hash * 397) ^ Parameter.GetHashCode();
                    return (hash * 397) ^ Layer.GetHashCode();
                }
            }
        }

        private sealed class ModifierHandle : IDisposable
        {
            private StatModifierCollection _owner;
            private readonly StatModifier _modifier;
            public ModifierHandle(StatModifierCollection owner, StatModifier modifier)
            {
                _owner = owner;
                _modifier = modifier;
            }
            public void Dispose()
            {
                StatModifierCollection owner = _owner;
                if (owner == null)
                    return;
                _owner = null;
                owner.Remove(_modifier);
            }
        }
    }
}
