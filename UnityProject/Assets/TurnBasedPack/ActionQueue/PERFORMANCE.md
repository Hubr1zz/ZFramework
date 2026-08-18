# ActionQueue 性能优化审查

以下是基于当前源码的静态分析，不等同于 Unity Profiler 实测。由于单条 Chain 默认最多 128 个 Action，普通卡牌结算大概率已经足够快；优化目标应优先放在减少 GC，而不是改变正确的队列语义。

### P0：可重复基准（已实现）

当前提供三个 PlayMode 性能案例，记录总耗时、单 Chain 平均耗时与当前线程 GC 分配：

1. 100 个无 Reactor 的同步 Action。
2. 100 个 Action，每个匹配 10 个 Reactor。
3. 同一组 Composite + Reactor 链，打开和关闭 Debug 记录作对比。

基准代码位于 `Benchmarks/`，只在 Editor 或 Development Build 中编译。把
`ActionQueueBenchmarkRunner` 添加到 `ActionQueueRunner` 所在对象，进入 Play Mode 后从组件菜单执行
`ActionQueue/Performance/Run P0 Suite`。运行前需关闭 Debugger、关闭断点并等待已有 Chain 结束。
结果写入 Console，也保存在组件的 `LastReport` 中。Profiler 可同时观察
`ActionQueue.Benchmark.NoReactors`、`ActionQueue.Benchmark.Reactors`、
`ActionQueue.Benchmark.DebugRecording` 三个独立测量区间；外层 `ActionQueue.Benchmark.Suite` 包含整套流程。

性能数字依赖 Unity 版本、脚本后端、机器和 Profiler 是否附加，下面只记录本次开发机结果，目标平台仍应单独留档。

#### 2026-08-10 Editor 实测

| 案例 | ms/Chain | 基准脚本报告的 B/Chain |
|---|---:|---:|
| 100 leaf actions / no reactors / debug off | 0.5252 | 0 |
| 100 leaf actions / 10 reactors / debug off | 1.8936 | 0 |
| 100 leaf actions / 10 reactors / debug on | 11.3662 | 0 |

`0 B/Chain` 暂不能视为真实零分配：Debug 模式必然创建节点和字符串，说明当前 Unity/Mono 下
`GC.GetAllocatedBytesForCurrentThread()` 没有提供有效计数。确认 GC 应以 CPU Profiler 的
`GC Alloc` 列和 `GC.Alloc` 调用栈为准。基准入口现在会先执行一次已知 1 KB 分配校准；如果计数器仍返回 0，会在 Console 明确警告忽略 B/Chain。

### P1：调试记录按需启用（已实现）

`ActionQueueDebugService` 现在由 `runner.Debugger` 首次访问时才创建。窗口通过
`AcquireRecording()` 持有记录租约；没有租约且断点关闭时，Runner 的调试 Hook 快速返回，不创建 Action/Reactor 调试节点。

- 未打开窗口：不创建调试服务，不记录节点。
- 打开窗口：记录当前与随后发生的 Chain，并保留窗口需要的最近结果。
- 断点模式：即使没有普通租约也保持记录，关闭最后一个窗口时会释放等待器并清理记录。

代价是窗口在 Chain 中途才开启时，看不到开启之前已完成的节点；这是“零常驻记录”策略的明确取舍。

### P1：Debugger 事件驱动快照（已实现）

窗口订阅 `StateChanged`，只在版本变化时标记快照为 dirty；`OnGUI` 复用缓存快照，避免空闲时持续重建 List 和字符串。另保留 5 Hz 版本检查兜底，但版本未变化时不会 Repaint。GUIStyle 与树遍历容器也已复用。

Reactor 注册和释放会主动通知窗口，因此队列空闲时修改 Reactor 也能刷新显示。事件回调只置 dirty 和请求 Repaint，不在运行泵中直接生成快照。

### P1：无分配双端队列替代 LinkedList（已实现）

工作队列已改为 `ArrayDeque<QueueWorkItem>`：环形数组负责 O(1) 的头尾插入与头部弹出，`QueueWorkItem` 是带 `Kind` 的只读结构体。扩容之外，不再为每个工作项创建具体 WorkItem 对象和 `LinkedListNode`。

```text
QueueWorkItem
├── Kind: ActionBefore / ActionExecute / CompositeContinue / ...
├── Runtime 或 State 引用
└── Index / Outcome 等少量数据
```

状态机类型集中在 `Core/ActionQueueEngine.WorkItems.cs`，容器在 `Core/ArrayDeque.cs`，没有重新挤回 Engine 主文件。由于这一项触及执行顺序，仍需在 Unity 中用现有五个案例验证 Immediate、Bottom 和 Composite continuation 的先后关系。

### P2：降低 Reactor 收集成本——Reactor 很多时才明显

当前每个 Action 在 Before 与 After 各调用一次 `Collect`，每次都会创建候选列表、排序并创建 Invocation/Context。可按以下顺序优化：

