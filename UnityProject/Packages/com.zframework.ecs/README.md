# ZFramework ECS

An optional, ability-driven ECS extension for ZFramework. The package is isolated from
`Assets/TEngine` and contains no project-specific rules, entities, tags, or content.

## Integration

The extension uses the public TEngine module contract and requires no changes to the
outer framework:

```csharp
using TEngine;
using ZFramework.ECS;

IEcsModule ecs = ModuleSystem.GetModule<IEcsModule>();
EntityId entity = ecs.World.CreateEntity();
```

The module is created lazily, updated through `IUpdateModule`, and shut down by
`ModuleSystem`. Remove `Packages/com.zframework.ecs` to remove the extension.

## Runtime flow

1. External code creates entities, components, tags, and ability requests.
2. `AbilityRuleSystem` evaluates declarative tag requirements.
3. Structural/tag changes are committed at stage boundaries.
4. Registered execution systems consume the resulting component/tag combinations.

System stages are deterministic: `Input`, `AbilityRules`, `Simulation`, `Lifetime`,
then `Presentation`. Systems with the same order retain registration order.
Structural changes made by systems must go through `EcsSystemContext.Commands` and
become visible at the next stage boundary.
The world and pipeline cannot be reset or structurally reconfigured during an update.

## Boundaries

- Tags express boolean facts and eligibility.
- Components hold per-entity values and runtime state.
- Ability definitions only describe activation, ongoing, lifetime, and tag transitions.
- Project code supplies concrete execution systems and content definitions.
- Framework-level events should be used at presentation or module boundaries, not as
  the ECS internal mutation mechanism.
