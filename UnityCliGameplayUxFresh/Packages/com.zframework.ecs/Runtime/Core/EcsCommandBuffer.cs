using System;
using System.Collections.Generic;

namespace ZFramework.ECS
{
    public sealed class EcsCommandBuffer
    {
        private interface ICommand
        {
            void Apply(EcsWorld world);
        }

        private sealed class SetComponentCommand<T> : ICommand
        {
            private readonly EntityId _entity;
            private readonly T _component;

            public SetComponentCommand(EntityId entity, T component)
            {
                _entity = entity;
                _component = component;
            }

            public void Apply(EcsWorld world)
            {
                if (world.IsAlive(_entity))
                {
                    world.SetComponent(_entity, _component);
                }
            }
        }

        private sealed class RemoveComponentCommand<T> : ICommand
        {
            private readonly EntityId _entity;
            public RemoveComponentCommand(EntityId entity) => _entity = entity;
            public void Apply(EcsWorld world) => world.RemoveComponent<T>(_entity);
        }

        private sealed class DestroyEntityCommand : ICommand
        {
            private readonly EntityId _entity;
            public DestroyEntityCommand(EntityId entity) => _entity = entity;
            public void Apply(EcsWorld world) => world.DestroyEntity(_entity);
        }

        private sealed class TagCommand : ICommand
        {
            private readonly EntityId _entity;
            private readonly TagId _tag;
            private readonly bool _add;

            public TagCommand(EntityId entity, TagId tag, bool add)
            {
                _entity = entity;
                _tag = tag;
                _add = add;
            }

            public void Apply(EcsWorld world)
            {
                if (!world.IsAlive(_entity))
                {
                    return;
                }

                if (_add)
                {
                    world.AddTag(_entity, _tag);
                }
                else
                {
                    world.RemoveTag(_entity, _tag);
                }
            }
        }

        private readonly List<ICommand> _commands = new List<ICommand>();
        private readonly Dictionary<EntityId, Dictionary<TagId, int>> _projectedTagDeltas =
            new Dictionary<EntityId, Dictionary<TagId, int>>();

        public int Count => _commands.Count;

        public void SetComponent<T>(EntityId entity, T component) =>
            _commands.Add(new SetComponentCommand<T>(entity, component));

        public void RemoveComponent<T>(EntityId entity) =>
            _commands.Add(new RemoveComponentCommand<T>(entity));

        public void DestroyEntity(EntityId entity) => _commands.Add(new DestroyEntityCommand(entity));

        public void AddTag(EntityId entity, TagId tag)
        {
            AddProjectedTagDelta(entity, tag, 1);
            _commands.Add(new TagCommand(entity, tag, true));
        }

        public void RemoveTag(EntityId entity, TagId tag)
        {
            AddProjectedTagDelta(entity, tag, -1);
            _commands.Add(new TagCommand(entity, tag, false));
        }

        public bool HasTag(EcsWorld world, EntityId entity, TagId tag)
        {
            int count = world.GetTagCount(entity, tag);
            if (_projectedTagDeltas.TryGetValue(entity, out Dictionary<TagId, int> tags)
                && tags.TryGetValue(tag, out int delta))
            {
                count += delta;
            }

            return count > 0;
        }

        public void Playback(EcsWorld world)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            try
            {
                for (int i = 0; i < _commands.Count; i++)
                {
                    _commands[i].Apply(world);
                }
            }
            finally
            {
                Clear();
            }
        }

        public void Clear()
        {
            _commands.Clear();
            _projectedTagDeltas.Clear();
        }

        private void AddProjectedTagDelta(EntityId entity, TagId tag, int delta)
        {
            if (!_projectedTagDeltas.TryGetValue(entity, out Dictionary<TagId, int> tags))
            {
                tags = new Dictionary<TagId, int>();
                _projectedTagDeltas.Add(entity, tags);
            }

            tags.TryGetValue(tag, out int current);
            tags[tag] = current + delta;
        }
    }
}
