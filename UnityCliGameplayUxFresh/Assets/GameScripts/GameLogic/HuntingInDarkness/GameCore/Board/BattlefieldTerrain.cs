using System;
using System.Collections.Generic;

namespace HuntingInDarkness.GameCore.Board
{
    public static class BattlefieldTerrainDefinitions
    {
        public const string ThrowRockActionId = "throw-rock";

        public static BoardEntityDefinition CreateGrass(string id = "grass") =>
            new BoardEntityDefinition(
                id,
                BoardEntityKind.Terrain,
                allowsOverlap: true,
                allowsTraversal: true,
                evasionModifier: 1);

        public static BoardEntityDefinition CreateRock(string id = "rock") =>
            new BoardEntityDefinition(
                id,
                BoardEntityKind.Terrain,
                allowsOverlap: true,
                allowsTraversal: true,
                temporaryActions: new[]
                {
                    new BattlefieldActionDefinition(ThrowRockActionId, "投石")
                });
    }

    /// <summary>Read-only terrain modifiers exposed without leaking board containers.</summary>
    public sealed class BattlefieldTerrainQuery
    {
        private readonly BoardState _board;

        public BattlefieldTerrainQuery(BoardState board) =>
            _board = board ?? throw new ArgumentNullException(nameof(board));

        public int GetEvasionModifier(int entityId)
        {
            if (!_board.HasEntity(entityId))
                return 0;

            int modifier = 0;
            foreach (BoardEntityState entity in
                     _board.GetEntitiesAt(_board.GetEntityPosition(entityId)))
            {
                if (entity.EntityId != entityId && !entity.IsDestroyed)
                    modifier += entity.Definition.EvasionModifier;
            }
            return modifier;
        }

        public IReadOnlyList<BattlefieldActionDefinition> GetTemporaryActions(int entityId)
        {
            var result = new List<BattlefieldActionDefinition>();
            if (!_board.HasEntity(entityId))
                return result;

            var actionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (BoardEntityState entity in
                     _board.GetEntitiesAt(_board.GetEntityPosition(entityId)))
            {
                if (entity.EntityId == entityId || entity.IsDestroyed)
                    continue;
                foreach (BattlefieldActionDefinition action in entity.Definition.TemporaryActions)
                    if (actionIds.Add(action.Id))
                        result.Add(action);
            }
            return result;
        }
    }
}
