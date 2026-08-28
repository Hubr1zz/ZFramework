## Why

事件重投支付已经是独立权威检查点，但当前 pending occurrence 只保存事件与行动者身份，不保存所选分支、最终骰值和“已重投”状态。玩家在支付后退出并继续战役，会丢失重投结果；拥有多点意志时还能对同一 occurrence 再次支付并重复增加命运。

## What Changes

- 在共享事件 occurrence 上保存一份有界、可校验的重投继续快照，只记录稳定事件/选项/行动者身份和已提交的判定值。
- 营地与活动狩猎在重投检查点同步更新各自 occurrence；继续战役或狩猎恢复时从该结果继续，不重新选择、不重新初投、也不允许第二次重投。
- 无效、内容不匹配或未来版本的重投快照 fail closed，保留原检查点供诊断，不猜测或重复支付。
- 3D View 继续只提交“接受/重投”意图；物理骰表现仍由共享随机交互端口执行，权威状态仍由阶段 ActionQueue 提交。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `tabletop-random-interaction`: 已支付重投在恢复后继续使用同一权威结果，并保持每个 occurrence 最多一次支付。
- `event-occurrence-checkpoint-core`: 共享 occurrence 可携带有界的重投继续快照，并对身份、范围与版本执行一致校验。
- `campaign-persistence`: 营地 Continue 恢复已支付重投，不重复选择、初投或支付。
- `hunt-session-event-occurrence-recovery`: 活动狩猎快照恢复同一重投继续状态，不复制第二套事件规则。

## Impact

影响共享事件节点、事件选择事务、营地与狩猎 occurrence DTO/Store、活动狩猎快照适配器和对应定向测试。新增可选的版本化 checkpoint，并显式兼容没有重投快照的现有存档；不修改 GameManager、阶段管理器、Showdown、事件数值规则或 View 权威边界。
