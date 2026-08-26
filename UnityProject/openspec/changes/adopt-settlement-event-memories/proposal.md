## Why

营地事件已经能结算选择、判定与效果，但存档和 3D 年鉴只保留“已发生”，玩家读档后无法回顾自己的决定及其结果，也无法把事件形成长期战役记忆。

## What Changes

- 为每个已权威提交的营地事件节点保存结构化、幂等且可读档的结果记忆。
- 为 Timeline 根 occurrence 绑定精确记忆，同时保留子事件和触发事件的独立记忆。
- 为事件选项建立稳定 `optionId`，并在正式营地内容装配时拒绝缺失或重复身份。
- 在 3D 营地年鉴中以玩家可读文本展示选择来源、判定、结果与实际效果；旧档不推测缺失历史。

## Capabilities

### New Capabilities

- `settlement-event-resolution-memory`: 定义营地事件提交后形成长期结构化记忆的玩家运行时契约。

### Modified Capabilities

- `settlement-timeline-event-identity`: 精确 Timeline occurrence 在完成时绑定对应的根事件记忆。
- `tabletop-settlement-advancement-ledger`: 3D 年鉴展示事件选择、判定、结果和子事件余波。
- `event-effect-resolution-reporting`: 已提交营地事件把结构化效果结果持久化到事件记忆。

## Impact

影响事件内容表与 ScriptableObject 校验、ActionQueue Resolution checkpoint、Settlement 存档数据、事件链提交边界和 3D 年鉴只读投影。旧存档兼容；不修改 Calendar、GameManager、Hunt 或 Showdown 流程。
