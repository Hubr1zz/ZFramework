using System;
using System.Collections.Generic;

namespace GameFramework.Buffs.Formula
{
    public interface IModifierLayerReducer
    {
        double Reduce(double currentValue, IReadOnlyList<StatModifier> modifiers);
    }

    public sealed class ModifierLayerDefinition
    {
        public ModifierLayerDefinition(ModifierLayerKey key, IModifierLayerReducer reducer)
        {
            Key = key;
            Reducer = reducer ?? throw new ArgumentNullException(nameof(reducer));
        }

        public ModifierLayerKey Key { get; }
        public IModifierLayerReducer Reducer { get; }
    }

    public sealed class ModifierPipeline
    {
        private readonly List<ModifierLayerDefinition> _layers = new();
        private readonly HashSet<ModifierLayerKey> _keys = new();

        public IReadOnlyList<ModifierLayerDefinition> Layers => _layers;

        public ModifierPipeline AddLayer(ModifierLayerKey key, IModifierLayerReducer reducer)
        {
            if (!_keys.Add(key))
                throw new InvalidOperationException($"Modifier layer '{key}' already exists in this pipeline.");
            _layers.Add(new ModifierLayerDefinition(key, reducer));
            return this;
        }

        public bool Contains(ModifierLayerKey key) => _keys.Contains(key);
    }

    public static class ModifierReducers
    {
        public static readonly IModifierLayerReducer Add =
            new DelegateReducer((current, modifiers) =>
            {
                double sum = 0d;
                foreach (StatModifier modifier in modifiers)
                    sum += modifier.Value;
                return current + sum;
            });

        public static readonly IModifierLayerReducer AdditiveMultiplier =
            new DelegateReducer((current, modifiers) =>
            {
                double sum = 0d;
                foreach (StatModifier modifier in modifiers)
                    sum += modifier.Value;
                return current * (1d + sum);
            });

        public static readonly IModifierLayerReducer Multiply =
            new DelegateReducer((current, modifiers) =>
            {
                double product = 1d;
                foreach (StatModifier modifier in modifiers)
                    product *= modifier.Value;
                return current * product;
            });

        public static readonly IModifierLayerReducer OverrideByPriority =
            new DelegateReducer((current, modifiers) =>
            {
                if (modifiers.Count == 0)
                    return current;

                StatModifier selected = modifiers[0];
                for (int i = 1; i < modifiers.Count; i++)
                {
                    StatModifier candidate = modifiers[i];
                    if (candidate.Priority > selected.Priority ||
                        candidate.Priority == selected.Priority && candidate.Order > selected.Order)
                        selected = candidate;
                }
                return selected.Value;
            });

        public static IModifierLayerReducer Custom(
            Func<double, IReadOnlyList<StatModifier>, double> reducer) =>
            new DelegateReducer(reducer);

        private sealed class DelegateReducer : IModifierLayerReducer
        {
            private readonly Func<double, IReadOnlyList<StatModifier>, double> _reducer;
            public DelegateReducer(Func<double, IReadOnlyList<StatModifier>, double> reducer) =>
                _reducer = reducer ?? throw new ArgumentNullException(nameof(reducer));
            public double Reduce(double currentValue, IReadOnlyList<StatModifier> modifiers) =>
                _reducer(currentValue, modifiers);
        }
    }
}
