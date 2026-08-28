## Context

文件 Adapter 已用单调写版本、临时文件和备份保护磁盘一致性，但 Campaign 层没有把后台保存失败投影给玩家。关键事务已有 await/fail-gate，普通 autosave 则 fire-and-forget；二者必须共享同一协调器状态，又不能让普通请求改变关键调用自身的成功结果。

## Goals / Non-Goals

**Goals:**

- 让最新保存请求拥有可见状态，同时保留每个关键保存调用的独立真实结果。
- 重试时重新捕获最新 Settlement/Hunt 状态，避免回写旧失败快照。
- 在营地和狩猎的 3D 桌面上提供持续、可操作的失败反馈。

**Non-Goals:**

- 不回滚已经提交的玩法状态，不自动无限重试。
- 不修改存档格式、关键事务门禁或退出策略。
- 不把保存、轮询或按钮交互放入 ActionQueue/EventBus。
- 不推进 Showdown，也不重构 GameManager 或阶段管理器。

## Decisions

1. `saveRevision` 只决定哪个请求能更新玩家可见状态；一次保存调用是否成功只由该次存储结果与 campaign generation 决定，避免普通 autosave 让关键事务误报失败。
2. `generation` 隔离战役生命周期；Reset、Adopt 与成功的立即保存会让旧异步完成失效。
3. Failed 状态保存原因与可重试标志。取消重试时恢复先前 Failed，而普通生命周期取消回到 Idle。
4. Retry 使用单一 owner/completion，并重新调用快照捕获；Reset/Adopt 通过 epoch detach 旧 owner。
5. View 只轮询窄 `ICampaignReadModel.SaveStatus` 并调用 `ICampaignCommandPort.RetryPendingSaveAsync`。普通 Saving 不闪现卡片；只有已经出现 Failed 后才在重试期间保留卡片。
6. 独立 `CampaignSaveStatusPresenter3D` 跟随当前阶段根节点，避免继续扩大仅服务营地消息的 `SettlementNoticePresenter3D`。

## Risks / Trade-offs

- 多个普通 autosave 仍可同时抵达文件 Adapter → 现有单调写版本继续线性化磁盘结果；可见状态只跟随最新请求。
- 失败卡可能与其他桌面卡同时存在 → 默认偏移可序列化配置，且提示不阻塞玩法输入。
- `StablePayload` 仍表示最近可立即刷新的快照而非已确认落盘快照 → 保留既有重启/遭遇补偿语义，不在本次重命名或改写。
