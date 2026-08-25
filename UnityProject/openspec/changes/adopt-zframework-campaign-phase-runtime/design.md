## Context

现有非决战年度闭环已可运行，但 `GameManager` 同时拥有 Unity 场景引用、阶段 FSM、ActionSession 和存档门禁。第一阶段只迁移边界清晰的 FSM 所有权，避免在同一改动中重写全部运行态事务。

## Decisions

### Module 空闲初始化，lease 建立运行世代

`PlayableCampaignRuntimeModule.OnInit` 不读取内容或场景。`GameManager.Awake` 获取独占 `ICampaignPhaseRuntime`；lease 内创建 `PhaseManager` 并转发阶段表现回调。

### 单一活动运行态

模块同一时刻只允许一个活动 lease。每次成功获取递增 `GenerationId`；旧 lease 释放后才能建立新世代，防止多个场景宿主争用固定名称的 ZFramework FSM。

### Reset 与 Dispose 分离

启动或读档失败使用 `Reset` 清空 FSM 状态但保留当前 lease，以便玩家重试。`GameManager.OnDestroy` 使用 `Dispose` 释放 FSM 和模块占用；`ModuleSystem.Shutdown` 也能幂等释放当前 lease。

### 保持 ActionQueue 边界

阶段运行态的创建、重置和释放是生命周期管理，不创建 `GameAction`。营地、狩猎与 Campaign ActionQueue 仍只处理游戏性事务；View 显隐仍由 `GameManager` 的阶段回调负责。

## Risks / Trade-offs

- 本阶段没有迁移 Settlement/Hunt Manager、ActionSession 或稳定存档载荷，`GameManager` 仍较大。
- 当前 lease 在场景宿主销毁时一并释放；无场景 Host 的跨场景常驻运行态留到后续迁移职责簇。
- `PhaseManager` 仍复用固定 FSM 名称；独占 lease 是当前防止同名争用的门禁。
