using System;
using System.Collections.Generic;

namespace CardGame.ActionQueue
{
    /// <summary>
    /// Reactor 来源：
    /// 1) Global：规则、场景或战斗级常驻效果；
    /// 2) Entity：只路由给 Action 的 Source/Target，例如敌人的荆棘；
    /// 3) Chain：只存在于当前根流程；
    /// 4) Action.LocalReactors：只监听一个 Action 实例。
    /// </summary>
    public sealed class ReactorRegistry
    {
        private readonly List<Registration> _global = new();
        private readonly Dictionary<IReactorEntity, List<Registration>> _entity =
            new(ReferenceComparer<IReactorEntity>.Instance);

        private long _nextOrder;

        internal event Action Changed;

        public IDisposable RegisterGlobal(IGameActionReactor reactor)
        {
            return Add(_global, reactor, ReactorRelation.Any);
        }

        public IDisposable RegisterForEntity(
            IReactorEntity entity,
            IGameActionReactor reactor,
            ReactorRelation relation = ReactorRelation.Either)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (!_entity.TryGetValue(entity, out List<Registration> list))
            {
                list = new List<Registration>();
                _entity.Add(entity, list);
            }

            return Add(list, reactor, relation);
        }

        internal List<ReactorInvocation> Collect(
            GameAction action,
            ReactionTiming timing,
            ActionOutcome? outcome,
            long chainId,
            long actionId,
            IReadOnlyList<IGameActionReactor> chainReactors,
            IReadOnlyList<IGameActionReactor> scopedReactors,
            ReactionGateRegistry gates)
        {
            var candidates = new List<Candidate>();

            AddRegistrations(candidates, _global, ReactorRelation.Any, null, -1);
            AddAdHoc(candidates, chainReactors, ReactorRelation.Any, 1_000_000_000L);
            AddAdHoc(candidates, scopedReactors, ReactorRelation.Any, 1_500_000_000L);
            AddAdHoc(candidates, action.LocalReactors, ReactorRelation.Any, 2_000_000_000L);

            if (action is ISourceAction sourceAction && sourceAction.Source != null &&
                _entity.TryGetValue(sourceAction.Source, out List<Registration> source))
            {
                AddRegistrations(candidates, source, ReactorRelation.Source, sourceAction.Source, -1);
            }

            if (action is IMultiTargetAction multiTarget)
            {
                IReadOnlyList<IReactorEntity> targets = multiTarget.Targets;
                if (targets != null)
                {
                    for (int i = 0; i < targets.Count; i++)
                    {
                        IReactorEntity entity = targets[i];
                        if (entity != null && _entity.TryGetValue(entity, out List<Registration> target))
                        {
                            AddRegistrations(
                                candidates,
                                target,
                                ReactorRelation.Target,
                                entity,
                                i);
                        }
                    }
                }
            }
            else if (action is ITargetAction targetAction && targetAction.Target != null &&
                     _entity.TryGetValue(targetAction.Target, out List<Registration> target))
            {
                AddRegistrations(candidates, target, ReactorRelation.Target, targetAction.Target, 0);
            }

            candidates.Sort(new CandidateComparer(action.GetType()));
            var invocations = new List<ReactorInvocation>();

            foreach (Candidate candidate in candidates)
            {
                IGameActionReactor reactor = candidate.Reactor;
                if (reactor.Timing != timing)
                    continue;

                if (!reactor.ObservedActionType.IsAssignableFrom(action.GetType()))
                    continue;

                var context = new ReactionContext(
                    action,
                    chainId,
                    actionId,
                    candidate.Relation,
                    candidate.MatchedEntity,
                    candidate.TargetIndex,
                    outcome);

                var gateContext = new ReactionGateContext(
                    action,
                    reactor,
                    timing,
                    candidate.Relation,
                    candidate.MatchedEntity,
                    chainId,
                    actionId,
                    outcome);
                if (gates != null && !gates.Allows(gateContext, out _))
                    continue;

                if (!reactor.Matches(context))
                    continue;

                invocations.Add(new ReactorInvocation(reactor, context));
            }

            return invocations;
        }

        #region Debug Support

        internal List<string> GetDebugRegistrationDescriptions()
        {
            var result = new List<string>();
            foreach (Registration registration in _global)
            {
                if (!registration.IsDisposed)
                    result.Add($"Global | {registration.Reactor.DebugName} | {registration.Reactor.Timing}");
            }

            foreach (KeyValuePair<IReactorEntity, List<Registration>> pair in _entity)
            {
                foreach (Registration registration in pair.Value)
                {
                    if (!registration.IsDisposed)
                    {
                        result.Add(
                            $"Entity:{pair.Key.ReactorName} [{registration.Relation}] | " +
                            $"{registration.Reactor.DebugName} | {registration.Reactor.Timing}");
                    }
                }
            }

            return result;
        }

        #endregion

        private IDisposable Add(
            List<Registration> list,
            IGameActionReactor reactor,
            ReactorRelation relation)
        {
            if (reactor == null)
                throw new ArgumentNullException(nameof(reactor));

            var registration = new Registration(this, reactor, relation, _nextOrder++, list);
            list.Add(registration);
            NotifyChanged();
            return registration;
        }

        private void NotifyChanged()
        {
            Changed?.Invoke();
        }

