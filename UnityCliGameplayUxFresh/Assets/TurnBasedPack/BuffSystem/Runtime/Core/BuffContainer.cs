using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GameFramework.Buffs
{
    /// <summary>
    /// 与战斗循环、UI 和时间来源无关的 Buff 生命周期容器。
    /// 调用方通过 Advance(clock, amount) 明确推进回合、秒或任意自定义时钟。
    /// </summary>
    public sealed class BuffContainer
    {
        private readonly List<BuffInstance> _active = new();
        private readonly ReadOnlyCollection<BuffInstance> _activeView;
        private readonly Dictionary<BuffKey, List<BuffInstance>> _byKey = new();
        private long _nextId = 1;
        private bool _notifying;

        public BuffContainer(object owner)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _activeView = _active.AsReadOnly();
        }

        public object Owner { get; }
        public IReadOnlyList<BuffInstance> Active => _activeView;

        public event EventHandler<BuffApplyingEventArgs> Applying;
        public event EventHandler<BuffRemovingEventArgs> Removing;
        public event EventHandler<BuffChangedEventArgs> Changed;

        public BuffApplyResult Apply(BuffApplyRequest request)
        {
            ThrowIfNotifying();

            var applying = new BuffApplyingEventArgs(request);
            Notify(() => Applying?.Invoke(this, applying));
            if (applying.IsRejected)
                return new BuffApplyResult(BuffApplyStatus.Rejected, null, applying.RejectReason);

            List<BuffInstance> matches = GetMatches(request.Definition.Key);
            if (matches.Count > 0 && !ReferenceEquals(matches[0].Definition, request.Definition))
            {
                throw new InvalidOperationException(
                    $"Buff key '{request.Definition.Key}' is already active with a different definition instance.");
            }

            if (matches.Count > 0 && request.Definition.MergeStrategy != null)
                return ApplyCustomMerge(matches, request);

            switch (request.Definition.StackPolicy)
            {
                case BuffStackPolicy.Independent:
                    return AddNew(request, BuffApplyStatus.Added);
                case BuffStackPolicy.Reject when matches.Count > 0:
                    return new BuffApplyResult(BuffApplyStatus.Rejected, matches[0], "Buff already exists.");
                case BuffStackPolicy.Replace when matches.Count > 0:
                    foreach (BuffInstance match in matches.ToArray())
                    {
                        if (!TryRemove(match, BuffRemovalCause.Replaced, BuffChangeKind.Removed, out string reason))
                            return new BuffApplyResult(BuffApplyStatus.Rejected, match, reason);
                    }
                    return AddNew(request, BuffApplyStatus.Replaced);
                default:
                    return matches.Count == 0
                        ? AddNew(request, BuffApplyStatus.Added)
                        : UpdateExisting(matches[0], request);
            }
        }

        public bool Remove(BuffInstance instance, BuffRemovalCause cause = BuffRemovalCause.Explicit)
        {
            ThrowIfNotifying();
            return instance != null && instance.IsActive &&
                TryRemove(instance, cause, BuffChangeKind.Removed, out _);
        }

        public int RemoveByKey(BuffKey key)
        {
            ThrowIfNotifying();
            List<BuffInstance> matches = GetMatches(key);
            int removed = 0;
            foreach (BuffInstance instance in matches.ToArray())
            {
                if (TryRemove(instance, BuffRemovalCause.Explicit, BuffChangeKind.Removed, out _))
                    removed++;
            }
            return removed;
        }

        public int RemoveByTag(string tag)
        {
            ThrowIfNotifying();
            if (string.IsNullOrWhiteSpace(tag))
                return 0;

            var matches = new List<BuffInstance>();
            foreach (BuffInstance instance in _active)
            {
                if (instance.HasTag(tag))
                    matches.Add(instance);
            }

            int removed = 0;
            foreach (BuffInstance instance in matches)
            {
                if (TryRemove(instance, BuffRemovalCause.Dispel, BuffChangeKind.Removed, out _))
                    removed++;
            }
            return removed;
        }

        public bool TryGetFirst(BuffKey key, out BuffInstance instance)
        {
            if (_byKey.TryGetValue(key, out List<BuffInstance> matches) && matches.Count > 0)
            {
                instance = matches[0];
                return true;
            }

            instance = null;
            return false;
        }

        public void Advance(BuffClock clock, double amount)
        {
            ThrowIfNotifying();
            if (amount < 0d || double.IsNaN(amount) || double.IsInfinity(amount))
                throw new ArgumentOutOfRangeException(nameof(amount), "Advance amount must be finite and non-negative.");
            if (amount == 0d)
                return;

            // 回调期间禁止重入修改，因此可以原地正序推进且不产生每帧快照分配。
            for (int index = 0; index < _active.Count;)
            {
                BuffInstance instance = _active[index];
                if (!instance.IsActive || !instance.Duration.HasValue ||
                    !instance.Duration.Value.Clock.Equals(clock))
                {
                    index++;
                    continue;
                }

                int oldStacks = instance.Stacks;
                double? oldRemaining = instance.RemainingDuration;
                instance.RemainingDuration = Math.Max(0d, instance.RemainingDuration.Value - amount);
                if (instance.RemainingDuration <= 0d)
                {
                    if (!TryRemove(instance, BuffRemovalCause.Expired, BuffChangeKind.Expired, out _))
                        index++;
                }
                else
                {
                    RaiseChanged(BuffChangeKind.Updated, instance, oldStacks, oldRemaining);
                    index++;
                }
            }
        }

        private BuffApplyResult AddNew(BuffApplyRequest request, BuffApplyStatus status)
        {
            var instance = new BuffInstance(
                _nextId++,
                request.Definition,
                Owner,
                request.Source,
                request.Stacks,
                request.Duration);

            _active.Add(instance);
            if (!_byKey.TryGetValue(instance.Definition.Key, out List<BuffInstance> matches))
            {
                matches = new List<BuffInstance>();
                _byKey.Add(instance.Definition.Key, matches);
            }
            matches.Add(instance);
            RaiseChanged(BuffChangeKind.Added, instance, 0, null);
            return new BuffApplyResult(status, instance);
        }

        private BuffApplyResult UpdateExisting(BuffInstance instance, BuffApplyRequest request)
        {
            int oldStacks = instance.Stacks;
            double? oldRemaining = instance.RemainingDuration;
            BuffStackPolicy policy = request.Definition.StackPolicy;

            if (policy == BuffStackPolicy.Stack || policy == BuffStackPolicy.StackAndRefreshDuration)
                instance.Stacks = Math.Min(instance.Definition.MaxStacks, instance.Stacks + request.Stacks);

            if (policy == BuffStackPolicy.RefreshDuration || policy == BuffStackPolicy.StackAndRefreshDuration)
            {
                instance.Duration = request.Duration;
                instance.RemainingDuration = request.Duration?.Amount;
            }

            RaiseChanged(BuffChangeKind.Updated, instance, oldStacks, oldRemaining);
            return new BuffApplyResult(BuffApplyStatus.Updated, instance);
        }

        private BuffApplyResult ApplyCustomMerge(
            List<BuffInstance> matches,
            BuffApplyRequest request)
        {
            BuffInstance existing = matches[0];
            BuffMergeResult merge = request.Definition.MergeStrategy.Merge(existing, request);
            switch (merge.Action)
            {
                case BuffMergeAction.Reject:
                    return new BuffApplyResult(BuffApplyStatus.Rejected, existing, merge.Reason);
                case BuffMergeAction.AddIndependent:
                    return AddNew(request, BuffApplyStatus.Added);
                case BuffMergeAction.ReplaceExisting:
                    foreach (BuffInstance match in matches.ToArray())
                    {
                        if (!TryRemove(match, BuffRemovalCause.Replaced, BuffChangeKind.Removed, out string reason))
                            return new BuffApplyResult(BuffApplyStatus.Rejected, match, reason);
                    }
                    return AddNew(request, BuffApplyStatus.Replaced);
                case BuffMergeAction.UpdateExisting:
                    int oldStacks = existing.Stacks;
                    double? oldRemaining = existing.RemainingDuration;
                    existing.Stacks = Math.Min(existing.Definition.MaxStacks, merge.Stacks);
                    existing.Duration = merge.Duration;
                    existing.RemainingDuration = merge.Duration?.Amount;
                    RaiseChanged(BuffChangeKind.Updated, existing, oldStacks, oldRemaining);
                    return new BuffApplyResult(BuffApplyStatus.Updated, existing);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private bool TryRemove(
            BuffInstance instance,
            BuffRemovalCause cause,
            BuffChangeKind kind,
            out string reason)
        {
            if (!_active.Contains(instance))
            {
                reason = "Buff is not active in this container.";
                return false;
            }

            var removing = new BuffRemovingEventArgs(instance, cause);
            Notify(() => Removing?.Invoke(this, removing));
            if (removing.IsRejected)
            {
                reason = removing.RejectReason;
                return false;
            }

            reason = string.Empty;
            return RemoveInternal(instance, kind);
        }

        private bool RemoveInternal(BuffInstance instance, BuffChangeKind kind)
        {
            if (!_active.Remove(instance))
                return false;

            if (_byKey.TryGetValue(instance.Definition.Key, out List<BuffInstance> matches))
            {
                matches.Remove(instance);
                if (matches.Count == 0)
                    _byKey.Remove(instance.Definition.Key);
            }

            instance.IsActive = false;
            RaiseChanged(kind, instance, instance.Stacks, instance.RemainingDuration);
            return true;
        }

        private List<BuffInstance> GetMatches(BuffKey key)
        {
            return _byKey.TryGetValue(key, out List<BuffInstance> matches)
                ? new List<BuffInstance>(matches)
                : new List<BuffInstance>();
        }

        private void RaiseChanged(
            BuffChangeKind kind,
            BuffInstance instance,
            int previousStacks,
            double? previousRemaining)
        {
            var args = new BuffChangedEventArgs(kind, instance, previousStacks, previousRemaining);
            Notify(() => Changed?.Invoke(this, args));
        }

        private void Notify(Action notification)
        {
            _notifying = true;
            try
            {
                notification();
            }
            finally
            {
                _notifying = false;
            }
        }

        private void ThrowIfNotifying()
        {
            if (_notifying)
            {
                throw new InvalidOperationException(
                    "BuffContainer cannot be mutated from its lifecycle callbacks. " +
                    "Schedule a later operation through the host game loop instead.");
            }
        }
    }
}
