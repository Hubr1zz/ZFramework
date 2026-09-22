using System;

namespace GameFramework.Buffs
{
    public enum TriggerExhaustionPolicy
    {
        AnyChannel,
        AllChannels
    }

    /// <summary>与 Buff、ActionQueue 均无关的多通道触发次数状态。</summary>
    public sealed class ChargeTrigger
    {
        private readonly int[] _remaining;

        public ChargeTrigger(
            TriggerExhaustionPolicy exhaustionPolicy,
            params int[] charges)
        {
            if (charges == null || charges.Length == 0)
                throw new ArgumentException("At least one trigger channel is required.", nameof(charges));

            _remaining = new int[charges.Length];
            for (int i = 0; i < charges.Length; i++)
            {
                if (charges[i] < 0)
                    throw new ArgumentOutOfRangeException(nameof(charges));
                _remaining[i] = charges[i];
            }

            ExhaustionPolicy = exhaustionPolicy;
        }

        public TriggerExhaustionPolicy ExhaustionPolicy { get; }
        public int ChannelCount => _remaining.Length;

        public bool IsExhausted
        {
            get
            {
                bool any = false;
                bool all = true;
                foreach (int value in _remaining)
                {
                    any |= value <= 0;
                    all &= value <= 0;
                }
                return ExhaustionPolicy == TriggerExhaustionPolicy.AnyChannel ? any : all;
            }
        }

        public int Remaining(int channel)
        {
            ValidateChannel(channel);
            return _remaining[channel];
        }

        public bool TryConsume(int channel = 0, int amount = 1)
        {
            ValidateChannel(channel);
            if (amount < 1)
                throw new ArgumentOutOfRangeException(nameof(amount));
            if (_remaining[channel] <= 0)
                return false;

            _remaining[channel] = Math.Max(0, _remaining[channel] - amount);
            return true;
        }

        private void ValidateChannel(int channel)
        {
            if (channel < 0 || channel >= _remaining.Length)
                throw new ArgumentOutOfRangeException(nameof(channel));
        }
    }
}
