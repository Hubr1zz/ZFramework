using System;
using System.Collections.Generic;

namespace ZFramework.ECS
{
    public sealed class TagQuery
    {
        private readonly TagId[] _requiredAll;
        private readonly TagId[] _requiredAny;
        private readonly TagId[] _blockedAny;

        public IReadOnlyList<TagId> RequiredAll => _requiredAll;
        public IReadOnlyList<TagId> RequiredAny => _requiredAny;
        public IReadOnlyList<TagId> BlockedAny => _blockedAny;
        public bool IsEmpty => _requiredAll.Length == 0 && _requiredAny.Length == 0 && _blockedAny.Length == 0;

        public TagQuery(
            IEnumerable<TagId> requiredAll = null,
            IEnumerable<TagId> requiredAny = null,
            IEnumerable<TagId> blockedAny = null)
        {
            _requiredAll = Copy(requiredAll);
            _requiredAny = Copy(requiredAny);
            _blockedAny = Copy(blockedAny);
        }

        public bool Matches(EcsWorld world, EntityId entity, EcsCommandBuffer commands = null)
        {
            if (!world.IsAlive(entity))
            {
                return false;
            }

            for (int i = 0; i < _requiredAll.Length; i++)
            {
                if (!HasTag(world, commands, entity, _requiredAll[i]))
                {
                    return false;
                }
            }

            if (_requiredAny.Length > 0)
            {
                bool found = false;
                for (int i = 0; i < _requiredAny.Length; i++)
                {
                    if (HasTag(world, commands, entity, _requiredAny[i]))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    return false;
                }
            }

            for (int i = 0; i < _blockedAny.Length; i++)
            {
                if (HasTag(world, commands, entity, _blockedAny[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasTag(EcsWorld world, EcsCommandBuffer commands, EntityId entity, TagId tag)
        {
            return commands == null ? world.HasTag(entity, tag) : commands.HasTag(world, entity, tag);
        }

        private static TagId[] Copy(IEnumerable<TagId> source)
        {
            if (source == null)
            {
                return Array.Empty<TagId>();
            }

            return new List<TagId>(source).ToArray();
        }
    }
}
