using System;

namespace HuntingInDarkness.GameCore.Foundation
{
    /// <summary>
    /// Rules consume randomness through this port so simulations and tests can be seeded.
    /// </summary>
    public interface IRandomSource
    {
        int Next(int minInclusive, int maxExclusive);
        double NextDouble();
    }

    public sealed class SystemRandomSource : IRandomSource
    {
        private readonly Random _random;

        public SystemRandomSource() : this(new Random()) { }

        public SystemRandomSource(int seed) : this(new Random(seed)) { }

        public SystemRandomSource(Random random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public int Next(int minInclusive, int maxExclusive) =>
            _random.Next(minInclusive, maxExclusive);

        public double NextDouble() => _random.NextDouble();
    }
}
