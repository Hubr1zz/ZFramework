using System;
using System.Collections.Generic;

namespace ZFramework.ECS
{
    public enum EcsStage
    {
        Input = 100,
        AbilityRules = 200,
        Simulation = 300,
        Lifetime = 400,
        Presentation = 500
    }

    public readonly struct EcsSystemContext
    {
        public EcsWorld World { get; }
        public EcsCommandBuffer Commands { get; }
        public float DeltaTime { get; }
        public float RealDeltaTime { get; }
        public double Time { get; }

        internal EcsSystemContext(
            EcsWorld world,
            EcsCommandBuffer commands,
            float deltaTime,
            float realDeltaTime,
            double time)
        {
            World = world;
            Commands = commands;
            DeltaTime = deltaTime;
            RealDeltaTime = realDeltaTime;
            Time = time;
        }
    }

    public interface IEcsSystem
    {
        void Update(in EcsSystemContext context);
    }

    public sealed class EcsPipeline
    {
        private sealed class Entry
        {
            public IEcsSystem System;
            public int Order;
            public long Sequence;
        }

        private readonly SortedDictionary<EcsStage, List<Entry>> _stages =
            new SortedDictionary<EcsStage, List<Entry>>();
        private long _nextSequence;
        private bool _isUpdating;

        public void AddSystem(EcsStage stage, IEcsSystem system, int order = 0)
        {
            EnsureNotUpdating();
            if (system == null)
            {
                throw new ArgumentNullException(nameof(system));
            }

            if (!_stages.TryGetValue(stage, out List<Entry> entries))
            {
                entries = new List<Entry>();
                _stages.Add(stage, entries);
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (ReferenceEquals(entries[i].System, system))
                {
                    throw new InvalidOperationException("The same system instance cannot be registered twice in one stage.");
                }
            }

            entries.Add(new Entry { System = system, Order = order, Sequence = _nextSequence++ });
            entries.Sort((left, right) =>
            {
                int orderResult = left.Order.CompareTo(right.Order);
                return orderResult != 0 ? orderResult : left.Sequence.CompareTo(right.Sequence);
            });
        }

        public bool RemoveSystem(EcsStage stage, IEcsSystem system)
        {
            EnsureNotUpdating();
            if (!_stages.TryGetValue(stage, out List<Entry> entries))
            {
                return false;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (!ReferenceEquals(entries[i].System, system))
                {
                    continue;
                }

                entries.RemoveAt(i);
                if (entries.Count == 0)
                {
                    _stages.Remove(stage);
                }

                return true;
            }

            return false;
        }

        public void Update(
            EcsWorld world,
            EcsCommandBuffer commands,
            float deltaTime,
            float realDeltaTime,
            double time)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (commands == null) throw new ArgumentNullException(nameof(commands));

            if (_isUpdating)
            {
                throw new InvalidOperationException("An ECS pipeline cannot update recursively.");
            }

            var context = new EcsSystemContext(world, commands, deltaTime, realDeltaTime, time);
            _isUpdating = true;
            try
            {
                foreach (KeyValuePair<EcsStage, List<Entry>> stage in _stages)
                {
                    try
                    {
                        List<Entry> systems = stage.Value;
                        for (int i = 0; i < systems.Count; i++)
                        {
                            systems[i].System.Update(context);
                        }

                        commands.Playback(world);
                    }
                    catch
                    {
                        commands.Clear();
                        throw;
                    }
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }

        public void Clear()
        {
            EnsureNotUpdating();
            _stages.Clear();
            _nextSequence = 0;
        }

        private void EnsureNotUpdating()
        {
            if (_isUpdating)
            {
                throw new InvalidOperationException("The ECS pipeline cannot be structurally changed while it is updating.");
            }
        }
    }
}
