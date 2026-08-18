using System;
using System.Collections.Generic;
using CardGame.ActionQueue;
using GameFramework.Buffs;

namespace GameFramework.Buffs.ActionQueueAdapter
{
    public sealed class BuffReactionGateCatalog
    {
        private readonly Dictionary<BuffKey, Func<BuffInstance, IEnumerable<IReactionGate>>>
            _factories = new();

        public BuffReactionGateCatalog Register(
            BuffKey key,
            Func<BuffInstance, IEnumerable<IReactionGate>> factory)
        {
            _factories[key] = factory ?? throw new ArgumentNullException(nameof(factory));
            return this;
        }

        internal bool TryCreate(BuffInstance instance, out IEnumerable<IReactionGate> gates)
        {
            if (_factories.TryGetValue(instance.Definition.Key, out var factory))
            {
                gates = factory(instance);
                return true;
            }
            gates = null;
            return false;
        }
    }

    /// <summary>
    /// Buff 存活时注册玩法 ReactionGate，移除/过期时释放。系统 EngineGuard 不经过此 Adapter。
    /// </summary>
    public sealed class BuffReactionGateBinding : IDisposable
    {
        private readonly BuffContainer _container;
        private readonly ReactionGateRegistry _registry;
        private readonly BuffReactionGateCatalog _catalog;
        private readonly Dictionary<long, List<IDisposable>> _handles = new();

        public BuffReactionGateBinding(
            BuffContainer container,
            ReactionGateRegistry registry,
            BuffReactionGateCatalog catalog)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _container.Changed += OnChanged;
            foreach (BuffInstance instance in _container.Active)
                Register(instance);
        }

        private void OnChanged(object sender, BuffChangedEventArgs args)
        {
            if (args.Kind == BuffChangeKind.Added)
                Register(args.Instance);
            else if (args.Kind == BuffChangeKind.Removed || args.Kind == BuffChangeKind.Expired)
                Unregister(args.Instance.Id);
        }

        private void Register(BuffInstance instance)
        {
            Unregister(instance.Id);
            if (!_catalog.TryCreate(instance, out IEnumerable<IReactionGate> gates) || gates == null)
                return;
            var handles = new List<IDisposable>();
            foreach (IReactionGate gate in gates)
                handles.Add(_registry.Register(gate));
            _handles.Add(instance.Id, handles);
        }

        private void Unregister(long id)
        {
            if (!_handles.TryGetValue(id, out List<IDisposable> handles))
                return;
            _handles.Remove(id);
            foreach (IDisposable handle in handles)
                handle.Dispose();
        }

        public void Dispose()
        {
            _container.Changed -= OnChanged;
            foreach (List<IDisposable> handles in _handles.Values)
            {
                foreach (IDisposable handle in handles)
                    handle.Dispose();
            }
            _handles.Clear();
        }
    }
}
