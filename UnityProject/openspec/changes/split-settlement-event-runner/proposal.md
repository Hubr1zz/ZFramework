## Why

营地事件链的 ActionQueue 执行与恢复 projection 续跑仍位于 GameManager，导致阶段换代后异步 continuation 需要额外的世代检查，并让 GameManager 同时承担表现桥与事件 runner。

## What Changes

- 由 `PlayableSettlementPhaseCoordinator` 持有营地事件 runner。
- 将 `SettlementEventWork` 执行、projection Complete/Prepare 续跑和 runner 取消收敛到 coordinator。
- GameManager 继续拥有回营提交、两阶段保存、pending return、projection 创建/发布、FSM、存档和 startup 边界。

## Non-goals

- 不改变 Settlement ActionQueue、存档 schema、Timeline 规则或事件内容。
- 不迁移回营事务、两阶段保存、phase transition、ActiveHuntRestoreTransaction 或 Showdown。
- 不保留旧的 `EventData`/occurrence 执行重载；生产调用统一使用 `SettlementEventWork`。
