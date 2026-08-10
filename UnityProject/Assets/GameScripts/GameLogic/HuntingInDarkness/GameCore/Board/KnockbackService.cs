using System;
using System.Collections.Generic;

namespace HuntingInDarkness.GameCore.Board
{
    public enum KnockbackCollisionKind
    {
        None,
        Boundary,
        Entity
    }

    public readonly struct ImpactContext
    {
        public BoardEntityState MovingEntity { get; }
        public BoardEntityState BlockingEntity { get; }
        public GridPosition CollisionPosition { get; }
        public int RequestedDistance { get; }
        public int TravelledDistance { get; }

        public ImpactContext(
            BoardEntityState movingEntity,
            BoardEntityState blockingEntity,
            GridPosition collisionPosition,
            int requestedDistance,
            int travelledDistance)
        {
            MovingEntity = movingEntity;
            BlockingEntity = blockingEntity;
            CollisionPosition = collisionPosition;
            RequestedDistance = requestedDistance;
            TravelledDistance = travelledDistance;
        }
    }

    public readonly struct ImpactDamage
    {
        public int MovingEntityDamage { get; }
        public int BlockingEntityDamage { get; }

        public ImpactDamage(int movingEntityDamage, int blockingEntityDamage)
        {
            MovingEntityDamage = Math.Max(0, movingEntityDamage);
            BlockingEntityDamage = Math.Max(0, blockingEntityDamage);
        }
    }

    /// <summary>
    /// Supplies project-specific impact damage without coupling board movement to a formula.
    /// </summary>
    public interface IImpactDamagePolicy
    {
        ImpactDamage Calculate(ImpactContext context);
    }

    /// <summary>
    /// Immutable movement intent. Adapters may animate Path before calling TryCommit.
    /// </summary>
    public sealed class KnockbackPlan
    {
        public int EntityId { get; }
        public GridPosition Origin { get; }
        public GridPosition FinalPosition { get; }
        public IReadOnlyList<GridPosition> Path { get; }
        public KnockbackCollisionKind CollisionKind { get; }
        public GridPosition CollisionPosition { get; }
        public int? BlockingEntityId { get; }
        public ImpactDamage Damage { get; }
        public bool HasCollision => CollisionKind != KnockbackCollisionKind.None;

        internal KnockbackPlan(
            int entityId,
            GridPosition origin,
            GridPosition finalPosition,
            IReadOnlyList<GridPosition> path,
            KnockbackCollisionKind collisionKind,
            GridPosition collisionPosition,
            int? blockingEntityId,
            ImpactDamage damage)
        {
            EntityId = entityId;
            Origin = origin;
            FinalPosition = finalPosition;
            Path = path ?? Array.Empty<GridPosition>();
            CollisionKind = collisionKind;
            CollisionPosition = collisionPosition;
            BlockingEntityId = blockingEntityId;
            Damage = damage;
        }
    }

    public readonly struct KnockbackCommitResult
    {
        public bool Succeeded { get; }
        public BoardDamageResult MovingEntityDamage { get; }
        public BoardDamageResult BlockingEntityDamage { get; }

        public KnockbackCommitResult(
            bool succeeded,
            BoardDamageResult movingEntityDamage,
            BoardDamageResult blockingEntityDamage)
        {
            Succeeded = succeeded;
            MovingEntityDamage = movingEntityDamage;
            BlockingEntityDamage = blockingEntityDamage;
        }
    }

    /// <summary>Plans deterministic knockback, then commits it after presentation completes.</summary>
    public sealed class KnockbackService
    {
        private readonly BoardState _board;
        private readonly IImpactDamagePolicy _damagePolicy;

        public KnockbackService(BoardState board, IImpactDamagePolicy damagePolicy)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _damagePolicy = damagePolicy ?? throw new ArgumentNullException(nameof(damagePolicy));
        }

