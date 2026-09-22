using System;
using System.Collections.Generic;

namespace HuntingInDarkness.GameCore.Board
{
    public enum HexFacing
    {
        E = 0,
        NE = 1,
        NW = 2,
        W = 3,
        SW = 4,
        SE = 5
    }

    /// <summary>Engine-independent entity placement state.</summary>
    public sealed class BoardState
    {
        private static readonly BoardEntityDefinition LegacyUnitDefinition =
            new BoardEntityDefinition("legacy-unit", BoardEntityKind.Unit, false, false);

        private readonly HexGridMap _grid;
        private readonly Dictionary<int, GridPosition> _entityPositions =
            new Dictionary<int, GridPosition>();
        private readonly Dictionary<GridPosition, List<int>> _positionEntities =
            new Dictionary<GridPosition, List<int>>();
        private readonly Dictionary<int, HexFacing> _facings =
            new Dictionary<int, HexFacing>();
        private readonly Dictionary<int, BoardEntityState> _entities =
            new Dictionary<int, BoardEntityState>();

        public BoardState(HexGridMap grid) => _grid = grid;

        public bool IsValid(GridPosition position) => _grid.Contains(position);
        public IReadOnlyList<GridPosition> GetAllPositions() => _grid.GetAll();
        public IReadOnlyList<GridPosition> GetInRange(GridPosition center, int range) =>
            _grid.GetInRange(center, range);
        public int GetDistance(GridPosition a, GridPosition b) => _grid.GetDistance(a, b);

        public int? GetEntityAt(GridPosition position)
        {
            if (!_positionEntities.TryGetValue(position, out List<int> ids))
                return null;

            foreach (int id in ids)
            {
                if (!_entities.TryGetValue(id, out BoardEntityState entity))
                    return id;
                if (entity.IsDestroyed)
                    continue;
                if (!entity.Definition.AllowsOverlap)
                    return id;
            }
            return null;
        }

        public IReadOnlyList<BoardEntityState> GetEntitiesAt(GridPosition position)
        {
            var result = new List<BoardEntityState>();
            if (!_positionEntities.TryGetValue(position, out List<int> ids))
                return result;

            foreach (int id in ids)
                if (_entities.TryGetValue(id, out BoardEntityState entity))
                    result.Add(entity);
            return result;
        }

        public BoardEntityState GetEntity(int entityId) =>
            _entities.TryGetValue(entityId, out BoardEntityState entity) ? entity : null;

        public GridPosition GetEntityPosition(int entityId) =>
            _entityPositions.TryGetValue(entityId, out GridPosition position)
                ? position
                : GridPosition.Zero;

        public HexFacing GetEntityFacing(int entityId) =>
            _facings.TryGetValue(entityId, out HexFacing facing) ? facing : HexFacing.E;

        public bool HasEntity(int entityId) => _entityPositions.ContainsKey(entityId);

        public void RegisterEntity(
            BoardEntityState entity,
            GridPosition position,
            HexFacing facing = HexFacing.E)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));
            _entities[entity.EntityId] = entity;
            Place(entity.EntityId, position, facing);
        }

        public void Place(int entityId, GridPosition position, HexFacing facing = HexFacing.E)
        {
            if (_entityPositions.TryGetValue(entityId, out GridPosition oldPosition))
                RemoveFromPosition(entityId, oldPosition);
            if (!_entities.ContainsKey(entityId))
                _entities[entityId] = new BoardEntityState(entityId, LegacyUnitDefinition);
            _entityPositions[entityId] = position;
            AddToPosition(entityId, position);
            _facings[entityId] = facing;
        }

        public void Move(int entityId, GridPosition target)
        {
            TryMove(entityId, target);
        }

        public BoardMovementResult TryMove(int entityId, GridPosition target)
        {
            BoardMovementResult preview = TryMovePreview(entityId, target);
            if (!preview.Succeeded)
                return preview;

            Place(entityId, target, GetEntityFacing(entityId));
            return preview;
        }

        public BoardMovementResult TryMovePreview(int entityId, GridPosition target)
        {
            GridPosition origin = GetEntityPosition(entityId);
            if (!HasEntity(entityId) || !IsValid(target))
                return new BoardMovementResult(false, origin, target, Array.Empty<int>());

            List<int> blockers = GetOverlapBlockers(entityId, target);
            return blockers.Count > 0
                ? new BoardMovementResult(false, origin, target, blockers)
                : new BoardMovementResult(true, origin, target, Array.Empty<int>());
        }

        public IReadOnlyList<int> GetTraversalBlockers(int movingEntityId, GridPosition position)
        {
            var blockers = new List<int>();
            if (!_positionEntities.TryGetValue(position, out List<int> ids))
                return blockers;

            foreach (int id in ids)
            {
                if (id == movingEntityId)
                    continue;
                if (!_entities.TryGetValue(id, out BoardEntityState entity) ||
                    (!entity.IsDestroyed && !entity.Definition.AllowsTraversal))
                    blockers.Add(id);
            }
            return blockers;
        }

        public BoardDamageResult ApplyDamage(int entityId, int amount)
        {
            return _entities.TryGetValue(entityId, out BoardEntityState entity)
                ? entity.ApplyDamage(amount)
                : BoardDamageResult.None(entityId, 0);
        }

        public void Remove(int entityId)
        {
            if (_entityPositions.TryGetValue(entityId, out GridPosition position))
            {
                RemoveFromPosition(entityId, position);
                _entityPositions.Remove(entityId);
            }
            _facings.Remove(entityId);
            _entities.Remove(entityId);
        }

        public void SetFacing(int entityId, HexFacing facing) => _facings[entityId] = facing;

        private List<int> GetOverlapBlockers(int movingEntityId, GridPosition position)
        {
            var blockers = new List<int>();
            if (!_positionEntities.TryGetValue(position, out List<int> ids))
                return blockers;

            foreach (int id in ids)
            {
                if (id == movingEntityId)
                    continue;
                if (!_entities.TryGetValue(id, out BoardEntityState entity) ||
                    (!entity.IsDestroyed && !entity.Definition.AllowsOverlap))
                    blockers.Add(id);
            }
            return blockers;
        }

        private void AddToPosition(int entityId, GridPosition position)
        {
            if (!_positionEntities.TryGetValue(position, out List<int> ids))
            {
                ids = new List<int>();
                _positionEntities[position] = ids;
            }
            if (!ids.Contains(entityId))
                ids.Add(entityId);
        }

        private void RemoveFromPosition(int entityId, GridPosition position)
        {
            if (!_positionEntities.TryGetValue(position, out List<int> ids))
                return;
            ids.Remove(entityId);
            if (ids.Count == 0)
                _positionEntities.Remove(position);
        }
    }
}
