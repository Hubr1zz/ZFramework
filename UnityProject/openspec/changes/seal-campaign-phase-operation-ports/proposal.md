## Why

ZFramework 已唯一持有 Settlement、Hunt、Showdown 三阶段管理器，但 GameManager 仍可取得具体 manager/coordinator，并能注入任意运行态构造工厂。这使阶段世代、ActionSession 与旧回调防护仍依赖组合根自律，也为 GameManager 再次膨胀留下入口。

## What Changes

- CampaignRuntime 仅向 GameManager 暴露三个 internal 阶段操作端口，不再暴露具体 manager/coordinator。
- Settlement 与 Hunt 的 manager、ActionSession factory 和 current-generation provider 收回各自阶段管理器内部。
- GameManager 只向阶段端口提供场景依赖、共享交互端口与跨阶段 callback，继续持有顶层 FSM 和跨阶段事务。
- Showdown 仅收口生命周期访问，不修改任何战斗玩法。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `game-manager-orchestration`: 限定 GameManager 只能消费阶段操作端口。
- `settlement-phase-orchestration`: 由 SettlementPhaseManager 内部组装运行态 factory。
- `hunt-phase-orchestration`: 由 HuntPhaseManager 内部绑定 current generation 与运行态 factory。
- `showdown-phase-orchestration`: 通过窄生命周期端口保留兼容门面。

## Impact

影响 CampaignRuntime 组合根、三个 plain phase manager、GameManager 的内部字段与既有架构测试。不改变存档 schema、ActionQueue 玩法边界、跨阶段事务顺序、内容资产、MonoBehaviour 数量或 Showdown 规则。
