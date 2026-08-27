using System;
using System.Collections.Generic;

namespace ZFramework.ECS
{
    public sealed class AbilityDefinition
    {
        private readonly TagId[] _grantOnStart;
        private readonly TagId[] _removeOnStart;
        private readonly TagId[] _removeOnEnd;

        public AbilityId Id { get; }
        public string Name { get; }
        public TagQuery ActivationQuery { get; }
        public TagQuery OngoingQuery { get; }
        public IReadOnlyList<TagId> GrantOnStart => _grantOnStart;
        public IReadOnlyList<TagId> RemoveOnStart => _removeOnStart;
        public IReadOnlyList<TagId> RemoveOnEnd => _removeOnEnd;

        /// <summary>
        /// Negative values mean manual lifetime, zero means one pipeline tick, and positive values mean seconds.
        /// </summary>
        public float DurationSeconds { get; }

        public AbilityDefinition(
            string name,
            TagQuery activationQuery = null,
            TagQuery ongoingQuery = null,
            IEnumerable<TagId> grantOnStart = null,
            IEnumerable<TagId> removeOnStart = null,
            IEnumerable<TagId> removeOnEnd = null,
            float durationSeconds = 0f)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("An ability requires a non-empty name.", nameof(name));
            }

            if (float.IsNaN(durationSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(durationSeconds), "Ability duration cannot be NaN.");
            }

            Name = name;
            Id = AbilityId.FromName(name);
            ActivationQuery = activationQuery ?? new TagQuery();
            OngoingQuery = ongoingQuery ?? new TagQuery();
            _grantOnStart = Copy(grantOnStart);
            _removeOnStart = Copy(removeOnStart);
            _removeOnEnd = removeOnEnd == null ? Copy(_grantOnStart) : Copy(removeOnEnd);
            DurationSeconds = durationSeconds;
        }

        private static TagId[] Copy(IEnumerable<TagId> source)
        {
            return source == null ? Array.Empty<TagId>() : new List<TagId>(source).ToArray();
        }
    }

    public sealed class AbilityRegistry
    {
        private readonly Dictionary<AbilityId, AbilityDefinition> _definitions =
            new Dictionary<AbilityId, AbilityDefinition>();

        public int Count => _definitions.Count;

        public void Register(AbilityDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            if (_definitions.TryGetValue(definition.Id, out AbilityDefinition existing))
            {
                if (!string.Equals(existing.Name, definition.Name, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Stable id collision between abilities '{existing.Name}' and '{definition.Name}'.");
                }

                throw new InvalidOperationException($"Ability '{definition.Name}' is already registered.");
            }

            _definitions.Add(definition.Id, definition);
        }

        public bool TryGet(AbilityId id, out AbilityDefinition definition) =>
            _definitions.TryGetValue(id, out definition);

        public AbilityDefinition Get(AbilityId id)
        {
            if (!_definitions.TryGetValue(id, out AbilityDefinition definition))
            {
                throw new KeyNotFoundException($"Ability {id} is not registered.");
            }

            return definition;
        }

        public bool Remove(AbilityId id) => _definitions.Remove(id);
        public void Clear() => _definitions.Clear();
    }
}
