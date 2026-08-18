# ZFramework RTS 维护计划与测试指南

## 1. 定位

RTS 是 Editor 优先的逻辑实验层，不是 Player 热更新或安全沙箱。Unity/ZFramework 负责场景、资源、物理和主循环；RTS 负责普通 C# 玩法实例的创建、Tick、状态迁移和快速替换。

核心原则：只维护一份纯 C# Data 规则实现。Editor 开发态和正式 Unity 端分别使用薄 Adaptor，Unity View 只负责表现与资产引用；正式化输出增量模块，默认 Player 不包含 RTS。

## 2. 当前完成度

已实现：

- Unity 无关的 `ZFramework.RTS.Contracts`。
- `ScriptRuntimeModule : Module, IUpdateModule` 集中调度。
- `ScriptAnchor` 和 Transform 能力适配。
- `DynamicAssemblyScriptProvider` 与 `StaticScriptProvider`。
- 活动实例状态迁移、替换失败回滚和逐实例异常熔断。
- 外部 Roslyn CLI、Session 编译入口和 Kernel 冒烟测试工具。
- 重新加载当前 ZFramework 主场景，同时保留 Provider、ModuleSystem 和启动/资源流程。
- Editor 文件监听，Play Mode 中保存当前 `RTSWorkspace/Sessions/<Session>/Sources` 后自动编译并热替换。
- 每个脚本实例独立的 `IScriptScope`，随解绑、替换失败和运行时关闭统一清理。
- 零 RTS 正式源码导出、旧静态 Provider 产物清理与 Player 构建门禁。
- Editor-safe API 基线策略、100 代 Kernel 压力测试和场景连续重启入口。
- Unity Test Framework Play Mode 集成测试：完整 ZFramework 启动、RTS 场景加载、脚本 Tick 和三次场景重启。

暂不承诺：旧动态程序集单独卸载、强安全沙箱、死循环隔离。常驻外部编译 daemon 降为可选性能优化；当前文件监听已经闭合 MVP 工作流。

## 3. Provider 与同一源码双入口

玩法规则只写一次。下面的 `IScript` 只是 RTS Adaptor，真正规则应放在不引用 RTS/Unity 的 Data 类型中：

```csharp
[ScriptId("enemy.chaser")]
public sealed class EnemyChaser : IScript
{
    public void Bind(IScriptContext context, IWorldObject owner, string config) { }
    public void RestoreState(ScriptState state) { }
    public void Start() { }
    public void Tick(in ScriptTime time) { }
    public ScriptState CaptureState() => ScriptState.Empty;
    public void Dispose() { }
}
```

Editor 动态路径无需额外注册，加载 DLL 后按 `[ScriptId]` 反射发现类型。静态 Provider 注册表仍可用于 Editor 测试或迁移期兼容，例如：

```csharp
var provider = new StaticScriptProvider("editor-static-test")
    .Register<EnemyChaser>("enemy.chaser");

ModuleSystem.GetModule<IScriptRuntimeModule>().ReplaceProvider(provider);
```

它不再是默认正式 Player 路径。正式化从 Control Center 执行，按 `Assets/GameScripts/Generated/RTS/<Session>/ExportNNNN` 导出；只归档当前 Session 的旧版本，其他 Session 最新版可共存。工具不生成 Bootstrap Prefab，也不修改现有场景或启动流程。

权威模块采用 Data / Adaptor / View 三层：纯 C# Data 文件可由两端直接共享；RTS 与正式 Adaptor 分开维护；Unity View 持序列化资产引用。小型边界可用条件编译，但不再推荐把三层塞入一个巨大文件。生成目录禁止手工修改。

弊端：

- 权威文件中仍有两层薄入口，条件编译边界和标记需要遵守约定。
- RTS 状态迁移到正式入口不是自动语义转换；需要持久化的状态应放在纯业务核心或显式数据模型中。
- 新增 Unity 能力时通常要同时补 RTS 适配器和传统入口，存在少量维护成本。
- 导出是源码复制与条件编译，不会理解任意架构；业务核心若直接依赖 RTS 类型，工具会拒绝导出。

## 4. 场景级运行时重启

`RestartCurrentScene` 使用 ZFramework `ISceneModule.LoadSceneAsync` 以 `Single` 模式重新加载 `CurrentMainSceneName`：

```csharp
ModuleSystem.GetModule<IScriptRuntimeModule>().RestartCurrentScene(
    progress => Log.Info("Restart {0:P0}", progress),
    (ok, error) => Log.Info(ok ? "Restarted" : error));
```

重启时：

