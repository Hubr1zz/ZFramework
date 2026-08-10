using TEngine;

namespace ZFramework.ECS
{
    public sealed class EcsModule : Module, IEcsModule, IUpdateModule
    {
        private readonly EcsCommandBuffer _commands = new EcsCommandBuffer();
        private double _time;
        private bool _isUpdating;

        public EcsWorld World { get; private set; }
        public EcsPipeline Pipeline { get; private set; }
        public AbilityService Abilities { get; private set; }
        public bool Enabled { get; set; } = true;

        public override void OnInit()
        {
            World = new EcsWorld();
            Pipeline = new EcsPipeline();
            Abilities = new AbilityService();
            Pipeline.AddSystem(EcsStage.AbilityRules, new AbilityRuleSystem(Abilities), int.MinValue);
        }

        public void Update(float elapseSeconds, float realElapseSeconds)
        {
            if (!Enabled)
            {
                return;
            }

            if (_isUpdating)
            {
                throw new System.InvalidOperationException("The ECS module cannot update recursively.");
            }

            _isUpdating = true;
            try
            {
                _time += elapseSeconds;
                Pipeline.Update(World, _commands, elapseSeconds, realElapseSeconds, _time);
            }
            finally
            {
                _isUpdating = false;
            }
        }

        public void AddSystem(EcsStage stage, IEcsSystem system, int order = 0) =>
            Pipeline.AddSystem(stage, system, order);

        public bool RemoveSystem(EcsStage stage, IEcsSystem system) =>
            Pipeline.RemoveSystem(stage, system);

        public void ResetWorld()
        {
            if (_isUpdating)
            {
                throw new System.InvalidOperationException("The ECS world cannot be reset while systems are updating.");
            }

            _commands.Clear();
            Abilities.ClearRequests();
            World.Clear();
            _time = 0d;
        }

        public override void Shutdown()
        {
            Enabled = false;
            ResetWorld();
            Abilities.Registry.Clear();
            Pipeline.Clear();
        }
    }
}
