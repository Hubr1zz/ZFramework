using System;

namespace GameFramework.Buffs
{
    public sealed class BuffInstance
    {
        internal BuffInstance(
            long id,
            BuffDefinition definition,
            object owner,
            object source,
            int stacks,
            BuffDuration? duration)
        {
            Id = id;
            Definition = definition;
            Owner = owner;
            Source = source;
            Stacks = Math.Min(stacks, definition.MaxStacks);
            Duration = duration;
            RemainingDuration = duration?.Amount;
        }

        public long Id { get; }
        public BuffDefinition Definition { get; }
        public object Owner { get; }
        public object Source { get; }
        public int Stacks { get; internal set; }
        public BuffDuration? Duration { get; internal set; }
        public double? RemainingDuration { get; internal set; }
        public bool IsActive { get; internal set; } = true;

        public bool HasTag(string tag) => Definition.HasTag(tag);

        public override string ToString() =>
            $"{Definition.Key}#{Id} x{Stacks}";
    }
}
