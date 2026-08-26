## Boundary

`PlayableSettlementPhaseCoordinator` 捕获当前 `IPlayableSettlementRuntime` 与 active `PlayableSettlementActionSession`，启动单一事件 runner。每次 `await ResolveEventsAsync` 返回后，在 `SettlementEventRestoreProjection.Complete` 或下一次 `Prepare` 前复核 runtime、session 和 runner token；不匹配时停止且不修改 projection。

Reset、Deactivate、Dispose 取消 runner。并行事件请求显式失败并将恢复 projection 置为不可用，不静默丢弃第二批事件。GameManager 在第二次可靠保存完成后调用 coordinator；projection 仍由 GameManager 创建、发布和清理。
