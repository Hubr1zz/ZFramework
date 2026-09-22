using System;
using System.Collections.Generic;
using HuntingInDarkness.Data;
using UnityEngine;

namespace HuntingInDarkness.Hunt
{
    public enum HuntTileInteractionKind
    {
        None,
        Reveal,
        Move
    }

    public readonly struct HuntTileInteractionCommit
    {
        public HuntTileInteractionCommit(HuntTileInteractionKind kind, Vector2Int coordinate, HexTileInstance tile, IReadOnlyList<Vector2Int> newlyInteractable)
        {
            Kind = kind;
            Coordinate = coordinate;
            Tile = tile;
            NewlyInteractable = newlyInteractable ?? Array.Empty<Vector2Int>();
        }

        public HuntTileInteractionKind Kind { get; }
        public Vector2Int Coordinate { get; }
        public HexTileInstance Tile { get; }
        public IReadOnlyList<Vector2Int> NewlyInteractable { get; }
        public bool BossEncounter => Tile?.HasBossEncounter == true;
        public bool IsCommitted => Kind != HuntTileInteractionKind.None && Tile != null;
    }
}
