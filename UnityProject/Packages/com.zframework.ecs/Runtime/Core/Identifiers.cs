using System;

namespace ZFramework.ECS
{
    public readonly struct EntityId : IEquatable<EntityId>
    {
        public static readonly EntityId Invalid = new EntityId(-1, 0);

        public int Index { get; }
        public int Version { get; }
        public bool IsValid => Index >= 0 && Version > 0;

        internal EntityId(int index, int version)
        {
            Index = index;
            Version = version;
        }

        public bool Equals(EntityId other) => Index == other.Index && Version == other.Version;
        public override bool Equals(object obj) => obj is EntityId other && Equals(other);
        public override int GetHashCode() => unchecked((Index * 397) ^ Version);
        public override string ToString() => IsValid ? $"Entity({Index}:{Version})" : "Entity(Invalid)";
        public static bool operator ==(EntityId left, EntityId right) => left.Equals(right);
        public static bool operator !=(EntityId left, EntityId right) => !left.Equals(right);
    }

    public readonly struct TagId : IEquatable<TagId>
    {
        public int Value { get; }

        public TagId(int value)
        {
            Value = value;
        }

        public static TagId FromName(string name) => new TagId(StableId<TagId>.FromName(name));
        public bool Equals(TagId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is TagId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => $"Tag({Value})";
        public static bool operator ==(TagId left, TagId right) => left.Equals(right);
        public static bool operator !=(TagId left, TagId right) => !left.Equals(right);
    }

    public readonly struct AbilityId : IEquatable<AbilityId>
    {
        public int Value { get; }

        public AbilityId(int value)
        {
            Value = value;
        }

        public static AbilityId FromName(string name) => new AbilityId(StableId<AbilityId>.FromName(name));
        public bool Equals(AbilityId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is AbilityId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => $"Ability({Value})";
        public static bool operator ==(AbilityId left, AbilityId right) => left.Equals(right);
        public static bool operator !=(AbilityId left, AbilityId right) => !left.Equals(right);
    }

    internal static class StableId<TCategory>
    {
        private static readonly object SyncRoot = new object();
        private static readonly System.Collections.Generic.Dictionary<int, string> Names =
            new System.Collections.Generic.Dictionary<int, string>();

        public static int FromName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A stable id requires a non-empty name.", nameof(name));
            }

            unchecked
            {
                const uint offset = 2166136261;
                const uint prime = 16777619;
                uint hash = offset;
                for (int i = 0; i < name.Length; i++)
                {
                    hash ^= name[i];
                    hash *= prime;
                }

                int value = (int)hash;
                lock (SyncRoot)
                {
                    if (Names.TryGetValue(value, out string existing)
                        && !string.Equals(existing, name, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Stable id collision between '{existing}' and '{name}' in {typeof(TCategory).Name}.");
                    }

                    Names[value] = name;
                }

                return value;
            }
        }
    }
}
