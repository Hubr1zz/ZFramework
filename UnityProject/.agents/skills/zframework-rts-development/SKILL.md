---
name: zframework-rts-development
description: Design or modify ZFramework RTS Sessions, Session Sources, world recipes, adapters, stable asset mappings, and formalization output. Use whenever an RTS experiment must remain clean enough to become incremental Unity production code without duplicated rules, hardcoded assets, generated bootstraps, or engine APIs in gameplay data.
---

# ZFramework RTS Development

Use one authoritative gameplay model in both live RTS and formal Unity execution. Treat hot reload and production as two adapters around the same pure C# Data layer, never as two implementations of the rules.

## Required workflow

1. Read [references/DATA-ADAPTOR-VIEW.md](references/DATA-ADAPTOR-VIEW.md) before designing or changing gameplay structure.
2. Read the active `RTSWorkspace/Sessions/<session>/session.json` and `reuse-analysis.md`.
3. Inventory the existing production scene startup, lifecycle owner, asset-loading system, public services, already-formalized modules, and relevant stable RTS capability versions. Update `reuse-analysis.md` with what will be reused and what must be new before editing gameplay.
4. Put rules, state transitions, configuration values, and migration-friendly snapshots in pure C# Data code.
5. Put Unity rendering, serialized Prefab/material/audio references, and object pooling in View code.
6. Make RTS and production Adaptors translate lifecycle, input, time, asset keys, and view commands only.
7. Use `InContext` when parity with the production startup/loading flow matters; use `Sandbox` for isolated experiments.
8. Verify the dynamic compile path and the production compile path. Check that formalization is incremental and does not alter existing scenes or startup unless explicitly requested.

## Hard gates

- Data must not reference Unity, ZFramework runtime, RTS contracts, `GameObject`, `MonoBehaviour`, or engine time APIs.
- There must be exactly one implementation of each gameplay rule. Do not duplicate rules under `ZFRAMEWORK_RTS` and `UNITY_5_3_OR_NEWER` branches.
- View must not decide damage, cooldowns, waves, scoring, victory, or other gameplay rules.
- Adaptors may translate and reconcile; they must not become a second gameplay model.
- Do not emit `RuntimeInitializeOnLoadMethod`, generated Bootstrap Prefabs, scene edits, `FindObjectOfType`, or an implicit global singleton for formalized output unless the user explicitly requests that integration style.
- Do not encode Prefab paths, GUIDs, `Resources.Load` paths, primitive construction, colors, or presentation constants inside Data. Use stable asset keys and an explicit mapping owned by the View/integration layer.
- Do not generate production code dominated by `global::`. Prefer ordinary namespace-scoped files and `using` directives. Fully qualify only real ambiguity points.
- Preserve an existing scene's startup flow. Formalization produces an incremental module plus integration metadata; the project's current composition root opts into it.
- Published capability contracts are immutable within their version. Add optional capabilities or a new `V2` contract for breaking changes; do not silently mutate `V1`.
- Normal gameplay edits stay under `RTSWorkspace/Sessions/<session>/Sources/` and must not trigger AssetDatabase compilation. Never edit another Session unless it is declared as a dependency or the user explicitly changes scope. Stable host/capability changes are infrastructure work and require a Unity compile.
- Reuse production assemblies, public services, or single-owner shared Data before adding code. Do not copy formalized rules back into a Session.
- Session dependencies must be acyclic. ScriptIds, state keys, and stable asset keys must be namespaced to prevent cross-Session collisions.

## Formalization checks

- Export beneath `Assets/GameScripts/Generated/RTS/<session>/ExportNNNN/`.
- Keep only the newest export within each Session active; retain older versions of that Session as non-compiling snapshots. Other Sessions' newest exports remain active.
- Export only Session-owned deltas. Shared/production dependencies keep a single owner and must not be copied into every Session export.
- Include the session, export number, source hashes, stable asset keys, required capability versions, and integration notes in the manifest.
- Never generate or attach a Bootstrap Prefab by default.
- Refuse formalization when asset keys are unresolved, rules exist in an adapter/view, or production integration would require an unacknowledged scene mutation.
- Make preview, apply, validation, and rollback deterministic.

## Completion checklist

- The pure Data files compile without Unity or RTS references.
- RTS and production execute the same Data types.
- Hot replacement preserves declared Data state and reconciles world objects without duplication.
- Formalized code exposes normal fields/configuration at the View or integration boundary.
- Existing startup explicitly owns creation and disposal.
- No new tower-defense/example-specific editor command was added to the reusable RTS product surface.
