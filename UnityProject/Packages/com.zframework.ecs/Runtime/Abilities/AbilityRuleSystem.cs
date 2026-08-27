using System;
using System.Collections.Generic;

namespace ZFramework.ECS
{
    internal sealed class AbilityRuleSystem : IEcsSystem
    {
        private readonly AbilityService _abilities;
        private readonly HashSet<AbilityKey> _cancellations = new HashSet<AbilityKey>();
        private readonly List<EntityId> _runtimeEntities = new List<EntityId>();

        public AbilityRuleSystem(AbilityService abilities)
        {
            _abilities = abilities ?? throw new ArgumentNullException(nameof(abilities));
        }

        public void Update(in EcsSystemContext context)
        {
            _abilities.DrainCancellations(_cancellations);
            CollectRuntimeEntities(context.World);
            EndExpiredOrCancelled(context);
            StartRequested(context);
            RemoveEmptyRuntimeBuffers(context);
        }

        private void CollectRuntimeEntities(EcsWorld world)
        {
            _runtimeEntities.Clear();
            foreach (EntityId entity in world.Query<AbilityRuntimeBuffer>())
            {
                _runtimeEntities.Add(entity);
            }
        }

        private void EndExpiredOrCancelled(in EcsSystemContext context)
        {
            for (int entityIndex = 0; entityIndex < _runtimeEntities.Count; entityIndex++)
            {
                EntityId entity = _runtimeEntities[entityIndex];
                if (!context.World.TryGetComponent(entity, out AbilityRuntimeBuffer runtime))
                {
                    continue;
                }

                for (int i = runtime.Count - 1; i >= 0; i--)
                {
                    ActiveAbility active = runtime.Items[i];
                    if (!_abilities.Registry.TryGet(active.Ability, out AbilityDefinition definition))
                    {
                        EndAbility(context.Commands, entity, runtime, i, active.Definition);
                        continue;
                    }

                    bool explicitlyCancelled = _cancellations.Contains(new AbilityKey(entity, active.Ability));
                    bool expired = active.EndTime <= context.Time;
                    bool ongoingFailed = !definition.OngoingQuery.IsEmpty
                                         && !definition.OngoingQuery.Matches(context.World, entity, context.Commands);

                    if (explicitlyCancelled || expired || ongoingFailed)
                    {
                        EndAbility(context.Commands, entity, runtime, i, definition);
                    }
                }
            }
        }

        private void StartRequested(in EcsSystemContext context)
        {
            while (_abilities.TryDequeueRequest(out AbilityRequest request))
            {
                if (!context.World.IsAlive(request.Entity)
                    || !_abilities.Registry.TryGet(request.Ability, out AbilityDefinition definition)
                    || !definition.ActivationQuery.Matches(context.World, request.Entity, context.Commands))
                {
                    continue;
                }

                if (!context.World.TryGetComponent(request.Entity, out AbilityRuntimeBuffer runtime))
                {
                    runtime = new AbilityRuntimeBuffer();
                    context.World.SetComponent(request.Entity, runtime);
                    _runtimeEntities.Add(request.Entity);
                }

                if (runtime.Contains(request.Ability))
                {
                    continue;
                }

                ApplyTags(context.Commands, request.Entity, definition.RemoveOnStart, false);
                ApplyTags(context.Commands, request.Entity, definition.GrantOnStart, true);

                double endTime = definition.DurationSeconds < 0f
                    ? double.PositiveInfinity
                    : context.Time + definition.DurationSeconds;
                runtime.Add(new ActiveAbility(definition, endTime));
            }
        }

        private void RemoveEmptyRuntimeBuffers(in EcsSystemContext context)
        {
            for (int i = 0; i < _runtimeEntities.Count; i++)
            {
                EntityId entity = _runtimeEntities[i];
                if (context.World.TryGetComponent(entity, out AbilityRuntimeBuffer runtime) && runtime.Count == 0)
                {
                    context.Commands.RemoveComponent<AbilityRuntimeBuffer>(entity);
                }
            }
        }

        private static void EndAbility(
            EcsCommandBuffer commands,
            EntityId entity,
            AbilityRuntimeBuffer runtime,
            int index,
            AbilityDefinition definition)
        {
            if (definition != null)
            {
                ApplyTags(commands, entity, definition.RemoveOnEnd, false);
            }

            runtime.RemoveAt(index);
        }

        private static void ApplyTags(
            EcsCommandBuffer commands,
            EntityId entity,
            IReadOnlyList<TagId> tags,
            bool add)
        {
            for (int i = 0; i < tags.Count; i++)
            {
                if (add)
                {
                    commands.AddTag(entity, tags[i]);
                }
                else
                {
                    commands.RemoveTag(entity, tags[i]);
                }
            }
        }
    }
}
