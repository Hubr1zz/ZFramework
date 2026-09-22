using System;

namespace GameFramework.Buffs.Formula
{
    public readonly struct FormulaKey : IEquatable<FormulaKey>
    {
        public FormulaKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Formula key cannot be empty.", nameof(value));
            Value = value;
        }

        public string Value { get; }
        public bool Equals(FormulaKey other) =>
            StringComparer.Ordinal.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is FormulaKey other && Equals(other);
        public override int GetHashCode() =>
            Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
    }

    public readonly struct FormulaParameterKey : IEquatable<FormulaParameterKey>
    {
        public FormulaParameterKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Formula parameter key cannot be empty.", nameof(value));
            Value = value;
        }

        public string Value { get; }
        public bool Equals(FormulaParameterKey other) =>
            StringComparer.Ordinal.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is FormulaParameterKey other && Equals(other);
        public override int GetHashCode() =>
            Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
    }

    public readonly struct ModifierLayerKey : IEquatable<ModifierLayerKey>
    {
        public ModifierLayerKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Modifier layer key cannot be empty.", nameof(value));
            Value = value;
        }

        public string Value { get; }
        public bool Equals(ModifierLayerKey other) =>
            StringComparer.Ordinal.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is ModifierLayerKey other && Equals(other);
        public override int GetHashCode() =>
            Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
    }
}
