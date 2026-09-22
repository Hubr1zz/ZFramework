using System;

namespace HuntingInDarkness.GameCore.Foundation
{
    /// <summary>
    /// Exported state for a <see cref="StatefulRandomSource"/>.
    /// </summary>
    public readonly struct StatefulRandomState
    {
        public StatefulRandomState(uint value)
        {
            if (value == 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Random state must be non-zero.");

            Value = value;
        }

        public uint Value { get; }
    }

    /// <summary>
    /// Optional extension for random sources whose sequence can be persisted and restored.
    /// </summary>
    public interface IStatefulRandomSource
    {
        StatefulRandomState ExportState();
        void RestoreState(StatefulRandomState state);
    }

    /// <summary>
    /// Small deterministic xorshift source with an explicit, portable state.
    /// </summary>
    public sealed class StatefulRandomSource : IRandomSource, IStatefulRandomSource
    {
        private uint state;

        public StatefulRandomSource(int seed)
        {
            uint initialState = unchecked((uint)seed);
            state = initialState == 0 ? 0x9E3779B9u : initialState;
        }

        public StatefulRandomSource(StatefulRandomState initialState)
        {
            EnsureValidState(initialState.Value);
            state = initialState.Value;
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            if (minInclusive > maxExclusive)
                throw new ArgumentOutOfRangeException(nameof(minInclusive));

            if (minInclusive == maxExclusive)
                return minInclusive;

            ulong range = (ulong)((long)maxExclusive - minInclusive);
            uint value = NextUInt();
            ulong offset = ((ulong)value * range) >> 32;
            return (int)((long)minInclusive + (long)offset);
        }

        public double NextDouble()
        {
            ulong value = ((ulong)NextUInt() << 21) | (NextUInt() & 0x1FFFFFu);
            return value / 9007199254740992d;
        }

        public StatefulRandomState ExportState() => new StatefulRandomState(state);

        public void RestoreState(StatefulRandomState restoredState)
        {
            EnsureValidState(restoredState.Value);
            state = restoredState.Value;
        }

        private uint NextUInt()
        {
            uint value = state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            state = value;
            return value;
        }

        private static void EnsureValidState(uint value)
        {
            if (value == 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Random state must be non-zero.");
        }
    }
}
