---
schemaVersion: 2
category: architecture
title: 活动狩猎检查点与恢复
---

## MODIFIED Requirements

### Requirement: Active Hunt snapshot freezes authoritative runtime state

活动狩猎检查点 SHALL 保存稳定远征与内容身份、年份、编队、选中猎人、小队坐标、地图、资源点、携带物、事件 occurrence、随机状态，以及非负的同行救援人口。当前快照 SHALL 使用 schema v3；schema v2 SHALL 明确迁移救援人口为 0，不得通过旧版本混写新字段。

#### Scenario: Rescue population survives a process restart

- **WHEN** Hunt 事件提交两名同行幸存者并完成活动狩猎保存
- **THEN** v3 快照 SHALL 保存数值 2
- **AND** 恢复后的 HuntManager SHALL 在发布运行图前得到相同数值

#### Scenario: A v2 active hunt is restored

- **WHEN** 合法 schema v2 快照没有救援字段
- **THEN** 恢复候选 SHALL 使用救援人口 0
- **AND** v2 快照伪造非零救援字段、负值或未来版本 SHALL fail closed
