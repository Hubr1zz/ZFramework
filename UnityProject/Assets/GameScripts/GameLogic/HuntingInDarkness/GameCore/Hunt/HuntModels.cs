using System;
using System.Collections.Generic;
using HuntingInDarkness.GameCore.Board;

namespace HuntingInDarkness.GameCore.Hunt
{
    public enum HuntTileVisibility
    {
        Locked,
        Interactable,
        Revealed
    }

    public sealed class HuntTileDefinition
    {
        public string Id { get; }
        public int SpawnWeight { get; }
        public bool SpawnInGroup { get; }
        public int GroupSize { get; }
        public int BossEncounterWeight { get; }

        public HuntTileDefinition(
            string id,
            int spawnWeight,
            bool spawnInGroup,
            int groupSize,
            int bossEncounterWeight)
        {
            Id = id ?? string.Empty;
            SpawnWeight = spawnWeight;
            SpawnInGroup = spawnInGroup;
            GroupSize = groupSize;
            BossEncounterWeight = bossEncounterWeight;
        }
    }

    public sealed class HuntTileState
    {
        public GridPosition Position { get; }
        public HuntTileDefinition Definition { get; }
        public HuntTileVisibility Visibility { get; set; } = HuntTileVisibility.Locked;
        public bool HasBossEncounter { get; set; }

        public HuntTileState(GridPosition position, HuntTileDefinition definition)
        {
            Position = position;
            Definition = definition;
        }
    }

    public sealed class HuntMapState
    {
        public Dictionary<GridPosition, HuntTileState> Tiles { get; } =
            new Dictionary<GridPosition, HuntTileState>();
    }

    public sealed class HuntNavigationState
    {
        public GridPosition SquadPosition { get; private set; } = GridPosition.Zero;

        public void Reset() => SquadPosition = GridPosition.Zero;
        public void MoveTo(GridPosition target) => SquadPosition = target;
        public bool IsAdjacent(GridPosition target) =>
            HexDistance(SquadPosition, target) == 1;

        public static int HexDistance(GridPosition a, GridPosition b)
        {
            int dx = a.X - b.X;
            int dy = a.Y - b.Y;
            return (Math.Abs(dx) + Math.Abs(dy) + Math.Abs(dx + dy)) / 2;
        }
    }

    public sealed class ResourcePointDefinition
    {
        public string ResourceId { get; }
        public int SpawnWeight { get; }
        public int DrawCount { get; }
        public int MaxPerTile { get; }

        public ResourcePointDefinition(string resourceId, int spawnWeight, int drawCount, int maxPerTile)
        {
            ResourceId = resourceId ?? string.Empty;
            SpawnWeight = spawnWeight;
            DrawCount = drawCount;
            MaxPerTile = maxPerTile;
        }
    }
}