        private static void AddRegistrations(
            List<Candidate> candidates,
            List<Registration> registrations,
            ReactorRelation actualRelation,
            IReactorEntity matchedEntity,
            int targetIndex)
        {
            foreach (Registration registration in registrations)
            {
                if (registration.IsDisposed)
                    continue;

                if ((registration.Relation & actualRelation) == 0 &&
                    (registration.Relation & ReactorRelation.Any) == 0)
                    continue;

                // Source 与 Target 是同一实体时，同一个注册只触发一次，但保留双重关系信息。
                bool merged = false;
                for (int i = 0; i < candidates.Count; i++)
                {
                    Candidate existing = candidates[i];
                    if (existing.Order != registration.Order ||
                        !ReferenceEquals(existing.Reactor, registration.Reactor))
                        continue;

                    candidates[i] = new Candidate(
                        existing.Reactor,
                        existing.Relation | actualRelation,
                        existing.Order,
                        existing.MatchedEntity ?? matchedEntity,
                        MergeTargetIndex(existing.TargetIndex, targetIndex));
                    merged = true;
                    break;
                }

                if (merged)
                    continue;

                candidates.Add(new Candidate(
                    registration.Reactor,
                    actualRelation,
                    registration.Order,
                    matchedEntity,
                    targetIndex));
            }
        }

        private static int MergeTargetIndex(int existing, int incoming)
        {
            if (existing < 0)
                return incoming;
            if (incoming < 0)
                return existing;
            return Math.Min(existing, incoming);
        }

        private static void AddAdHoc(
            List<Candidate> candidates,
            IReadOnlyList<IGameActionReactor> reactors,
            ReactorRelation relation,
            long orderBase)
        {
            if (reactors == null)
                return;

            for (int i = 0; i < reactors.Count; i++)
            {
                if (reactors[i] != null)
                {
                    candidates.Add(new Candidate(
                        reactors[i],
                        relation,
                        orderBase + i,
                        null,
                        -1));
                }
            }
        }

        private sealed class Registration : IDisposable
        {
            private readonly ReactorRegistry _registry;
            private readonly List<Registration> _owner;

            public Registration(
                ReactorRegistry registry,
                IGameActionReactor reactor,
                ReactorRelation relation,
                long order,
                List<Registration> owner)
            {
                _registry = registry;
                Reactor = reactor;
                Relation = relation;
                Order = order;
                _owner = owner;
            }

            public IGameActionReactor Reactor { get; }
            public ReactorRelation Relation { get; }
            public long Order { get; }
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                if (IsDisposed)
                    return;

                IsDisposed = true;
                _owner.Remove(this);
                _registry.NotifyChanged();
            }
        }

        private readonly struct Candidate
        {
            public Candidate(
                IGameActionReactor reactor,
                ReactorRelation relation,
                long order,
                IReactorEntity matchedEntity,
                int targetIndex)
            {
                Reactor = reactor;
                Relation = relation;
                Order = order;
                MatchedEntity = matchedEntity;
                TargetIndex = targetIndex;
            }

            public IGameActionReactor Reactor { get; }
            public ReactorRelation Relation { get; }
            public long Order { get; }
            public IReactorEntity MatchedEntity { get; }
            public int TargetIndex { get; }
        }

        internal sealed class ReactorInvocation
        {
            public ReactorInvocation(IGameActionReactor reactor, ReactionContext context)
            {
                Reactor = reactor;
                Context = context;
            }

            public IGameActionReactor Reactor { get; }
            public ReactionContext Context { get; }

            public void Invoke(ReactionResponse response)
            {
                Reactor.React(Context, response);
            }
        }

        private sealed class CandidateComparer : IComparer<Candidate>
        {
            private readonly Type _actionType;
            private readonly Dictionary<Type, int> _distanceCache = new();

            public CandidateComparer(Type actionType)
            {
                _actionType = actionType;
            }

            public int Compare(Candidate x, Candidate y)
            {
                // 更具体的 Action 监听器总是先于其父类监听器。
                // Priority 只在相同继承层级内排序，最后才使用稳定注册顺序。
                int specificity = GetDistance(x.Reactor.ObservedActionType)
                    .CompareTo(GetDistance(y.Reactor.ObservedActionType));
                if (specificity != 0)
                    return specificity;

                int priority = y.Reactor.Priority.CompareTo(x.Reactor.Priority);
                return priority != 0 ? priority : x.Order.CompareTo(y.Order);
            }

            private int GetDistance(Type observed)
            {
                if (_distanceCache.TryGetValue(observed, out int distance))
                    return distance;
                distance = GetTypeDistance(_actionType, observed);
                _distanceCache.Add(observed, distance);
                return distance;
            }

            private static int GetTypeDistance(Type concrete, Type observed)
            {
                if (concrete == observed)
                    return 0;

                int distance = 0;
                for (Type current = concrete; current != null; current = current.BaseType)
                {
                    if (current == observed)
                        return distance;
                    distance++;
                }

                // GameAction 通常使用类继承；接口监听仍保证在类层级之后稳定执行。
                return observed.IsInterface && observed.IsAssignableFrom(concrete)
                    ? 10_000
                    : int.MaxValue;
            }
        }

        private sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
        {
            public static readonly ReferenceComparer<T> Instance = new();

            public bool Equals(T x, T y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
