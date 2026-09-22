using System;
using System.Collections.Generic;

namespace HuntingInDarkness.GameCore.Board
{
    public enum BoardEntityKind
    {
        Unit,
        Terrain,
        Obstacle
    }

    public sealed class BattlefieldActionDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }

        public BattlefieldActionDefinition(string id, string displayName)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
        }
    }

    /// <summary>Immutable rules shared by every runtime instance of a board entity.</summary>
    public sealed class BoardEntityDefinition
    {
        private static readonly IReadOnlyList<BattlefieldActionDefinition> EmptyActions =
            Array.Empty<BattlefieldActionDefinition>();

        public string Id { get; }
        public BoardEntityKind Kind { get; }
        public bool AllowsOverlap { get; }
        public bool AllowsTraversal { get; }
        public int MaxHealth { get; }
        public string DamageEffectId { get; }
        public string DestructionEffectId { get; }
        public int EvasionModifier { get; }
        public IReadOnlyList<BattlefieldActionDefinition> TemporaryActions { get; }
        public bool IsDestructible => MaxHealth > 0;

        public BoardEntityDefinition(
            string id,
            BoardEntityKind kind,
            bool allowsOverlap,
            bool allowsTraversal,
            int maxHealth = 0,
            string damageEffectId = "",
            string destructionEffectId = "",
            int evasionModifier = 0,
            IReadOnlyList<BattlefieldActionDefinition> temporaryActions = null)
        {
            Id = id ?? string.Empty;
            Kind = kind;
            AllowsOverlap = allowsOverlap;
            AllowsTraversal = allowsTraversal;
            MaxHealth = Math.Max(0, maxHealth);
            DamageEffectId = damageEffectId ?? string.Empty;
            DestructionEffectId = destructionEffectId ?? string.Empty;
            EvasionModifier = evasionModifier;
            TemporaryActions = temporaryActions == null
                ? EmptyActions
                : new List<BattlefieldActionDefinition>(temporaryActions);
        }
    }

    public sealed class BoardEntityState
    {
        public int EntityId { get; }
        public BoardEntityDefinition Definition { get; }
        public int CurrentHealth { get; private set; }
        public bool IsDestroyed { get; private set; }

        public BoardEntityState(int entityId, BoardEntityDefinition definition)
        {
            EntityId = entityId;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            CurrentHealth = definition.MaxHealth;
        }

        public BoardDamageResult ApplyDamage(int amount)
        {
            if (amount <= 0 || !Definition.IsDestructible || IsDestroyed)
                return BoardDamageResult.None(EntityId, CurrentHealth);

            int previousHealth = CurrentHealth;
            CurrentHealth = Math.Max(0, CurrentHealth - amount);
            IsDestroyed = CurrentHealth == 0;
            return new BoardDamageResult(
                EntityId,
                previousHealth - CurrentHealth,
                CurrentHealth,
                IsDestroyed,
                Definition.DamageEffectId,
                IsDestroyed ? Definition.DestructionEffectId : string.Empty);
        }
    }

    public readonly struct BoardDamageResult
    {
        public int EntityId { get; }
        public int DamageApplied { get; }
        public int RemainingHealth { get; }
        public bool WasDestroyed { get; }
        public string DamageEffectId { get; }
        public string DestructionEffectId { get; }

        public BoardDamageResult(
            int entityId,
            int damageApplied,
            int remainingHealth,
            bool wasDestroyed,
            string damageEffectId,
            string destructionEffectId)
        {
            EntityId = entityId;
            DamageApplied = damageApplied;
            RemainingHealth = remainingHealth;
            WasDestroyed = wasDestroyed;
            DamageEffectId = damageEffectId ?? string.Empty;
            DestructionEffectId = destructionEffectId ?? string.Empty;
        }

        public static BoardDamageResult None(int entityId, int remainingHealth) =>
            new BoardDamageResult(entityId, 0, remainingHealth, false, string.Empty, string.Empty);
    }

    public readonly struct BoardMovementResult
    {
        public bool Succeeded { get; }
        public GridPosition Origin { get; }
        public GridPosition Destination { get; }
        public IReadOnlyList<int> BlockingEntityIds { get; }

        public BoardMovementResult(
            bool succeeded,
            GridPosition origin,
            GridPosition destination,
            IReadOnlyList<int> blockingEntityIds)
        {
            Succeeded = succeeded;
            Origin = origin;
            Destination = destination;
            BlockingEntityIds = blockingEntityIds ?? Array.Empty<int>();
        }
    }
}
