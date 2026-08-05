# ZEngine portable Unity project

`ZEngine` is the clean integration branch for distributing TEngine as a project archive. It combines:

- the `zWorkFlow_Integration` editor workflow and Agent Workbench;
- the embedded `Packages/com.tengine.rts` stable host, contracts, Roslyn daemon, Session management, formalization and Zero-RTS guard;
- the Unity-project-local `UnityProject/.agents/skills/tengine-rts-development` design guardrails.

It intentionally excludes gameplay Sessions, tower-defense sources, screenshots, generated RTSTest assets, Player builds, Unity layouts and PlayMode test assemblies. A new project creates `RTSWorkspace/Sessions/<Session>/` only when the first Session is created.

## Archive contents

Keep the complete `UnityProject/Assets`, `UnityProject/Packages`, `UnityProject/ProjectSettings`, and `UnityProject/.agents` directories. There is no second repository-level `.agents` directory. Do not include `UnityProject/Library`, `Temp`, `Logs`, `UserSettings`, `RTSWorkspace`, build outputs, or package `Temp~`/`bin`/`obj` directories.

From the repository root, create the clean archive with:

```powershell
git archive --format=zip --output ZEngine.zip ZEngine UnityProject ZENGINE-PORTABLE.md
```

## First open

1. Open `UnityProject` with Unity 2022.3.
2. Ensure the .NET 8 SDK is available. The first Roslyn compiler build restores `Microsoft.CodeAnalysis.CSharp` from NuGet; later daemon compiles reuse the restored tool and cached references.
3. Open `TEngine > RTS > Control Center` and create a Session.
4. Set Entry ScriptId to the exact ID declared by the Session entry type, for example `[ScriptId("combat.preview-entry")]`.
5. Use `Sandbox` for isolated work or `InContext` to enter through the real scene/Procedure flow.

The default Agent queue performs external compilation followed by structured runtime-data validation. It does not capture screenshots.
