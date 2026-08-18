using System;
using System.Collections.Generic;

namespace GameFramework.Buffs.Formula
{
    public sealed class FormulaInputs
    {
        private readonly Dictionary<FormulaParameterKey, double> _values = new();

        public FormulaInputs Set(FormulaParameterKey parameter, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value), "Formula input must be finite.");
            _values[parameter] = value;
            return this;
        }

        public FormulaInputs Set(string parameter, double value) =>
            Set(new FormulaParameterKey(parameter), value);

        public bool TryGet(FormulaParameterKey parameter, out double value) =>
            _values.TryGetValue(parameter, out value);
    }

    public sealed class FormulaDefinition
    {
        private readonly Dictionary<FormulaParameterKey, ModifierPipeline> _pipelines = new();
        private readonly HashSet<FormulaParameterKey> _parameters = new();

        public FormulaDefinition(FormulaKey key, FormulaExpression expression)
        {
            Key = key;
            Expression = expression ?? throw new ArgumentNullException(nameof(expression));
            Expression.CollectParameters(_parameters);
        }

        public FormulaDefinition(string key, FormulaExpression expression)
            : this(new FormulaKey(key), expression) { }

        public FormulaKey Key { get; }
        public FormulaExpression Expression { get; }
        public IReadOnlyCollection<FormulaParameterKey> Parameters => _parameters;

        public FormulaDefinition ConfigureParameter(
            FormulaParameterKey parameter,
            ModifierPipeline pipeline)
        {
            if (!_parameters.Contains(parameter))
                throw new ArgumentException($"Parameter '{parameter}' is not used by formula '{Key}'.", nameof(parameter));
            _pipelines[parameter] = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            return this;
        }

        public FormulaDefinition ConfigureParameter(string parameter, ModifierPipeline pipeline) =>
            ConfigureParameter(new FormulaParameterKey(parameter), pipeline);

        public double Evaluate(FormulaInputs inputs, StatModifierCollection modifiers = null)
        {
            if (inputs == null)
                throw new ArgumentNullException(nameof(inputs));

            return Expression.Evaluate(parameter =>
            {
                if (!inputs.TryGet(parameter, out double value))
                    throw new KeyNotFoundException($"Formula '{Key}' requires parameter '{parameter}'.");

                if (modifiers == null || !_pipelines.TryGetValue(parameter, out ModifierPipeline pipeline))
                    return value;

                modifiers.ValidateLayers(Key, parameter, pipeline);
                return modifiers.Evaluate(value, Key, parameter, pipeline);
            });
        }
    }
}
