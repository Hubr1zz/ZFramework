using System;
using System.Collections.Generic;
using CardGame.ActionQueue;
using GameFramework.Buffs;

namespace GameFramework.Buffs.ActionQueueAdapter
{
    /// <summary>只负责把存活 Buff 翻译成实体 Reactor 注册；不拥有 Buff 生命周期。</summary>
    public sealed class BuffActionQueueBinding : IDisposable
    {
        private readonly BuffContainer _container;
        private readonly ReactorRegistry _reactors;
        private readonly IReactorEntity _owner;
        private readonly BuffReactorCatalog _catalog;
        private readonly Dictionary<long, List<IDisposable>> _registrations = new();
        private bool _disposed;

        public BuffActionQueueBinding(
            BuffContainer container,
            ReactorRegistry reactors,
            IReactorEntity owner,
            BuffReactorCatalog catalog)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _reactors = reactors ?? throw new ArgumentNullException(nameof(reactors));
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

            if (!ReferenceEquals(_container.Owner, _owner))
            {
                throw new ArgumentException(
                    "BuffContainer owner and ActionQueue reactor entity must be the same object.",
                    nameof(owner));
            }

            _container.Changed += OnBuffChanged;
            foreach (BuffInstance instance in _container.Active)
                Register(instance);
        }

        private void OnBuffChanged(object sender, BuffChangedEventArgs args)
        {
            switch (args.Kind)
            {
                case BuffChangeKind.Added:
                    Register(args.Instance);
                    break;
                case BuffChangeKind.Removed:
                case BuffChangeKind.Expired:
                    Unregister(args.Instance.Id);
                    break;
            }
        }

        private void Register(BuffInstance instance)
        {
            Unregister(instance.Id);
            if (!_catalog.TryCreate(instance, out IEnumerable<BuffReactorRegistration> reactors) ||
                reactors == null)
                return;

            var handles = new List<IDisposable>();
            foreach (BuffReactorRegistration registration in reactors)
            {
                handles.Add(_reactors.RegisterForEntity(
                    _owner,
                    registration.Reactor,
                    registration.Relation));
            }
            _registrations.Add(instance.Id, handles);
        }

        private void Unregister(long instanceId)
        {
            if (!_registrations.TryGetValue(instanceId, out List<IDisposable> handles))
                return;
            _registrations.Remove(instanceId);
            foreach (IDisposable handle in handles)
                handle.Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _container.Changed -= OnBuffChanged;
            foreach (List<IDisposable> handles in _registrations.Values)
            {
                foreach (IDisposable handle in handles)
                    handle.Dispose();
            }
            _registrations.Clear();
        }
    }
}
