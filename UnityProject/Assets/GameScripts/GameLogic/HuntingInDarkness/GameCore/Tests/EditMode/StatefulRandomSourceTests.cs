using HuntingInDarkness.GameCore.Foundation;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests
{
    public sealed class StatefulRandomSourceTests
    {
        [Test]
        public void ExportAndRestore_ReplaysIntSequence()
        {
            var random = new StatefulRandomSource(12345);
            random.Next(0, 100);
            StatefulRandomState state = random.ExportState();
            int expectedFirst = random.Next(-20, 40);
            int expectedSecond = random.Next(0, 1000);

            random.RestoreState(state);

            Assert.That(random.Next(-20, 40), Is.EqualTo(expectedFirst));
            Assert.That(random.Next(0, 1000), Is.EqualTo(expectedSecond));
        }

        [Test]
        public void ExportAndRestore_ReplaysDoubleSequence()
        {
            var random = new StatefulRandomSource(67890);
            StatefulRandomState state = random.ExportState();
            double expectedFirst = random.NextDouble();
            double expectedSecond = random.NextDouble();

            random.RestoreState(state);

            Assert.That(random.NextDouble(), Is.EqualTo(expectedFirst));
            Assert.That(random.NextDouble(), Is.EqualTo(expectedSecond));
        }

        [Test]
        public void StateConstructor_ContinuesExportedSequence()
        {
            var original = new StatefulRandomSource(42);
            original.Next(1, 7);
            StatefulRandomState state = original.ExportState();
            var restored = new StatefulRandomSource(state);

            Assert.That(restored.Next(1, 7), Is.EqualTo(original.Next(1, 7)));
            Assert.That(restored.NextDouble(), Is.EqualTo(original.NextDouble()));
        }

        [Test]
        public void Constructor_WithZeroSeedStillCreatesValidState()
        {
            var random = new StatefulRandomSource(0);

            Assert.That(random.ExportState().Value, Is.Not.EqualTo(0u));
            Assert.That(random.NextDouble(), Is.InRange(0d, 1d));
        }
    }
}
