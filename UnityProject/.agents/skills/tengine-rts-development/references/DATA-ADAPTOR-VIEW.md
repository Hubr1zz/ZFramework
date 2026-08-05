# Data / Adaptor / View reference

## Ownership

### Data

Data is ordinary C# with no engine reference. It owns configuration, deterministic state, rules, commands, and snapshots.

```csharp
public sealed class AbilityData
{
    public float FireInterval { get; }
    public float Cooldown { get; private set; }

    public AbilityData(float fireInterval) => FireInterval = fireInterval;

    public bool Tick(float deltaTime, bool hasTarget)
    {
        Cooldown = Math.Max(0f, Cooldown - deltaTime);
        if (!hasTarget || Cooldown > 0f) return false;
        Cooldown = FireInterval;
        return true;
    }
}
```

The same file is compiled by Roslyn for RTS and by Unity for production. It needs no conditional compilation.

### View

View owns Unity objects and presentation references. Serialized Prefab fields belong here, not in Data.

```csharp
public sealed class CombatPreviewView : MonoBehaviour
{
    [SerializeField] private GameObject actorPrefab;
    [SerializeField] private GameObject supportEffectPrefab;

    public GameObject Spawn(string assetKey, Vector3 position) { /* map and pool */ }
    public void Despawn(int handle) { /* return to pool */ }
}
```

### Adaptors

The RTS adaptor binds Data to stable capabilities such as `IRtsWorldServiceV1`. It can map Data commands to logical world keys such as `actor/support/17` and asset keys such as `unit.support`, but it cannot own cooldown or support rules.

The production adaptor binds the same Data to `CombatPreviewView` and the existing TEngine procedure/module/scene startup. It is created by that existing composition root. It must not auto-create itself.

For an existing production feature, prefer an `InContext` Session: start through the real main scene and Procedure/Module chain, reference the production service boundary, and activate only the Session-owned delta at the stable RTS extension point. Do not clone the production implementation into Session Sources.

## Stable asset keys

A stable asset key is a semantic identifier such as `unit.support`, `ability.rapid`, or `effect.support-pulse`. It is deliberately independent of a Unity path, GUID, address, Prefab instance, or RTS primitive.

Mappings are environment-specific:

```text
unit.support -> RTS preview: capsule + green tint
unit.support -> Production: Addressables/Prefab registered in CombatPreviewView
```

Keys let Data request meaning while the View selects representation. Renaming or moving a Prefab does not change gameplay code. Missing keys are validation errors, not an excuse to create a hardcoded fallback in production.

## Can both sides read the same C# file?

Yes for pure Data, shared commands, snapshots, and engine-neutral interfaces. Removing unnecessary `global::` makes those files easier to read but is not what makes sharing possible; dependency purity is.

RTS and production still differ in lifecycle, available references, object ownership, serialization, asset mapping, state migration, and startup integration. Keep those differences in small adaptor files. A conditional source file is acceptable for a tiny boundary, but separate adaptor files are usually clearer and prevent accidental rule duplication.

## Capability evolution

`IRtsWorldServiceV1` and similar published contracts are stable infrastructure. Their implementations and project-specific adapter set grow as development needs new presentation abilities. Add a new optional capability or `V2` for breaking semantics. Most gameplay features should only add Data, keys, and thin adapters; only genuinely new engine abilities justify changing the stable host and paying for a Unity compilation.
