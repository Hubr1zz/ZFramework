## Why

狩猎事件目前只在活动会话中产生即时效果，回营后无法说明玩家经历了哪些选择、判定和致命伤结果；父事件提交后读档恢复子事件时，也缺少与已提交事实绑定的长期记录。需要把已提交事件结果沿活动狩猎存档和回营检查点带入 3D 营地年鉴，同时保持幂等、存档兼容和有界恢复。

## What Changes

- 每个已提交的 Hunt 事件 occurrence 生成带远征与序号身份的结构化结果记忆；阻止、取消和仅重掷不生成记忆。
- 活动狩猎 schema v4 保存并严格恢复事件记忆；旧 v2/v3 不接受伪造的记忆载荷，未来或越界数据 fail closed。
- 回营协议 v4 将记忆深拷贝到 `HuntRecord`，并以稳定 ExpeditionId 作为 RecordId；营地提交前验证身份、数量和效果边界。
- 3D 营地年鉴在每次远征摘要下按 occurrence 顺序显示事件子条目，并展示玩家可读的致命伤死亡牌、部位、剩余生命、永久损伤或死亡结果。
- 收敛 Settlement/Hunt 共用的事件记忆数据模型与验证规则；ActionQueue 仍只处理玩法提交，年鉴仅作只读 View 投影。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `active-hunt-persistence`: 活动狩猎检查点新增已提交事件结果记忆及 v4 恢复门禁。
- `hunt-return-outcome-checkpoint`: 回营快照新增远征事件记忆并在 Settlement 提交前验证。
- `hunt-session-event-occurrence-recovery`: 父已提交、子待恢复时同时保存不重放的父结果记忆。
- `tabletop-settlement-advancement-ledger`: 3D 年鉴把远征事件作为狩猎记录的有序子条目展示。

## Impact

影响 Hunt 事件提交、活动狩猎序列化、回营协议、Settlement 历史和 3D 年鉴投影。存档协议从 active v3/return v3 升至 v4；v2/v3 继续支持其既有字段，但不得携带 v4 事件记忆。不会改变 Combat、Showdown、GameManager 阶段生命周期或 UI 事件调度。
