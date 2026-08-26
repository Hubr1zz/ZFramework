## Context

`main_giant_face` 已通过 `ScheduleEvent(main_face_echo, 2)` 写入稳定 Scheduled 年鉴条目。默认两季日历下，该条目在跨入第 3 年时进入年度事件批次；季节数变化不会改变“两年后”的语义。缺口只在生产内容没有第二次抉择。

## Decisions

### 保留身份，只升级内容

继续使用 `main_face_echo` 和 `triggered_face_memory`。未完成旧日程会恢复为新版 Choice；已完成旧日程不补发，避免破坏 occurrence exactly-once。

### 两条路线都使用已有权威

风险路线使用 `Understanding 7 / PhysicalDice / 1d10`，成功提交 `AddUnderstanding(selected, 1)` 与 `AddResource(broken_stone, 1)`，失败提交 `AddRecoverableWound(selected, arms, 1)`。稳妥路线不要求猎人，提交 `AddResource(broken_stone, 2)`。View 只提供输入，效果和完成事实由 Settlement ActionQueue 提交。

### 不叠加下一次狩猎租约

当前战役只有一个 pending Hunt noise lease。年度随机事件可能已占用它；本内容用普通伤势形成跨阶段整备后果，避免隐式租约冲突卡住事件 occurrence。

## Risks / Trade-offs

- 内容升级会改变尚未完成的旧日程，这是稳定 ID 下的预期 live-content 行为。
- 当前只验证一个代表性主线案例，不扩建通用后果 DSL 或批量叙事内容。
- 3D Presenter 和物理骰子沿用共享生产路径，本 Change 不新增视觉、音效或演出。
