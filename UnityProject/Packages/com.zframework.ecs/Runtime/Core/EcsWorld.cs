using System;
using System.Collections.Generic;

namespace ZFramework.ECS
{
    internal interface IComponentPool
    {
        bool Has(int entityIndex);
        void Remove(int entityIndex);
        void Clear();
        IEnumerable<int> Indices { get; }
    }

    internal sealed class ComponentPool<T> : IComponentPool
    {
        private readonly Dictionary<int, T> _items = new Dictionary<int, T>();

        public IEnumerable<int> Indices => _items.Keys;
        public bool Has(int entityIndex) => _items.ContainsKey(entityIndex);
        public bool TryGet(int entityIndex, out T component) => _items.TryGetValue(entityIndex, out component);
        public void Set(int entityIndex, T component) => _items[entityIndex] = component;
        public void Remove(int entityIndex) => _items.Remove(entityIndex);
        public void Clear() => _items.Clear();
    }

    public sealed class EcsWorld
    {
        private static readonly TagId[] EmptyTags = Array.Empty<TagId>();

        private readonly List<int> _versions = new List<int>();
        private readonly List<bool> _alive = new List<bool>();
        private readonly Stack<int> _freeIndices = new Stack<int>();
        private readonly Dictionary<Type, IComponentPool> _componentPools = new Dictionary<Type, IComponentPool>();
        private readonly Dictionary<int, Dictionary<TagId, int>> _tags =
            new Dictionary<int, Dictionary<TagId, int>>();

        public int EntityCount { get; private set; }

        public EntityId CreateEntity()
        {
            int index;
            if (_freeIndices.Count > 0)
            {
                index = _freeIndices.Pop();
                _alive[index] = true;
            }
            else
            {
                index = _versions.Count;
                _versions.Add(1);
                _alive.Add(true);
            }

            EntityCount++;
            return new EntityId(index, _versions[index]);
        }

        public bool IsAlive(EntityId entity)
        {
            return entity.Index >= 0
                   && entity.Index < _alive.Count
                   && _alive[entity.Index]
                   && _versions[entity.Index] == entity.Version;
        }

        public bool DestroyEntity(EntityId entity)
        {
            if (!IsAlive(entity))
            {
                return false;
            }

            foreach (IComponentPool pool in _componentPools.Values)
            {
                pool.Remove(entity.Index);
            }

            _tags.Remove(entity.Index);
            _alive[entity.Index] = false;
            _versions[entity.Index]++;
            _freeIndices.Push(entity.Index);
            EntityCount--;
            return true;
        }

        public void SetComponent<T>(EntityId entity, T component)
        {
            EnsureAlive(entity);
            GetOrCreatePool<T>().Set(entity.Index, component);
        }

        public bool HasComponent<T>(EntityId entity)
        {
            return IsAlive(entity) && TryGetPool<T>(out ComponentPool<T> pool) && pool.Has(entity.Index);
        }

        public bool TryGetComponent<T>(EntityId entity, out T component)
        {
            if (IsAlive(entity) && TryGetPool<T>(out ComponentPool<T> pool))
            {
                return pool.TryGet(entity.Index, out component);
            }

            component = default;
            return false;
        }

        public T GetComponent<T>(EntityId entity)
        {
            if (!TryGetComponent(entity, out T component))
            {
                throw new InvalidOperationException($"{entity} does not contain component {typeof(T).FullName}.");
            }

            return component;
        }

        public bool RemoveComponent<T>(EntityId entity)
        {
            if (!IsAlive(entity) || !TryGetPool<T>(out ComponentPool<T> pool) || !pool.Has(entity.Index))
            {
                return false;
            }

            pool.Remove(entity.Index);
            return true;
        }

        public bool AddTag(EntityId entity, TagId tag)
        {
            EnsureAlive(entity);
            if (!_tags.TryGetValue(entity.Index, out Dictionary<TagId, int> tags))
            {
                tags = new Dictionary<TagId, int>();
                _tags.Add(entity.Index, tags);
            }

            tags.TryGetValue(tag, out int count);
            tags[tag] = checked(count + 1);
            return count == 0;
        }

        public bool RemoveTag(EntityId entity, TagId tag)
        {
            if (!IsAlive(entity)
                || !_tags.TryGetValue(entity.Index, out Dictionary<TagId, int> tags)
                || !tags.TryGetValue(tag, out int count))
            {
                return false;
            }

            if (count <= 1)
            {
                tags.Remove(tag);
                if (tags.Count == 0)
                {
                    _tags.Remove(entity.Index);
                }
            }
            else
            {
                tags[tag] = count - 1;
            }

            return true;
        }

        public bool HasTag(EntityId entity, TagId tag)
        {
            return GetTagCount(entity, tag) > 0;
        }

        public int GetTagCount(EntityId entity, TagId tag)
        {
            if (IsAlive(entity)
                && _tags.TryGetValue(entity.Index, out Dictionary<TagId, int> tags)
                && tags.TryGetValue(tag, out int count))
            {
                return count;
            }

            return 0;
        }

        public IReadOnlyCollection<TagId> GetTags(EntityId entity)
        {
            EnsureAlive(entity);
            return _tags.TryGetValue(entity.Index, out Dictionary<TagId, int> tags)
                ? tags.Keys
                : EmptyTags;
        }

        public IEnumerable<EntityId> Query<T>()
        {
            if (!TryGetPool<T>(out ComponentPool<T> pool))
            {
                yield break;
            }

            foreach (int index in pool.Indices)
            {
                if (_alive[index])
                {
                    yield return new EntityId(index, _versions[index]);
                }
            }
        }

        public IEnumerable<EntityId> Query<TFirst, TSecond>()
        {
            if (!TryGetPool<TFirst>(out ComponentPool<TFirst> first)
                || !TryGetPool<TSecond>(out ComponentPool<TSecond> second))
            {
                yield break;
            }

            foreach (int index in first.Indices)
            {
                if (_alive[index] && second.Has(index))
                {
                    yield return new EntityId(index, _versions[index]);
                }
            }
        }

        public void Clear()
        {
            foreach (IComponentPool pool in _componentPools.Values)
            {
                pool.Clear();
            }

            _tags.Clear();
            _freeIndices.Clear();
            for (int i = 0; i < _alive.Count; i++)
            {
                if (_alive[i])
                {
                    _versions[i]++;
                    _alive[i] = false;
                }

                _freeIndices.Push(i);
            }

            EntityCount = 0;
        }

        private ComponentPool<T> GetOrCreatePool<T>()
        {
            Type type = typeof(T);
            if (!_componentPools.TryGetValue(type, out IComponentPool pool))
            {
                pool = new ComponentPool<T>();
                _componentPools.Add(type, pool);
            }

            return (ComponentPool<T>)pool;
        }

        private bool TryGetPool<T>(out ComponentPool<T> pool)
        {
            if (_componentPools.TryGetValue(typeof(T), out IComponentPool value))
            {
                pool = (ComponentPool<T>)value;
                return true;
            }

            pool = null;
            return false;
        }

        private void EnsureAlive(EntityId entity)
        {
            if (!IsAlive(entity))
            {
                throw new InvalidOperationException($"{entity} is not alive in this world.");
            }
        }
    }
}
