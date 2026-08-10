namespace ZFramework.ECS
{
    /// <summary>
    /// Public integration boundary. Requesting this interface through TEngine.ModuleSystem
    /// installs the ECS extension lazily; removing the package removes the feature entirely.
    /// </summary>
    public interface IEcsModule
    {
        EcsWorld World { get; }
        EcsPipeline Pipeline { get; }
        AbilityService Abilities { get; }
        bool Enabled { get; set; }

        void AddSystem(EcsStage stage, IEcsSystem system, int order = 0);
        bool RemoveSystem(EcsStage stage, IEcsSystem system);
        void ResetWorld();
    }
}
