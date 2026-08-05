# TEngine RTS 简介与上手指南

## 1. RTS 解决什么问题

RTS 把高频玩法实验从 Unity AssetDatabase 脚本编译中分离出来。正常循环是：

```text
选择 Session → Agent 分析正式基线与复用点 → 修改 Session/Sources → 自动编译/换代 → 读取运行状态与结构化报告验证
```

这个循环不要求退出 Play Mode，不重载场景，也不触发 Domain Reload。稳定宿主、Contracts、Capability 或 asmdef 的变化属于低频基础设施工作，仍需要 Unity 编译。

打开 `TEngine > RTS > Control Center`。顶部只有这一个 RTS 菜单：

- **Agent 工作流**：session、自动热替换、Workspace、运行状态和 Agent 验证队列。
- **手动工具**：显式启动、恢复、重建 RTSTest、装载 DLL、压力测试和三层骨架生成器。
- **正式化**：Dry Run、session/version 导出、回滚和 Zero-RTS Player 验证。
- **简介与指南**：架构摘要、固定场景路径和文档入口。

## 2. 第一次运行

1. 退出 Play Mode，等待一次稳定宿主编译完成。
2. 打开 Control Center，在“手动工具”中选择 `RtsTest`。
3. 点击“启动所选目标”。
4. 回到“Agent 工作流”选择 Session 和运行配置，保持“保存外部源码后自动编译并热替换”开启。
5. 点击“启动当前 Session”。Agent 只修改 `RTSWorkspace/Sessions/<Session>/Sources/` 下的源码。

编译失败或新代激活异常不会杀死当前健康玩法。连续保存会合并，最终只应用最新结果。Mono 下旧动态程序集不能卸载；达到 Control Center 的代数/内存提示后，安排一次低频维护性退出 Play Mode 来回收内存。

### Session、编译单元和运行配置

- **Session** 是功能工作区，拥有源码、入口 ScriptId、资产映射、任务、运行状态、验证报告和正式化历史。
- **编译单元** 只定义额外源码根、程序集引用白名单和编译器输出边界；不要为了每个玩法功能创建编译单元。
- **Sandbox** 通过固定 RTSTest 隔离验证。
- **InContext** 先走正式主场景和 TEngine Procedure，再挂载 Session 增量，用于验证与正式加载流程的一致性。

Agent 修改前必须更新 Session 的 `reuse-analysis.md`：优先复用正式程序集、已有服务和单一所有者的共享 Data，禁止把正式规则复制回 Session。

Entry ScriptId 不能随便填写。它必须与入口 `IScript` 类型上的 `[ScriptId("...")]` 完全一致，并在当前 Session 及其依赖闭包中唯一。建议使用稳定、带命名空间的形式，例如 `combat.damage-preview`；只使用小写字母、数字、点、横线和下划线。

默认验证队列是 `compile → validate-runtime-data`，检查编译结果、运行时错误、健康代、加载代数和活动实例，不自动截图。截图不再是 Agent 验证步骤。

当前支持“同一 Session 内热更新”，不支持事务式跨 Session 热切换。切换 Combat/Exploration 等 Session 前先退出 Play Mode，再选择并启动；界面会在运行中锁定 Session 选择，防止显示状态与实际 Provider 不一致。

## 3. RTSTest 在哪里

RTSTest 是工具维护的固定开发场景：

```text
Assets/AssetRaw/Scenes/RTSTest.unity
```

它位于 YooAsset 原始资源目录，因此不在常见的 `Assets/Scenes` 下。场景只负责稳定测试入口和通用 Host；Session 入口 ScriptId 位于各自 `session.json`。运行时会创建临时 Session Overlay，不把玩法 Anchor 固化进测试场景。

只有场景丢失或稳定 Bootstrap 损坏时，才在“手动工具”点击“重建 RTSTest”。这会覆盖该测试场景，不是日常玩法修改步骤。正式化不会删除、重建或改写 RTSTest；只要它没有加入 Player 的启用 Build Scenes，就不会成为正式启动入口。

## 4. 必须采用 Data / Adaptor / View

### Data

Data 是唯一规则与状态所有者，例如伤害、冷却、任务进度、胜负和快照。它是无条件编译的普通 C#，不能引用任何引擎 API。

```csharp
public sealed class AbilityData
{
    public float Cooldown { get; private set; }
    public bool Tick(float deltaTime, float fireInterval, bool hasTarget)
    {
        Cooldown = Math.Max(0f, Cooldown - deltaTime);
        if (!hasTarget || Cooldown > 0f) return false;
        Cooldown = fireInterval;
        return true;
    }
}
```

