## Why

`GameManager` 直接创建和销毁阶段 FSM，使权威游戏阶段依附于场景 `MonoBehaviour` 生命周期，并允许后续玩法继续向组合根堆积所有权。ZFramework 已提供 `ModuleSystem`，阶段运行态应先迁入框架模块，再逐簇迁移其他战役职责。

## What Changes

- 新增 ZFramework `PlayableCampaignRuntimeModule`，独占持有当前阶段 FSM、跨阶段 ActionEnvironment registry、发明安装租约、Campaign ActionSession，以及由 Manager、Settlement ActionSession 和事件恢复投影组成的营地运行世代。
- `GameManager` 通过代际 lease 启动、切换和重置战役运行态，只保留领域对象、场景表现回调与兼容入口。
- 同一时刻拒绝第二个阶段运行态；释放后新 lease 使用递增代际 ID。
- 玩家阶段切换与遭遇请求只能经过 Campaign ActionQueue；启动/恢复内部 FSM 操作不进入玩法队列。
- 新建与读档营地先形成 detached generation，再通过引用身份 CAS 交换为当前权威；旧世代仅在事务完成后释放，失败时可原样换回。
- 离营只释放当前世代的 Settlement ActionSession，Manager/Data 与 GenerationId 保持；回营在同一世代重建 Session。狩猎会话、存档格式和 3D 表现行为保持不变。

## Capabilities

### Modified Capabilities

- `game-manager-orchestration`: 阶段 FSM 和跨阶段 ActionEnvironment scope 所有权从 MonoBehaviour 移到 ZFramework 战役模块，GameManager 变为当前运行世代的场景宿主。

## Impact

- `GameModule` 的框架模块访问入口。
- 战役阶段 FSM、Campaign Runner 与共享 installer registry 的创建、重置、切换与释放。
- `GameManager` 生命周期和非决战战役循环验证。