1. 缓存 `action.Source`、`action.Target` 和 `action.GetType()`，避免同次收集重复访问。
2. 当前 `AddRegistrations` 为每条 Registration 都线性扫描已有 Candidate；由于注册 `Order` 本来就是唯一的，普通 Global 收集也可能达到 O(R²)。可先特判 Source 与 Target 是同一对象时以 `Source | Target` 收集一次，其余注册直接追加，使候选构建回到 O(R)。
3. 在排序前过滤 `Timing` 与不兼容的 `ObservedActionType`，减少排序元素。
4. 将内部 `ReactorInvocation` 改为只读结构体；若兼容性允许，再评估把 `ReactionContext` 改为只读结构体，或由 Invocation 内联保存字段，减少每个匹配 Reactor 的对象分配。
5. 根据 Profiler 决定是否池化候选/Invocation List。
6. 只有在同类 Action 与大量常驻 Reactor 被反复结算时，再考虑“按具体 Action 类型 + Registry 版本”的匹配缓存。

不要一开始就缓存 `Matches` 的结果，因为它可能依赖当前血量、Outcome 或其他动态状态。

### P2：避免无 Reactor 阶段的状态机分配——常见场景收益稳定

即使某个阶段没有匹配 Reactor，目前仍会创建 `ReactionBatchState`、`ReactionResponse` 和 continuation WorkItem。可以让 `BeginReactionBatch` 在 `invocations.Count == 0` 时走无 Reactor 快路径，直接进入 `FinishBefore` 或 `FinishResolved`。

实现时不要复用一个公开可修改的全局 `ReactionResponse`；应使用内部只读空响应，或把“无响应”改成单独回调路径。

### P2：延迟格式化循环轨迹——收益小而安全

`ActiveChain.AddTrace` 当前会在每个 Action 进入时构造 `"Action <= Cause"` 字符串，但它通常只在超过循环上限时使用。可以在固定容量环形缓冲中保存 `(actionName, cause)`，仅在真正报警时拼接字符串。

### P2：清理空 Entity 注册桶——长时间战斗的内存稳定性

Entity Reactor Dispose 后会从 List 删除，但 `_entity` 字典仍保留实体键和空 List。大量临时单位反复生成、死亡时，这会持续持有实体引用。建议在实体列表变空时同时从字典移除对应 key。

### 暂不建议

- **并行执行 Action/Reactor**：当前规则依赖严格顺序与可变 Response，并行会破坏确定性，得不偿失。
- **移除 Action 数量上限**：这不是性能限制，而是资源耗尽保护。
- **缓存 `Matches` 结果**：动态游戏状态会让缓存过期，容易产生错误结算。
- **立即池化所有 GameAction**：Action 是一次性且常含异步状态；没有明确生命周期协议时，池化更容易造成旧状态泄漏。

### 推荐实施顺序

| 顺序 | 优化 | 主要收益 | 风险 |
|---|---|---|---|
| 1 | Profiler 基准与 GC 记录 | 判断真正瓶颈 | 低 |
| 2 | Debug 按需记录 + 事件驱动快照 | 大幅降低开发期 GC | 低至中 |
| 3 | 无 Reactor 快路径 | 减少常见链路分配 | 低 |
| 4 | 清理空 Entity 桶、缓存本次 Collect 属性 | 内存稳定、低成本 | 低 |
| 5 | 环形双端队列 + 结构化 WorkItem | 降低密集链路 GC | 中 |
| 6 | Reactor 列表池化和类型缓存 | 大规模 Reactor 场景 | 中至高 |


## 已实施状态

P0/P1 源码已完成，并通过项目 C# 编译检查。具体分布如下：

| 模块 | 文件 | 职责 |
|---|---|---|
| Core | `Core/ArrayDeque.cs` | 可扩容环形双端队列 |
| Core | `Core/ActionQueueEngine.cs` | 与 UnityEngine 无关的队列执行泵 |
| Core | `Core/ActionQueueEngine.Actions.cs` | Action、Composite 与循环保护 |
| Core | `Core/ActionQueueEngine.Reactions.cs` | Reactor 批次和队列位置语义 |
| Core | `Core/ActionQueueEngine.WorkItems.cs` | 结构化工作项和运行状态机 |
| Debugging | `Debugging/ActionQueueDebugService.cs` | 按需记录、版本和断点状态 |
| Debugging | `Debugging/ActionQueueEngine.Debug.cs` | 与 Engine 私有状态相接的调试 partial |
| Unity | `Unity/ActionQueueRunner.cs` | MonoBehaviour 生命周期和 API 转发 |
| Editor | `Editor/ActionQueueDebuggerWindow.cs` | 事件驱动、快照缓存和可视化 |
| Benchmarks | `Benchmarks/*.cs` | P0 基准案例、报告与入口 |

Unity Play Mode 的实际性能数字和行为案例仍应由项目所在 Unity Editor 执行；命令行 C# 编译不能替代主线程 Profiler 数据。P2 保留为后续候选，必须由这套基准和 Profiler 证据驱动。
