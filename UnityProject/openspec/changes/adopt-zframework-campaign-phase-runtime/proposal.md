## Why

`GameManager` 直接创建和销毁阶段 FSM，使权威游戏阶段依附于场景 `MonoBehaviour` 生命周期，并允许后续玩法继续向组合根堆积所有权。ZFramework 已提供 `ModuleSystem`，阶段运行态应先迁入框架模块，再逐簇迁移其他战役职责。

## What Changes

- 新增 ZFramework `PlayableCampaignRuntimeModule`，独占持有当前阶段 FSM。
- `GameManager` 通过代际 lease 启动、切换和重置阶段，只保留场景表现回调与兼容入口。
- 同一时刻拒绝第二个阶段运行态；释放后新 lease 使用递增代际 ID。
- ActionQueue、存档事务、营地/狩猎会话和 3D 表现行为保持不变。

## Capabilities

### Modified Capabilities

- `game-manager-orchestration`: 阶段 FSM 所有权从 MonoBehaviour 移到 ZFramework 战役模块，GameManager 变为当前运行世代的场景宿主。

## Impact

- `GameModule` 的框架模块访问入口。
- 战役阶段 FSM 的创建、重置、切换与释放。
- `GameManager` 生命周期和非决战战役循环验证。
