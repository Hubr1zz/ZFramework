using System;
using System.Collections.Generic;
using CardGame.ActionQueue;

namespace HuntingInDarkness.ActionFlow
{
    /// <summary>在一个 ActionEnvironment 生命周期内维护稳定引用身份，供实体 Reactor 精确路由。</summary>
    public sealed class ReactorEntityHandleRegistry : IDisposable
    {
        private readonly Dictionary<EntityHandleKey, ReactorEntityHandle> handles = new();
        private readonly string environmentName;
        private bool disposed;

        public ReactorEntityHandleRegistry(string environmentName)
        {
            this.environmentName = string.IsNullOrWhiteSpace(environmentName) ? "ActionEnvironment" : environmentName;
        }

        public int Count => handles.Count;

        public ReactorEntityHandle GetOrCreate(string entityType, string stableId, string displayName = null)
        {
            ThrowIfDisposed();
            var key = new EntityHandleKey(entityType, stableId);
            if (handles.TryGetValue(key, out ReactorEntityHandle handle)) return handle;

            string reactorName = string.IsNullOrWhiteSpace(displayName) ? $"{environmentName}/{key.EntityType}:{key.StableId}" : displayName.Trim();
            handle = new ReactorEntityHandle(key.EntityType, key.StableId, reactorName);
            handles.Add(key, handle);
            return handle;
        }

        public bool TryGet(string entityType, string stableId, out ReactorEntityHandle handle)
        {
            ThrowIfDisposed();
            return handles.TryGetValue(new EntityHandleKey(entityType, stableId), out handle);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            handles.Clear();
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ReactorEntityHandleRegistry));
        }

        private readonly struct EntityHandleKey : IEquatable<EntityHandleKey>
        {
            public EntityHandleKey(string entityType, string stableId)
            {
                if (string.IsNullOrWhiteSpace(entityType)) throw new ArgumentException("Entity type is required.", nameof(entityType));
                if (string.IsNullOrWhiteSpace(stableId)) throw new ArgumentException("Stable id is required.", nameof(stableId));
                EntityType = entityType.Trim();
                StableId = stableId.Trim();
            }

            public string EntityType { get; }
            public string StableId { get; }

            public bool Equals(EntityHandleKey other) => string.Equals(EntityType, other.EntityType, StringComparison.Ordinal) && string.Equals(StableId, other.StableId, StringComparison.Ordinal);
            public override bool Equals(object obj) => obj is EntityHandleKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    return (StringComparer.Ordinal.GetHashCode(EntityType) * 397) ^ StringComparer.Ordinal.GetHashCode(StableId);
                }
            }
        }
    }

    public sealed class ReactorEntityHandle : IReactorEntity
    {
        internal ReactorEntityHandle(string entityType, string stableId, string reactorName)
        {
            EntityType = entityType;
            StableId = stableId;
            ReactorName = reactorName;
        }

        public string EntityType { get; }
        public string StableId { get; }
        public string ReactorName { get; }

        public override string ToString() => ReactorName;
    }
}
