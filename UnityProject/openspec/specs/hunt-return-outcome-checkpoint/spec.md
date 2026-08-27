---
schemaVersion: 2
category: feature
title: "狩猎归来结果检查点"
---

# Hunt Return Outcome Checkpoint Specification

## Purpose

让正常狩猎归来的资源、参战猎人成长、远征历史、季节游标以及跨年时的年度事件在可恢复的 Settlement 提交边界内恰好生效一次。

## Requirements

### Requirement: Hunt return snapshot uses stable identities

正常撤退记录 SHALL 在 Hunt Runner 内冻结唯一 RecordId、回营协议版本、年份、参战猎人 InstanceId、伤亡数与资源 ContentId；记录 SHALL NOT 保存运行时资产引用。

#### Scenario: Retreat preparation succeeds

- **WHEN** 当前 Hunt Runner 接受撤退准备
- **THEN** 生成的快照 SHALL 包含完整稳定身份，且克隆与跨层传递 SHALL NOT 改写或丢失这些字段

### Requirement: Pending return is durable before Hunt exits

Campaign 编排 SHALL 在离开 Hunt 或修改营地结果前保存完整 PendingHuntReturn。保存失败 SHALL 保持 Hunt 权威上下文；阶段切换被拒绝时 SHALL 安全撤销检查点，无法撤销时 SHALL 保留门禁并只允许重试回营。

#### Scenario: The process stops before Settlement starts

- **WHEN** 回营检查点已保存但 Settlement root 尚未执行
- **THEN** 继续战役 SHALL 从记录恢复完整结算，不依赖未序列化的猎人 Collectibles

### Requirement: Settlement validates the complete return plan before mutation

Settlement Runner SHALL 在同一 root 修改状态前验证协议版本、RecordId、年份、参战者唯一性与存在性、伤亡数、资源 ContentId、聚合数量和整数溢出。未知、重复、溢出或未来版本输入 SHALL 可诊断失败并保持 pending，且 SHALL NOT 产生部分资源、成长、历史或年份变化。

#### Scenario: A return references invalid content

- **WHEN** 当前版本记录包含未知资源、未知猎人、重复猎人或超限资源
- **THEN** 整个归来计划 SHALL 被拒绝，营地权威状态 SHALL 保持原值

### Requirement: Current return outcomes commit exactly once

未应用的当前版本记录 SHALL 在一个 Settlement root 内转入记录中的资源、清除参战者携带物、只推进存活参战者并处理退休、追加 HuntHistory，并推进恰好一个配置季节。只有该季节提交越过冻结日历末尾时才 SHALL 推进一年并物化年度 Timeline。死亡参战者 SHALL NOT 成长；提交事实 SHALL 仅从 root 的 EventBus outbox 发布。

#### Scenario: A current return commits

- **WHEN** 有效 RecordId 尚未出现在 HuntHistory
- **THEN** 资源、成长或退休、历史和年份 SHALL 各提交一次，并在权威状态完成后发布对应事实

#### Scenario: The same return is replayed

- **WHEN** 相同 RecordId 已存在于 HuntHistory
- **THEN** Settlement SHALL 幂等成功，且 SHALL NOT 重复资源、成长、退休、年份或年度事件

### Requirement: Applied state remains recoverable until checkpoint clear is durable

Settlement 提交后 SHALL 先保存仍含 PendingHuntReturn 的已应用状态，再清除并再次保存检查点。任一次保存失败 SHALL 保持可重试门禁；HuntHistory.RecordId SHALL 是判断整体结果已应用的唯一幂等键。

#### Scenario: The process stops after result save but before checkpoint clear

- **WHEN** 存档同时包含该 RecordId 的 HuntHistory 与 PendingHuntReturn
- **THEN** 继续战役 SHALL 只清除并保存 pending，不重复任何归来结果

### Requirement: Legacy return records preserve prior side effects

协议版本 0 SHALL 视为旧流程已经处理资源和成长，只兼容提交历史与年份；高于当前版本的记录 SHALL fail closed。兼容流程 SHALL NOT 根据当前营地名册猜测旧参战者。

#### Scenario: A legacy return is recovered

- **WHEN** 版本 0 记录具有有效 RecordId 和当前年份
- **THEN** Settlement SHALL 只幂等提交历史与年份，不再次转入资源或推进猎人