        public KnockbackPlan Plan(int entityId, GridPosition direction, int distance)
        {
            if (!_board.HasEntity(entityId))
                throw new InvalidOperationException($"Entity {entityId} is not on the board.");
            if (_board.GetDistance(GridPosition.Zero, direction) != 1)
                throw new ArgumentException("Knockback direction must be one adjacent axial step.", nameof(direction));

            int safeDistance = Math.Max(0, distance);
            GridPosition origin = _board.GetEntityPosition(entityId);
            GridPosition current = origin;
            var path = new List<GridPosition>(safeDistance);

            for (int step = 1; step <= safeDistance; step++)
            {
                GridPosition next = current + direction;
                if (!_board.IsValid(next))
                    return CreateCollisionPlan(
                        entityId, origin, current, path, KnockbackCollisionKind.Boundary,
                        next, null, safeDistance);

                IReadOnlyList<int> blockers = _board.GetTraversalBlockers(entityId, next);
                if (blockers.Count == 0 && step == safeDistance)
                {
                    BoardMovementResult occupancy = _board.TryMovePreview(entityId, next);
                    blockers = occupancy.BlockingEntityIds;
                }

                if (blockers.Count > 0)
                    return CreateCollisionPlan(
                        entityId, origin, current, path, KnockbackCollisionKind.Entity,
                        next, blockers[0], safeDistance);

                path.Add(next);
                current = next;
            }

            return new KnockbackPlan(
                entityId, origin, current, path, KnockbackCollisionKind.None,
                current, null, new ImpactDamage(0, 0));
        }

        public KnockbackCommitResult TryCommit(KnockbackPlan plan)
        {
            if (plan == null || !_board.HasEntity(plan.EntityId) ||
                _board.GetEntityPosition(plan.EntityId) != plan.Origin)
                return FailedCommit(plan?.EntityId ?? 0);

            if (plan.BlockingEntityId.HasValue &&
                (!_board.HasEntity(plan.BlockingEntityId.Value) ||
                 _board.GetEntityPosition(plan.BlockingEntityId.Value) != plan.CollisionPosition))
                return FailedCommit(plan.EntityId);

            BoardMovementResult movement = _board.TryMove(plan.EntityId, plan.FinalPosition);
            if (!movement.Succeeded)
                return FailedCommit(plan.EntityId);

            BoardDamageResult movingDamage =
                _board.ApplyDamage(plan.EntityId, plan.Damage.MovingEntityDamage);
            BoardDamageResult blockingDamage = plan.BlockingEntityId.HasValue
                ? _board.ApplyDamage(plan.BlockingEntityId.Value, plan.Damage.BlockingEntityDamage)
                : BoardDamageResult.None(0, 0);
            return new KnockbackCommitResult(true, movingDamage, blockingDamage);
        }

        private KnockbackPlan CreateCollisionPlan(
            int entityId,
            GridPosition origin,
            GridPosition finalPosition,
            IReadOnlyList<GridPosition> path,
            KnockbackCollisionKind collisionKind,
            GridPosition collisionPosition,
            int? blockingEntityId,
            int requestedDistance)
        {
            BoardEntityState movingEntity = _board.GetEntity(entityId);
            BoardEntityState blockingEntity = blockingEntityId.HasValue
                ? _board.GetEntity(blockingEntityId.Value)
                : null;
            ImpactDamage damage = _damagePolicy.Calculate(new ImpactContext(
                movingEntity,
                blockingEntity,
                collisionPosition,
                requestedDistance,
                path.Count));
            return new KnockbackPlan(
                entityId,
                origin,
                finalPosition,
                new List<GridPosition>(path),
                collisionKind,
                collisionPosition,
                blockingEntityId,
                damage);
        }

        private static KnockbackCommitResult FailedCommit(int entityId) =>
            new KnockbackCommitResult(
                false,
                BoardDamageResult.None(entityId, 0),
                BoardDamageResult.None(0, 0));
    }
}
