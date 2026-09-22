using System;
using System.Collections.Generic;

namespace ZFramework.ECS
{
    public readonly struct ActiveAbility
    {
        public AbilityId Ability { get; }
        public double EndTime { get; }
        internal AbilityDefinition Definition { get; }

        internal ActiveAbility(AbilityDefinition definition, double endTime)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Ability = definition.Id;
            EndTime = endTime;
        }
    }

    public sealed class AbilityRuntimeBuffer
    {
        private readonly List<ActiveAbility> _items = new List<ActiveAbility>();

        public int Count => _items.Count;
        public IReadOnlyList<ActiveAbility> Items => _items;

        public bool Contains(AbilityId ability)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Ability == ability) return true;
            }

            return false;
        }

        internal void Add(ActiveAbility ability) => _items.Add(ability);
        internal void RemoveAt(int index) => _items.RemoveAt(index);
    }

    internal readonly struct AbilityRequest
    {
        public EntityId Entity { get; }
        public AbilityId Ability { get; }

        public AbilityRequest(EntityId entity, AbilityId ability)
        {
            Entity = entity;
            Ability = ability;
        }
    }

    internal readonly struct AbilityKey : IEquatable<AbilityKey>
    {
        public EntityId Entity { get; }
        public AbilityId Ability { get; }

        public AbilityKey(EntityId entity, AbilityId ability)
        {
            Entity = entity;
            Ability = ability;
        }

        public bool Equals(AbilityKey other) => Entity == other.Entity && Ability == other.Ability;
        public override bool Equals(object obj) => obj is AbilityKey other && Equals(other);
        public override int GetHashCode() => unchecked((Entity.GetHashCode() * 397) ^ Ability.GetHashCode());
    }

    public sealed class AbilityService
    {
        private readonly Queue<AbilityRequest> _requests = new Queue<AbilityRequest>();
        private readonly Queue<AbilityRequest> _cancellations = new Queue<AbilityRequest>();

        public AbilityRegistry Registry { get; } = new AbilityRegistry();

        public void Request(EntityId entity, AbilityId ability) =>
            _requests.Enqueue(new AbilityRequest(entity, ability));

        public void Cancel(EntityId entity, AbilityId ability) =>
            _cancellations.Enqueue(new AbilityRequest(entity, ability));

        public void ClearRequests()
        {
            _requests.Clear();
            _cancellations.Clear();
        }

        internal bool TryDequeueRequest(out AbilityRequest request)
        {
            if (_requests.Count > 0)
            {
                request = _requests.Dequeue();
                return true;
            }

            request = default;
            return false;
        }

        internal void DrainCancellations(HashSet<AbilityKey> target)
        {
            target.Clear();
            while (_cancellations.Count > 0)
            {
                AbilityRequest request = _cancellations.Dequeue();
                target.Add(new AbilityKey(request.Entity, request.Ability));
            }
        }
    }
}
