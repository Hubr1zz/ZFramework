---
schemaVersion: 2
category: architecture
title: 活动狩猎检查点与恢复
---

## ADDED Requirements

### Requirement: Active Hunt v4 persists committed event memories

活动狩猎 schema v4 SHALL 在最近一次 Resolution checkpoint 保存已提交事件的结构化结果记忆，包括稳定远征上下文、occurrence 序号、事件与选项身份、判定结果和效果结果。父事件已经提交而子 occurrence 仍待办时，快照 SHALL 同时保留父记忆与待办子节点。

#### Scenario: Parent result is committed before a child resumes

- **WHEN** 父事件已提交并产生待恢复子 occurrence，随后保存活动狩猎
- **THEN** v4 快照 SHALL 保存一条父结果记忆和该子 occurrence
- **AND** 恢复 SHALL 只执行子节点，不重放父效果或父随机结果

### Requirement: Active Hunt event memories fail closed by version and identity

v4 恢复 SHALL 验证记忆数量、效果数量、字段长度、ExpeditionId、已提交 occurrence 序号、每序号唯一性和完整 MemoryId。v2/v3 SHALL NOT 接受事件记忆；未来版本、跨远征身份或伪造序号 SHALL 在修改运行态前失败。

#### Scenario: A save injects a memory from another expedition

- **WHEN** v4 快照包含 SourceContextId 不等于活动 ExpeditionId 的结果记忆
- **THEN** 恢复 SHALL fail closed 并给出诊断
- **AND** SHALL NOT 部分恢复地图、携带物或事件队列
