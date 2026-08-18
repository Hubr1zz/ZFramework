using System;
using System.Collections.Generic;
using CardGame.ActionQueue;
using GameFramework.Buffs;

namespace GameFramework.Buffs.ActionQueueAdapter
{
    public readonly struct BuffReactorRegistration
    {
        public BuffReactorRegistration(
            IGameActionReactor reactor,
            ReactorRelation relation = ReactorRelation.Either)
        {
            Reactor = reactor ?? throw new ArgumentNullException(nameof(reactor));
            Relation = relation;
        }

        public IGameActionReactor Reactor { get; }
        public ReactorRelation Relation { get; }
    }

    /// <summary>
    /// BuffKey 到 ActionQueue Reactor 的外部映射。Factory 在 Buff 添加时调用一次；
    /// Reactor 应读取传入 BuffInstance 的实时层数，不应把可变层数复制进自身。
    /// BuffDefinition 与 BuffSystem.Runtime 不引用 ActionQueue。
    /// </summary>
    public sealed class BuffReactorCatalog
    {
        private readonly Dictionary<BuffKey, Func<BuffInstance, IEnumerable<BuffReactorRegistration>>>
            _factories = new();

        public BuffReactorCatalog Register(
            BuffKey key,
            Func<BuffInstance, IEnumerable<BuffReactorRegistration>> factory)
        {
            _factories[key] = factory ?? throw new ArgumentNullException(nameof(factory));
            return this;
        }

        internal bool TryCreate(
            BuffInstance instance,
            out IEnumerable<BuffReactorRegistration> registrations)
        {
            if (_factories.TryGetValue(instance.Definition.Key, out var factory))
            {
                registrations = factory(instance);
                return true;
            }

            registrations = null;
            return false;
        }
    }
}
