using System;
using System.Collections.Generic;
using HuntingInDarkness.GameCore.Board;
using HuntingInDarkness.GameCore.Combat;
using HuntingInDarkness.GameCore.Foundation;

namespace HuntingInDarkness.GameCore.Hunt
{
    public sealed class HuntMapGenerator
    {
        private static readonly GridPosition[] Directions =
        {
            new GridPosition(1, 0), new GridPosition(-1, 0),
            new GridPosition(0, 1), new GridPosition(0, -1),
            new GridPosition(1, -1), new GridPosition(-1, 1)
        };

        private readonly IRandomSource _random;
        private readonly int _radius;

        public HuntMapGenerator(IRandomSource random, int radius)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _radius = radius;
        }

        public HuntMapState Generate(
            IReadOnlyList<HuntTileDefinition> pool,
            HuntTileDefinition startingTile)
        {
            var map = new HuntMapState();
            List<GridPosition> allPositions = GetRadialPositions(_radius);

            foreach (GridPosition position in allPositions)
            {
                HuntTileDefinition definition = position == GridPosition.Zero && startingTile != null
                    ? startingTile
                    : PickTile(pool);

                if (definition != null && definition.SpawnInGroup && definition.GroupSize > 1)
                {
                    PlaceGroup(map, position, definition, allPositions, definition.GroupSize);
                    continue;
                }
                map.Tiles[position] = new HuntTileState(position, definition);
            }

            if (!map.Tiles.ContainsKey(GridPosition.Zero))
                map.Tiles[GridPosition.Zero] = new HuntTileState(GridPosition.Zero, startingTile);
            map.Tiles[GridPosition.Zero].Visibility = HuntTileVisibility.Revealed;

            foreach (GridPosition neighbor in GetNeighbors(GridPosition.Zero))
                if (map.Tiles.TryGetValue(neighbor, out HuntTileState tile) &&
                    tile.Visibility == HuntTileVisibility.Locked)
                    tile.Visibility = HuntTileVisibility.Interactable;

            PlaceBossEncounter(map);
            return map;
        }

        public static List<GridPosition> Reveal(HuntMapState map, GridPosition position)
        {
            if (!RevealOnly(map, position)) return new List<GridPosition>();
            return UnlockNeighbors(map, position);
        }

        public static bool RevealOnly(HuntMapState map, GridPosition position)
        {
            if (!map.Tiles.TryGetValue(position, out HuntTileState tile) || tile.Visibility != HuntTileVisibility.Interactable) return false;
            tile.Visibility = HuntTileVisibility.Revealed;
            return true;
        }

        public static List<GridPosition> UnlockNeighbors(HuntMapState map, GridPosition position)
        {
            if (!map.Tiles.TryGetValue(position, out HuntTileState tile) || tile.Visibility != HuntTileVisibility.Revealed) return new List<GridPosition>();
            var newlyInteractable = new List<GridPosition>();
            foreach (GridPosition neighbor in GetNeighbors(position))
            {
                if (!map.Tiles.TryGetValue(neighbor, out HuntTileState adjacent) ||
                    adjacent.Visibility != HuntTileVisibility.Locked) continue;
                adjacent.Visibility = HuntTileVisibility.Interactable;
                newlyInteractable.Add(neighbor);
            }
            return newlyInteractable;
        }

        public static List<GridPosition> GetNeighbors(GridPosition position)
        {
            var result = new List<GridPosition>(Directions.Length);
            foreach (GridPosition direction in Directions)
                result.Add(position + direction);
            return result;
        }

        private HuntTileDefinition PickTile(IReadOnlyList<HuntTileDefinition> pool)
        {
            if (pool == null || pool.Count == 0) return null;
            List<HuntTileDefinition> drawn = WeightedSelection.DrawWithoutReplacement(
                pool, 1, tile => Math.Max(1, tile?.SpawnWeight ?? 0), _random);
            return drawn.Count > 0 ? drawn[0] : pool[0];
        }

        private void PlaceGroup(
            HuntMapState map,
            GridPosition origin,
            HuntTileDefinition definition,
            List<GridPosition> allPositions,
            int count)
        {
            var placed = new List<GridPosition> { origin };
            map.Tiles[origin] = new HuntTileState(origin, definition);
            for (int i = 1; i < count; i++)
            {
                var candidates = new List<GridPosition>();
                foreach (GridPosition position in placed)
                foreach (GridPosition neighbor in GetNeighbors(position))
                    if (allPositions.Contains(neighbor) && !map.Tiles.ContainsKey(neighbor))
                        candidates.Add(neighbor);
                if (candidates.Count == 0) break;
                GridPosition next = candidates[_random.Next(0, candidates.Count)];
                placed.Add(next);
                map.Tiles[next] = new HuntTileState(next, definition);
            }
        }

        private void PlaceBossEncounter(HuntMapState map)
        {
            var candidates = new List<HuntTileState>();
            foreach (HuntTileState tile in map.Tiles.Values)
            {
                int distance = HuntNavigationState.HexDistance(tile.Position, GridPosition.Zero);
                if (distance >= 2 && distance <= _radius &&
                    tile.Definition != null && tile.Definition.BossEncounterWeight > 0)
                    candidates.Add(tile);
            }
            if (candidates.Count == 0)
            {
                foreach (HuntTileState tile in map.Tiles.Values)
                    if (HuntNavigationState.HexDistance(tile.Position, GridPosition.Zero) >= 2)
                        candidates.Add(tile);
            }
            if (candidates.Count > 0)
                candidates[_random.Next(0, candidates.Count)].HasBossEncounter = true;
        }

        private static List<GridPosition> GetRadialPositions(int radius)
        {
            var result = new List<GridPosition>();
            for (int x = -radius; x <= radius; x++)
            for (int y = -radius; y <= radius; y++)
                if (Math.Abs(x) + Math.Abs(y) + Math.Abs(x + y) <= 2 * radius)
                    result.Add(new GridPosition(x, y));
            return result;
        }
    }
}
