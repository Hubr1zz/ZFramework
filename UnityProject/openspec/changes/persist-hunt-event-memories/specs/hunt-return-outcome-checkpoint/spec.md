---
schemaVersion: 2
category: feature
title: 狩猎归来结果检查点
---

## ADDED Requirements

### Requirement: Return v4 carries bounded expedition event memories

当前 Hunt Runner SHALL 以活动 ExpeditionId 作为稳定 RecordId，并在回营协议 v4 中深拷贝本次远征已提交的事件结果记忆。Settlement Runner SHALL 在任何归来状态变更前验证版本、远征归属、occurrence 序号唯一性、记忆与效果上限及字段边界；v0-v3 记录 SHALL NOT 携带事件记忆。

#### Scenario: A valid v4 return commits event history

- **WHEN** 有效 v4 回营记录携带本次 ExpeditionId 下的有序事件记忆
- **THEN** Settlement SHALL 将同一深拷贝记录追加到 HuntHistory
- **AND** 相同 RecordId 重试 SHALL NOT 重复追加记忆或其他回营结果

#### Scenario: A legacy return carries forged memories

- **WHEN** v0-v3 回营记录包含任何事件记忆
- **THEN** 整个归来计划 SHALL fail closed 并保持 pending
- **AND** SHALL NOT 产生部分资源、成长、历史或日历变化
