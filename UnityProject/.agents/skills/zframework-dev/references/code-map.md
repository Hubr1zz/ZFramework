# 当前代码地图

> 由 `scripts/generate_architecture_docs.py` 从当前源码生成，不要手工修改。

## 自定义程序集

| 程序集 | 路径 | 工程内依赖 |
|---|---|---|
| `GameLogic` | `Assets/GameScripts/GameLogic` | `TEngine.Runtime`, `HuntingInDarkness.GameCore` |
| `HuntingInDarkness.GameCore` | `Assets/GameScripts/GameLogic/HuntingInDarkness/GameCore` | 仅第三方/Unity 依赖 |
| `Launcher` | `Assets/Launcher` | 仅第三方/Unity 依赖 |
| `TEngine.Editor` | `Assets/TEngine/Editor` | `TEngine.Runtime` |
| `TEngine.Runtime` | `Assets/TEngine/Runtime` | 仅第三方/Unity 依赖 |

## GameModule 入口

| 入口 | 类型 |
|---|---|
| `GameModule.Base` | `RootModule` |
| `GameModule.Debugger` | `IDebuggerModule` |
| `GameModule.Fsm` | `IFsmModule` |
| `GameModule.Procedure` | `IProcedureModule` |
| `GameModule.Resource` | `IResourceModule` |
| `GameModule.Audio` | `IAudioModule` |
| `GameModule.UI` | `UIModule` |
| `GameModule.Scene` | `ISceneModule` |
| `GameModule.Timer` | `ITimerModule` |
| `GameModule.Localization` | `ILocalizationModule` |

## 注册流程

入口：`ProcedureLaunch`

| Procedure | 直接状态切换 |
|---|---|
| `ProcedureClearCache` | `ProcedureInitContentPackages` |
| `ProcedureCreateDownloader` | `ProcedureDownloadOver`, `ProcedureDownloadFile` |
| `ProcedureDownloadFile` | `ProcedureDownloadOver`, `ProcedureCreateDownloader` |
| `ProcedureDownloadOver` | `ProcedureClearCache`, `ProcedureInitContentPackages` |
| `ProcedureInitContentPackages` | `ProcedurePreload` |
| `ProcedureInitPackage` | `ProcedureInitResources` |
| `ProcedureInitResources` | `ProcedureCreateDownloader`, `ProcedureInitContentPackages` |
| `ProcedureLaunch` | `ProcedureSplash` |
| `ProcedurePreload` | `ProcedureStartGame` |
| `ProcedureSplash` | `ProcedureInitPackage` |
| `ProcedureStartGame` | 终点/异步内部切换 |

## 非 Unity 内置包

| 包 | 版本/来源 |
|---|---|
| `com.cysharp.unitask` | `file:UniTask (embedded)` |
| `com.tengine.rts` | `file:com.tengine.rts (embedded)` |
| `com.tuyoogame.yooasset` | `file:YooAsset (embedded)` |
| `com.unity.ai.navigation` | `1.1.5 (registry)` |
| `com.unity.ide.rider` | `3.0.40 (registry)` |
| `com.unity.ide.visualstudio` | `2.0.23 (registry)` |
| `com.unity.nuget.newtonsoft-json` | `3.2.1 (registry)` |
| `com.unity.textmeshpro` | `3.0.9 (builtin)` |
| `com.unity.ugui` | `2.0.0 (builtin)` |

Unity 内置模块共 31 个，完整列表以 `Packages/manifest.json` 为准。
