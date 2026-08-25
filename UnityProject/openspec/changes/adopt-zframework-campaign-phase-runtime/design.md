## Context

现有非决战年度闭环已可运行，但 `GameManager` 同时拥有 Unity 场景引用、阶段 FSM、跨阶段 ActionEnvironment scope 和存档门禁。本阶段迁移同属一个战役世代的 FSM、共享 registry、发明安装租约和 Campaign Runner；阶段领域状态与阶段 Session 暂不迁移。

## Decisions

### Module 空闲初始化，lease 建立运行世代

`PlayableCampaignRuntimeModule.OnInit` 不读取内容或场景。`GameManager.Awake` 获取独占 `IPlayableCampaignRuntime`；lease 内创建 `PhaseManager` 与共享 installer registry，并转发阶段表现回调。

### 单一活动运行态

模块同一时刻只允许一个活动 lease。每次成功获取递增 `GenerationId`；旧 lease 释放后才能建立新世代，防止多个场景宿主争用固定名称的 ZFramework FSM。

### Reset 与 Dispose 分离

启动或读档失败使用 `Reset` 清空 FSM 与 Campaign Runner，但保留当前 lease 和外部注册的战役 installer，以便玩家重试。当前发明 installer 属于 gameplay shell，Reset 时解除并在下次启动重新注册。`GameManager.OnDestroy` 先释放阶段 Session，再由 lease 释放 Campaign Runner、installer 租约、registry、FSM 和模块占用；`ModuleSystem.Shutdown` 也能幂等释放。

### 共享 registry 只有 owner 可销毁

Settlement、Hunt 与 Combat Session 继续各自创建短生命周期 ActionEnvironment，并 attach 到 runtime 的同一个 registry。消费者接口不暴露 `Dispose`；只有具体 registry 和 runtime owner 能结束整个战役 scope。

### 保持 ActionQueue 边界

运行态的创建、重置和释放是生命周期管理，不创建 `GameAction`。玩家阶段切换和遭遇请求必须进入 runtime 内的 Campaign ActionSession，不保留 Session 缺失时直接提交的旁路；启动、恢复和回滚可调用内部 FSM 边界。View 显隐仍由 `GameManager` 的阶段回调负责。

## Risks / Trade-offs

- 本阶段没有迁移 Settlement/Hunt Manager、阶段 ActionSession 或稳定存档载荷，`GameManager` 仍较大。
- 当前 lease 在场景宿主销毁时一并释放；无场景 Host 的跨场景常驻运行态留到后续迁移职责簇。
- `PhaseManager` 仍复用固定 FSM 名称；独占 lease 是当前防止同名争用的门禁。
