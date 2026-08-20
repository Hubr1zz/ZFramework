using System.Collections.Generic;
using System.Linq;
using HuntingInDarkness.GameCore.Board;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Hunt;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests
{
    public sealed class HuntMapRulesTests
    {
        [Test]
        public void Generate_PreservesThePlacedAdjacentGroup()
        {
            var grouped = new HuntTileDefinition("StatuePlains", 1, true, 4, 0);
            var regular = new HuntTileDefinition("Ruins", 1, false, 1, 0);
            var starting = new HuntTileDefinition("Starting", 1, false, 1, 0);
            var generator = new HuntMapGenerator(new FirstGroupThenRegularRandom(), 2);

            HuntMapState map = generator.Generate(new[] { grouped, regular }, starting);
            List<GridPosition> groupedPositions = map.Tiles.Values.Where(tile => tile.Definition == grouped).Select(tile => tile.Position).ToList();

            Assert.That(groupedPositions, Has.Count.EqualTo(4));
            Assert.That(IsConnected(groupedPositions), Is.True);
            Assert.That(map.Tiles, Has.Count.EqualTo(19));
            Assert.That(map.Tiles[GridPosition.Zero].Definition, Is.SameAs(starting));
        }

        [Test]
        public void Generate_ReservesStartingTileBeforePlacingLargeGroup()
        {
            var grouped = new HuntTileDefinition("StatuePlains", 1, true, 7, 0);
            var starting = new HuntTileDefinition("Starting", 1, false, 1, 0);
            var generator = new HuntMapGenerator(new FirstGroupThenRegularRandom(), 1);

            HuntMapState map = generator.Generate(new[] { grouped }, starting);

            Assert.That(map.Tiles, Has.Count.EqualTo(7));
            Assert.That(map.Tiles[GridPosition.Zero].Definition, Is.SameAs(starting));
            Assert.That(map.Tiles.Values.Count(tile => tile.Definition == grouped), Is.EqualTo(6));
        }

        [Test]
        public void Generate_AllowsConfiguredDispersedGroups()
        {
            var grouped = new HuntTileDefinition("Markers", 1, true, 2, 0, false);
            var regular = new HuntTileDefinition("Ruins", 1, false, 1, 0);
            var generator = new HuntMapGenerator(new FirstGroupThenRegularRandom(true), 2);

            HuntMapState map = generator.Generate(new[] { grouped, regular }, null);
            List<GridPosition> groupedPositions = map.Tiles.Values.Where(tile => tile.Definition == grouped).Select(tile => tile.Position).ToList();

            Assert.That(groupedPositions, Has.Count.EqualTo(2));
            Assert.That(HuntNavigationState.HexDistance(groupedPositions[0], groupedPositions[1]), Is.GreaterThan(1));
        }

        private static bool IsConnected(IReadOnlyCollection<GridPosition> positions)
        {
            if (positions.Count == 0) return true;
            var remaining = new HashSet<GridPosition>(positions);
            var pending = new Queue<GridPosition>();
            GridPosition first = remaining.First();
            remaining.Remove(first);
            pending.Enqueue(first);
            while (pending.Count > 0)
            {
                foreach (GridPosition neighbor in HuntMapGenerator.GetNeighbors(pending.Dequeue()))
                    if (remaining.Remove(neighbor))
                        pending.Enqueue(neighbor);
            }
            return remaining.Count == 0;
        }

        private sealed class FirstGroupThenRegularRandom : IRandomSource
        {
            private readonly bool chooseLastGroupPosition;
            private int calls;

            public FirstGroupThenRegularRandom(bool chooseLastGroupPosition = false)
            {
                this.chooseLastGroupPosition = chooseLastGroupPosition;
            }

            public int Next(int minInclusive, int maxExclusive)
            {
                calls++;
                if (calls == 1) return minInclusive;
                if (calls <= 4) return chooseLastGroupPosition ? maxExclusive - 1 : minInclusive;
                return maxExclusive - 1;
            }

            public double NextDouble() => 0d;
        }
    }
}