1. 旧场景的 Anchor 收到 `OnDisable`，RTS 实例 Dispose。
2. ZFramework 的持久化 GameEntry、RootModule、ModuleSystem、资源包和当前 RTS Provider 保留。
3. 新场景加载后 Anchor 再次注册，使用当前 Provider 创建全新脚本实例。
4. 不重新进入 Launch、Splash、InitPackage、InitResources 或下载流程。

默认 `gcCollect=false`，避免把“快速场景重启”变成一次完整资源清理。Bootstrap/build index 0 场景被拒绝重启，以避免复制 `DontDestroyOnLoad` 的 GameEntry。

当前重启会重置场景和场景脚本状态；不会保留旧 `ScriptState`。如果以后需要“场景重载但恢复玩法快照”，应另加显式 Snapshot 模式。

### 4.1 ScriptScope

每次脚本实例都会获得独立的 `context.Scope`。事件退订、Timer 移除、取消令牌取消等动作应注册到 Scope：

```csharp
context.Scope.Register(() => eventBus.Remove(handler));
context.Scope.Register(() =>
{
    cancellation.Cancel();
    cancellation.Dispose();
});
```

Scope 按注册逆序清理并保证自身幂等。脚本仍可在 `Dispose` 中释放普通内部状态，但同一外部资源不要同时手动释放和注册到 Scope。

## 5. 手工测试流程

### 5.0 一键启动（推荐）

1. 回到 Edit Mode。
2. 打开 `ZFramework > RTS > Control Center`，在“手动工具”选择 `RtsTest` 并启动。
3. 工具会补齐 YooAsset 所需目录、生成 `Assets/AssetRaw/Scenes/RTSTest.unity`、编译当前 Session 脚本并从 `Assets/Scenes/main.unity` 启动。
4. 完整 ZFramework Procedure 到达 `ProcedureStartGame` 后，工具通过 `ISceneModule` 加载 `RTSTest`，并装载最新 RTS DLL。
5. 修改当前 Session 的 `Sources` 后保存，watcher 会自动编译并应用最新健康代。

Control Center 默认启用自动热替换。保存 `.cs` 后约 350ms 防抖并编译装载；显式编译和场景重启只保留在“手动工具”页作为恢复入口。

测试场景只在首次缺失时生成；需要恢复固定 Bootstrap 时在“手动工具”执行“重建 RTSTest”。该命令会覆盖测试场景。

### 5.1 编译检查

在仓库根执行：

```powershell
dotnet run --project UnityProject/Packages/com.zframework.rts/Tools~/KernelSmokeTests/ZFramework.RTS.KernelSmokeTests.csproj --configuration Release
```

打开 Unity 或用 BatchMode 编译一次，使下面文件存在：

```text
UnityProject/Library/ScriptAssemblies/ZFramework.RTS.Contracts.dll
```

然后在 `UnityProject` 目录执行：

```powershell
pwsh -File Packages/com.zframework.rts/Tools~/compile-sample.ps1 -Configuration Release -Session '<SessionId>'
dotnet build Packages/com.zframework.rts/Tools~/ProductionCompileSmoke/ZFramework.RTS.ProductionCompileSmoke.csproj `
  -p:UnityManagedPath='<Unity Editor>/Data/Managed/UnityEngine'
