---
schemaVersion: 2
category: feature
title: 狩猎归来结果检查点
---

## MODIFIED Requirements

### Requirement: Hunt return snapshot uses stable identities

正常撤退记录 SHALL 在 Hunt Runner 内冻结唯一 RecordId、回营协议版本、年份、参战猎人 InstanceId、伤亡数，以及按稳定物品 ContentId 聚合并排序的正数量携带物栈；记录 SHALL NOT 保存运行时资产引用。当前生产记录 SHALL 使用协议 v2 的通用物品栈并保持旧资源清单为空。

#### Scenario: Retreat preparation succeeds with mixed cargo

- **WHEN** 当前 Hunt Runner 接受包含资源和消耗品的撤退准备
- **THEN** 生成的 v2 快照 SHALL 为每个稳定物品 ID 保存聚合数量
- **AND** 克隆、玩法事实与跨层传递 SHALL 使用深拷贝，不得改写或丢失货物

### Requirement: Settlement validates the complete return plan before mutation

Settlement Runner SHALL 在同一 root 修改状态前验证协议版本、RecordId、年份、参战者唯一性与存在性、伤亡数、每个物品的稳定 ContentId、类别、聚合数量和整数溢出。未知、重复、类别不匹配、混写版本字段、溢出或未来版本输入 SHALL 可诊断失败并保持 pending，且 SHALL NOT 产生部分库存、成长、历史或日历变化。

#### Scenario: A return references invalid item content

- **WHEN** 当前版本记录包含未知物品、未知猎人、重复猎人、非法数量、超限数量或旧新字段混写
- **THEN** 整个归来计划 SHALL 被拒绝，营地权威状态和猎人携带物 SHALL 保持原值

### Requirement: Current return outcomes commit exactly once

未应用的当前版本记录 SHALL 在一个 Settlement root 内按目录类别转入货物：资源进入营地资源库存并发现材料，非资源物品进入既有通用仓库；随后 SHALL 清除参战者携带物、只推进存活参战者并处理退休、追加 HuntHistory、推进恰好一个配置季节，并按既有规则物化回营和年度 Timeline。死亡参战者 SHALL NOT 成长；提交事实 SHALL 仅从 root 的 EventBus outbox 发布。

#### Scenario: A mixed return commits

- **WHEN** 有效 v2 RecordId 尚未出现在 HuntHistory，且记录包含资源与旧式包扎布
- **THEN** 资源库存和非资源仓库 SHALL 分别增加记录数量
- **AND** 携带物、成长或退休、历史与日历 SHALL 各提交一次

#### Scenario: The same return is replayed

- **WHEN** 相同 RecordId 已存在于 HuntHistory
- **THEN** Settlement SHALL 幂等成功，且 SHALL NOT 重复任何库存、成长、退休、日历或 Timeline 结果

### Requirement: Legacy return records preserve prior side effects

协议版本 0 SHALL 视为旧流程已经处理资源和成长，只兼容提交历史与日历；协议版本 1 SHALL 只从旧资源 ContentId 清单迁移资源，不得接受 v2 物品栈；协议版本 2 SHALL 只接受通用物品栈，不得继续写入旧资源清单。高于当前版本的记录 SHALL fail closed，兼容流程 SHALL NOT 根据当前营地名册猜测旧参战者。

#### Scenario: A v1 resource return is recovered

- **WHEN** 版本 1 记录包含有效参战者和旧资源 ContentId 清单
- **THEN** Settlement SHALL 按资源类别完成一次迁移提交
- **AND** 任一 v2 物品栈混入该记录时 SHALL 拒绝整份计划而不是静默丢弃