### Adaptor

RTS Adaptor 把 `ScriptTime`、`IRtsWorldServiceV1` 等稳定能力翻译成 Data 输入和表现命令；正式 Adaptor 把同一 Data 接入已有 Procedure、Module 或场景生命周期。Adaptor 不复制规则。

### View

View 使用 Unity API，持有 `[SerializeField] GameObject`、材质、音效和对象池。它接收 Data/Adaptor 的表现命令，但不判断伤害、波次或胜负。

Control Center 的“创建 Data / Adaptor / View 骨架”会生成共享 Data、RTS Adaptor、Production Adaptor 和 Unity View 四个边界文件，不再生成单个充满条件分支和 `global::` 的文件，也不会生成 Bootstrap。

## 5. 稳定资产键

稳定资产键是与路径、GUID、Prefab 实例无关的语义 ID，例如：

```text
unit.support
ability.rapid
effect.support-pulse
```

RTS 测试端可以把 `unit.support` 映射成绿色 Capsule；正式 View 则把同一键映射成项目 Prefab。Prefab 移动或换美术资源时，Data 不变。每个 Session 在 `RTSWorkspace/Sessions/<Session>/asset-map.json` 维护覆盖映射；缺失的正式映射会阻止导出。

## 6. Adapter 与 Capability 如何演进

已发布的版本接口（例如 `IRtsWorldServiceV1`）是稳定代码：兼容能力可以增量添加，破坏性语义应新建 `V2`，不能偷偷修改 `V1`。Unity 侧 Capability 实现和项目 Adapter 会随着需要增量增长。

判断标准：

- 新规则、新数值、新波次：只改 Data/外部 RTS 源码，不编译 Unity。
- 已有能力的新用法：增加薄 Adapter，不改稳定 Host。
- 真正缺少引擎能力：新增 Capability/版本和 Unity 实现，接受一次基础设施编译。

## 7. 同一个 C# 文件与 `global::`

纯 Data、命令和快照可以由 RTS 与正式 Unity 直接读取同一个 `.cs` 文件。能否共享取决于依赖是否纯净，不取决于是否写了 `global::`；普通 `using` 足以处理绝大多数类型，只有真实命名冲突才需要完全限定名。

两端仍需处理不同的生命周期、程序集引用、Unity 序列化、Prefab 映射、对象所有权、热换状态迁移和启动流程。因此推荐共享 Data 文件、分开两个小 Adaptor，而不是用一个巨大条件编译文件掩盖差异。

## 8. Session 化正式化

先在 Control Center 选择 Session，例如 `CombatFeature`。每次导出进入：

```text
Assets/GameScripts/Generated/RTS/CombatFeature/Export0001
Assets/GameScripts/Generated/RTS/CombatFeature/Export0002
```

每个 Session 的最新 Export 是活动正式源码；该 Session 的旧 Export 会保留为 `.cs.snapshot`。战斗和探索等不同 Session 的最新版可以同时进入正式编译。每版包含所有权标记和 manifest。

正式化是增量输出：

1. 退出 Play Mode。
2. 运行 Dry Run，解决资产键和 RTS 残留阻塞项。
3. 确认导出。
4. 在已有场景/Procedure/Module 的 composition root 中显式创建 Data，并注入正式 Adaptor/View。
5. 把目标正式场景加入 Build Settings，运行“验证 Zero-RTS Player”，再直接启动或构建 Player 验证。

不会生成 Bootstrap Prefab，也不会自动挂载任何东西。这个限制避免 RTS 增量抢占项目已有启动流程。

## 9. 关于旧的硬编码 RTS 脚本

正式化工具是源码转换器，不会可靠地把任意旧脚本自动重构成三层架构。如果旧脚本在 Unity 分支中 `CreatePrimitive`、自动启动或复制规则，导出后仍可能保留这些设计。新的生成器和 Agent skill 会在设计阶段阻止此类结构；旧功能应先重构为 Data / Adaptor / View，再正式化。

## 10. Zero-RTS 验证

“验证 Zero-RTS Player”检查：

- Player 编译中没有 RTS 程序集；
- 启用的 Build Scene 不依赖 RTS Contracts/Runtime；
- 活动的 Generated `.cs` 不含 RTS Provider、Anchor 或接口残留；
- 正式资产映射已解决。

RTS PlayMode 测试 asmdef 必须排除 Player；正式场景也不能保留 `ScriptAnchor`。验证通过后，直接从已有正式启动场景运行，不需要 RTS 手动挂载。CI 可继续使用 `Tools~/CI/build-zero-rts-player.ps1` 构建并扫描 Player 输出。