```

第二条命令以 Unity 正式分支宏编译同一权威源码，提前发现传统入口的语法、类名和 Unity API 引用问题。

### 5.2 动态脚本测试

1. 打开一个通过 ZFramework 正常启动后进入的玩法场景，不要停留在 Launcher/build index 0。
2. 在 Session Sources 中创建带 `[ScriptId("sample.session-entry")]` 的入口 `IScript`。
3. 给任意 GameObject 添加 `ScriptAnchor`，`Script Id` 填 `sample.session-entry`。
4. 根据脚本约定填写 `Initial Config`。
5. 进入 Play Mode。
6. 在 Control Center“手动工具”执行“装载已编译 DLL”。
7. 选择 `Library/ZFrameworkRTS/Compiled/Sessions/<Session>/` 中最新 DLL。
8. 入口脚本应运行，结构化运行状态应报告健康代与活动实例。
9. 修改入口脚本并保存，验证状态迁移和原子热替换。

### 5.3 场景重启测试

1. 保持 Play Mode 和已加载的 RTS Provider。
2. 在 Control Center“手动工具”执行“重启当前玩法场景”。
3. 场景对象应销毁后重新生成，对象位置回到场景初值。
4. 新 Anchor 应按当前 Session 的入口 ScriptId 自动创建实例，无需重新加载 DLL。
5. Launcher/Splash/资源初始化界面不应再次出现。
6. Console 应显示 `[RTS] Current scene restarted.`。

也应测试：连续点击重启、加载场景过程中重启、停在 bootstrap 场景重启、ScriptId 不存在、脚本 Dispose 抛异常。

真实场景连续重启可执行“场景重启压力测试 ×10”。Kernel 冒烟测试会执行 100 代 Provider 替换并检查实例数量、状态迁移和 Scope 清理。

发行 package 不附带玩法 PlayMode 测试程序集。接入项目应在自己的测试目录中覆盖正式启动流程、Session 激活、失败回滚和场景重启。

## 6. 架构边界

- Unity 2022 MVP 用 `Assembly.Load` 代际加载，不承诺旧程序集单独卸载。
- 普通异常可隔离；死循环、OOM、StackOverflow 和 native 调用不能隔离。
- Kernel 的事务只保证活动实例表回滚，不能撤销候选脚本已经造成的外部副作用。
- 动态脚本技术上能引用 UnityEngine，但推荐只使用窄能力接口。
- 当前编译器只引用 `netstandard2.1 + Contracts`，并阻止 IO、网络、进程、反射和原生互操作等直接 API；这只是误用防线，不是安全沙箱。
- 静态 Provider 仍携带 RTS Contracts、Runtime 和注册代码，只用于 Editor 测试或迁移期兼容，不是默认交付形态。

### 6.1 与 ZFramework 启动模式的关系

RTS 适合进入 ZFramework Editor 扩展的启动选项，但应作为独立“启动目标”，不扩展 YooAsset `EPlayMode`：

```text
资源模式：EditorSimulate / Offline / Host / Web
启动目标：Normal / RTS Test (Editor)
```

两组选项正交。当前 `Start ZFramework Test Flow` 是启动目标的最小实现；未来可在现有 ZFramework Inspector/Toolbar 中复用该命令，不修改资源模式枚举或运行时资源契约。

### 6.2 零 RTS 正式包目标

正式交付的默认目标是：RTS 只存在于 Editor 开发流程，Player 尽量不携带任何 RTS 专属代码。不能只依赖 IL2CPP“可能裁剪”，而要提供显式的零 RTS 正式化与构建检查。

验收标准：

- Player 不包含 `ZFramework.RTS.Contracts`、`ZFramework.RTS.Runtime` 或任何 Editor/Roslyn 编译程序集。
- 正式场景和 Prefab 不保留 `ScriptAnchor`。
- 正式源码不实现 `IScript`，不保留 `ScriptIdAttribute`、动态/静态 Provider、状态热迁移和 `RtsStaticRegistry`。
- 正式玩法逻辑转换为普通项目 C#，由传统 MonoBehaviour、ZFramework Module/Procedure 或项目自己的生命周期入口驱动。
- 构建前扫描程序集、场景、Prefab 和生成目录；发现 RTS 类型或引用时直接阻止正式构建。

当前实现：`Contracts` 与 `Runtime` asmdef 均限制为 Editor；外部 Roslyn 编译定义 `ZFRAMEWORK_RTS`；正式导出只让 Unity 的传统入口分支生效。构建前 `RtsZeroBuildGuard` 检查 Player 程序集、旧注册表目录、活动 Generated 源码，以及启用 Build Scene 对 RTS Runtime/Contracts 的依赖。未加入 Build 的 RTSTest 可以继续作为开发资产保留。

自动导出的前提是业务核心不依赖 `IScriptContext`、`IWorldObject` 或其他 RTS 类型，RTS 脚本只作为薄适配器。符合该约束时工具可以丢弃 RTS 适配器并生成传统入口；不符合时必须列出依赖并中止自动导出，转为人工正式化，不能用文本替换冒充可靠转换。

### 6.3 正式化与 CI 顺序

1. 调用 `ZFramework.RTS.Editor.RtsTestFlow.PrepareTestAssetsForBatch`，生成测试场景及所有权标记。
2. 运行 Kernel、当前 Session 的 Roslyn 编译和接入项目自有的集成测试。
3. 调用 `ZFramework.RTS.Editor.RtsSourcePromotion.ExportZeroRtsForBatch`，按 session/version 生成增量正式源码，不修改场景。
4. 调用 `ZFramework.RTS.Editor.RtsZeroBuildGuard.ValidateForBatch`。
5. 执行真实 Player/IL2CPP 构建，并在 CI 检查最终产物中不存在 `ZFramework.RTS` 程序集或类型名。

## 7. 下一步

目标不是继续增加底层概念，而是缩短“提出玩法想法 → 看到效果 → 安全正式化”的路径，并让 Provider、条件编译和生成目录尽量对普通使用者不可见。

### P0：决定实际开发增效

- [x] 将同步 PowerShell 编译改为异步、可取消、可合并的编译队列，禁止编译等待阻塞 Unity 主线程。
- [x] 支持项目级设置、多个源码目录、多个编译单元和显式项目程序集引用白名单。
- [x] 提供统一 RTS 控制台：Normal/RTS Test 启动目标、当前代数、活动实例、耗时、诊断、取消、重启、正式化和验证入口。
- [x] 通过 ResourceModuleDriver Inspector 扩展接入 ZFramework 启动目标，同时保持启动目标与 YooAsset 资源模式正交。
- [x] 正式化增加 Dry Run、文件变更/删除清单、确认和 `Library/ZFrameworkRTS/PromotionBackups` 可恢复备份；保留未变化脚本的 `.meta`/GUID。
- [x] 使用独立 Roslyn 语法树导出器按 `UNITY_5_3_OR_NEWER` 生成正式源码并移除禁用分支，不再使用行文本剥离。
- [x] 提供真实 Player/可选 IL2CPP BatchMode 构建与最终产物扫描入口；构建前仍执行零 RTS 门禁。

### P1：降低学习、误操作与维护成本

- [x] `ScriptAnchor.ScriptId` 使用可搜索目录，检查缺失/重复；`RtsParameter` Schema 驱动结构化参数并兼容旧字符串。
- [x] 玩法向导生成纯 Data、RTS/Production 双端 Adaptor 与带稳定资产映射字段的 Unity View；不生成 Bootstrap，已有启动流程负责接入。
- [x] 编译诊断支持文件/行号点击，显示最近耗时与 P95，并明确失败时旧健康逻辑仍运行。
- [x] 显示动态代数与估算内存，阈值告警退出 Play Mode；可恢复最后健康 Provider，Editor 重启不自动加载旧 DLL。
- [x] 稳定资产键具备 JSON 映射、正式化清单，以及导出/构建依赖门禁；塔防 Dummy 创建不进入通用产品菜单。
- [x] 提供 v1 目标、伤害、投射物、特效、对象池、动画、音效和计时能力接口。
- [x] 模板向导强制 Data / Adaptor / View 边界；具体 Module/Procedure 接入由既有项目 composition root 决定。
- [x] 提供 Preserve/Reset/RequireCompatibleSchema 三种热替换状态策略，并在内核烟测覆盖保留、重置和失败回滚。

### P2：AI 长任务与规模化

- [x] `RTSWorkspace/rts-workspace.json` 汇总源码、能力、资产清单和测试命令，设置变化时可自动再生成。
- [x] 白名单任务队列默认使用编译与结构化运行数据验证；场景重启和正式验证为显式任务，限制步数/超时并支持人工确认点和取消。
- [x] 默认启用常驻编译 daemon，复用 Roslyn 进程；设置中可关闭并回退一次性编译进程。
- [x] 编译单元可独立配置源码、引用和输出，并用跨进程文件锁隔离；正式化输出也有独占锁。
- [x] 发行 package 不携带玩法垂直样例、截图或 PlayMode 测试程序集。

### 验收指标

- 保存到画面反馈：P50 小于 700ms、P95 小于 1.5s，且 Editor 主线程无明显冻结。
- 编译或替换失败后旧逻辑继续运行，并能一键定位错误和恢复健康代。
- 一次正式化所需人工修改接近零，导出操作可预览、可重复、可恢复。
- 新使用者十分钟内可完成创建、修改、热替换、场景重启和正式化全过程。
- Dummy 资产、RTS 程序集和启用的正式 Build Scene 引用不会进入正式 Player。

## 8. P0 使用入口

- `ZFramework > RTS > Control Center`：统一启动、编译、重启和正式化工作台。
- `Project Settings > ZFramework RTS`：配置编译单元、源码目录、引用程序集、输出目录和主场景。
- `ResourceModuleDriver Inspector`：只提供 Control Center 快捷入口，避免分散手动功能。
- `Control Center > Agent 工作流`：默认入口；管理 watcher、Workspace、状态和验证队列。
- `Control Center > 手动工具`：启动、恢复、诊断和压力测试。
- `Control Center > 正式化`：Dry Run、session/version 导出、回滚和 Zero-RTS 验证。
- `Tools~/CI/build-zero-rts-player.ps1`：BatchMode 正式化、Player/IL2CPP 构建与产物残留扫描。
