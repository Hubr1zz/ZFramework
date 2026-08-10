using System;
using ZFramework.ECS;

internal static class Program
{
    private static void Main()
    {
        EntityVersionsRejectStaleHandles();
        AbilityTagsHaveStackSemantics();
        ProjectedTagsBlockConflictingRequests();
        RemovedDefinitionsStillCleanUpActiveTags();
        ModuleNamingMatchesTEngineConvention();
        ResetIsRejectedDuringSystemUpdate();
        ResetInvalidatesEntities();
        Console.WriteLine("ZFramework.ECS smoke tests passed.");
    }

    private static void EntityVersionsRejectStaleHandles()
    {
        var world = new EcsWorld();
        EntityId first = world.CreateEntity();
        Check(world.DestroyEntity(first), "The live entity should be destroyed.");
        EntityId second = world.CreateEntity();
        Check(first.Index == second.Index, "A released entity slot should be reusable.");
        Check(first.Version != second.Version, "A reused slot must receive a new version.");
        Check(!world.IsAlive(first), "A stale entity handle must stay invalid.");
    }

    private static void AbilityTagsHaveStackSemantics()
    {
        EcsModule module = CreateModule();
        TagId ready = TagId.FromName("State.Ready");
        TagId active = TagId.FromName("State.Active");
        EntityId entity = module.World.CreateEntity();
        module.World.AddTag(entity, ready);
        module.World.AddTag(entity, active);

        var definition = new AbilityDefinition(
            "Action.Temporary",
            activationQuery: new TagQuery(requiredAll: new[] { ready }),
            grantOnStart: new[] { active },
            durationSeconds: 0f);
        module.Abilities.Registry.Register(definition);
        module.Abilities.Request(entity, definition.Id);

        module.Update(0.1f, 0.1f);
        Check(module.World.GetTagCount(entity, active) == 2, "Ability grants must stack with existing tags.");

        module.Update(0.1f, 0.1f);
        Check(module.World.GetTagCount(entity, active) == 1, "Ending one ability must only release its own tag grant.");
        module.Shutdown();
    }

    private static void ProjectedTagsBlockConflictingRequests()
    {
        EcsModule module = CreateModule();
        TagId exclusive = TagId.FromName("Group.Exclusive");
        TagId secondStarted = TagId.FromName("State.SecondStarted");
        EntityId entity = module.World.CreateEntity();

        var first = new AbilityDefinition(
            "Action.First",
            grantOnStart: new[] { exclusive },
            durationSeconds: -1f);
        var second = new AbilityDefinition(
            "Action.Second",
            activationQuery: new TagQuery(blockedAny: new[] { exclusive }),
            grantOnStart: new[] { secondStarted },
            durationSeconds: -1f);
        module.Abilities.Registry.Register(first);
        module.Abilities.Registry.Register(second);

        module.Abilities.Request(entity, first.Id);
        module.Abilities.Request(entity, second.Id);
        module.Update(0.1f, 0.1f);

        Check(module.World.HasTag(entity, exclusive), "The first request should activate.");
        Check(!module.World.HasTag(entity, secondStarted), "A projected tag must block a later same-stage request.");
        module.Shutdown();
    }

    private static void RemovedDefinitionsStillCleanUpActiveTags()
    {
        EcsModule module = CreateModule();
        TagId active = TagId.FromName("State.PersistentAction");
        EntityId entity = module.World.CreateEntity();
        var definition = new AbilityDefinition(
            "Action.RemovableDefinition",
            grantOnStart: new[] { active },
            durationSeconds: 0f);
        module.Abilities.Registry.Register(definition);
        module.Abilities.Request(entity, definition.Id);
        module.Update(0.1f, 0.1f);
        module.Abilities.Registry.Remove(definition.Id);
        module.Update(0.1f, 0.1f);

        Check(!module.World.HasTag(entity, active), "An active instance must retain enough data to clean up.");
        module.Shutdown();
    }

    private static void ResetInvalidatesEntities()
    {
        EcsModule module = CreateModule();
        EntityId beforeReset = module.World.CreateEntity();
        module.ResetWorld();
        Check(!module.World.IsAlive(beforeReset), "World reset must invalidate existing entity handles.");
        Check(module.World.EntityCount == 0, "World reset must remove all entities.");
        module.Shutdown();
    }

    private static void ModuleNamingMatchesTEngineConvention()
    {
        string assemblyName = typeof(IEcsModule).Assembly.GetName().Name;
        string implementationName = $"{typeof(IEcsModule).Namespace}.EcsModule, {assemblyName}";
        Check(Type.GetType(implementationName) == typeof(EcsModule),
            "IEcsModule must resolve to EcsModule through the TEngine naming convention.");
    }

    private static void ResetIsRejectedDuringSystemUpdate()
    {
        EcsModule module = CreateModule();
        module.AddSystem(EcsStage.Simulation, new ResetWorldSystem(module));
        bool rejected = false;
        try
        {
            module.Update(0.1f, 0.1f);
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        Check(rejected, "World reset must be rejected while systems are updating.");
        module.Shutdown();
    }

    private sealed class ResetWorldSystem : IEcsSystem
    {
        private readonly IEcsModule _module;
        public ResetWorldSystem(IEcsModule module) => _module = module;
        public void Update(in EcsSystemContext context) => _module.ResetWorld();
    }

    private static EcsModule CreateModule()
    {
        var module = new EcsModule();
        module.OnInit();
        return module;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
