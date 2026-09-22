using System;
using System.Collections.Generic;

namespace GameFramework.Buffs
{
    public readonly struct BuffKey : IEquatable<BuffKey>
    {
        public BuffKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Buff key cannot be empty.", nameof(value));

            Value = value;
        }

        public string Value { get; }

        public bool Equals(BuffKey other) =>
            StringComparer.Ordinal.Equals(Value, other.Value);

        public override bool Equals(object obj) => obj is BuffKey other && Equals(other);

        public override int GetHashCode() =>
            Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);

        public override string ToString() => Value ?? string.Empty;

        public static bool operator ==(BuffKey left, BuffKey right) => left.Equals(right);
        public static bool operator !=(BuffKey left, BuffKey right) => !left.Equals(right);
    }

    public readonly struct BuffClock : IEquatable<BuffClock>
    {
        public BuffClock(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Buff clock cannot be empty.", nameof(value));

            Value = value;
        }

        public string Value { get; }

        public bool Equals(BuffClock other) =>
            StringComparer.Ordinal.Equals(Value, other.Value);

        public override bool Equals(object obj) => obj is BuffClock other && Equals(other);
        public override int GetHashCode() =>
            Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
    }

    public readonly struct BuffDuration
    {
        public BuffDuration(BuffClock clock, double amount)
        {
            if (amount <= 0d || double.IsNaN(amount) || double.IsInfinity(amount))
                throw new ArgumentOutOfRangeException(nameof(amount), "Duration must be finite and positive.");

            Clock = clock;
            Amount = amount;
        }

        public BuffClock Clock { get; }
        public double Amount { get; }
    }

    public enum BuffStackPolicy
    {
        Independent,
        Reject,
        Stack,
        RefreshDuration,
        StackAndRefreshDuration,
        Replace
    }

    public enum BuffChangeKind
    {
        Added,
        Updated,
        Removed,
        Expired
    }

    public enum BuffRemovalCause
    {
        Explicit,
        Dispel,
        Replaced,
        Expired
    }

    public enum BuffMergeAction
    {
        Reject,
        AddIndependent,
        UpdateExisting,
        ReplaceExisting
    }

    public readonly struct BuffMergeResult
    {
        public BuffMergeResult(
            BuffMergeAction action,
            int stacks = 1,
            BuffDuration? duration = null,
            string reason = null)
        {
            if (stacks < 1)
                throw new ArgumentOutOfRangeException(nameof(stacks));
            Action = action;
            Stacks = stacks;
            Duration = duration;
            Reason = reason ?? string.Empty;
        }

        public BuffMergeAction Action { get; }
        public int Stacks { get; }
        public BuffDuration? Duration { get; }
        public string Reason { get; }
    }

    public interface IBuffMergeStrategy
    {
        BuffMergeResult Merge(BuffInstance existing, BuffApplyRequest incoming);
    }

    public enum BuffApplyStatus
    {
        Added,
        Updated,
        Replaced,
        Rejected
    }

    public sealed class BuffDefinition
    {
        private readonly HashSet<string> _tags;
        private readonly string[] _tagView;

        public BuffDefinition(
            BuffKey key,
            int maxStacks = 1,
            BuffStackPolicy stackPolicy = BuffStackPolicy.StackAndRefreshDuration,
            BuffDuration? defaultDuration = null,
            IEnumerable<string> tags = null,
            IBuffMergeStrategy mergeStrategy = null)
        {
            if (maxStacks < 1)
                throw new ArgumentOutOfRangeException(nameof(maxStacks));

            Key = key;
            MaxStacks = maxStacks;
            StackPolicy = stackPolicy;
            DefaultDuration = defaultDuration;
            MergeStrategy = mergeStrategy;
            _tags = new HashSet<string>(StringComparer.Ordinal);

            if (tags == null)
                return;

            foreach (string tag in tags)
            {
                if (!string.IsNullOrWhiteSpace(tag))
                    _tags.Add(tag);
            }

            _tagView = new string[_tags.Count];
            _tags.CopyTo(_tagView);
        }

        public BuffKey Key { get; }
        public int MaxStacks { get; }
        public BuffStackPolicy StackPolicy { get; }
        public BuffDuration? DefaultDuration { get; }
        public IBuffMergeStrategy MergeStrategy { get; }
        public IReadOnlyCollection<string> Tags => _tagView ?? Array.Empty<string>();

        public bool HasTag(string tag) => tag != null && _tags.Contains(tag);
    }

    public readonly struct BuffApplyRequest
    {
        public BuffApplyRequest(
            BuffDefinition definition,
            object source = null,
            int stacks = 1,
            BuffDuration? durationOverride = null)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (stacks < 1)
                throw new ArgumentOutOfRangeException(nameof(stacks));

            Source = source;
            Stacks = stacks;
            DurationOverride = durationOverride;
        }

        public BuffDefinition Definition { get; }
        public object Source { get; }
        public int Stacks { get; }
        public BuffDuration? DurationOverride { get; }
        public BuffDuration? Duration => DurationOverride ?? Definition.DefaultDuration;
    }

    public readonly struct BuffApplyResult
    {
        public BuffApplyResult(BuffApplyStatus status, BuffInstance instance, string reason = null)
        {
            Status = status;
            Instance = instance;
            Reason = reason ?? string.Empty;
        }

        public BuffApplyStatus Status { get; }
        public BuffInstance Instance { get; }
        public string Reason { get; }
        public bool IsAccepted => Status != BuffApplyStatus.Rejected;
    }

    public sealed class BuffApplyingEventArgs : EventArgs
    {
        internal BuffApplyingEventArgs(BuffApplyRequest request)
        {
            Request = request;
        }

        public BuffApplyRequest Request { get; }
        public bool IsRejected { get; private set; }
        public string RejectReason { get; private set; } = string.Empty;

        public void Reject(string reason)
        {
            IsRejected = true;
            RejectReason = reason ?? string.Empty;
        }
    }

    public sealed class BuffChangedEventArgs : EventArgs
    {
        internal BuffChangedEventArgs(
            BuffChangeKind kind,
            BuffInstance instance,
            int previousStacks,
            double? previousRemainingDuration)
        {
            Kind = kind;
            Instance = instance;
            PreviousStacks = previousStacks;
            PreviousRemainingDuration = previousRemainingDuration;
        }

        public BuffChangeKind Kind { get; }
        public BuffInstance Instance { get; }
        public int PreviousStacks { get; }
        public double? PreviousRemainingDuration { get; }
    }

    public sealed class BuffRemovingEventArgs : EventArgs
    {
        internal BuffRemovingEventArgs(BuffInstance instance, BuffRemovalCause cause)
        {
            Instance = instance;
            Cause = cause;
        }

        public BuffInstance Instance { get; }
        public BuffRemovalCause Cause { get; }
        public bool IsRejected { get; private set; }
        public string RejectReason { get; private set; } = string.Empty;

        public void Reject(string reason)
        {
            IsRejected = true;
            RejectReason = reason ?? string.Empty;
        }
    }
}
