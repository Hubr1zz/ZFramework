---
schemaVersion: 2
category: feature
title: 狩猎归来结果检查点
---

## MODIFIED Requirements

### Requirement: Hunt return snapshot uses stable identities

正常撤退记录 SHALL 在 Hunt Runner 内冻结唯一 RecordId、回营协议版本、年份、参战猎人身份、伤亡数、v2+ 通用携带物栈，以及非负的匿名救援人口。当前生产记录 SHALL 使用协议 v3；记录 SHALL NOT 保存运行时资产或 Hunter 候选引用。

#### Scenario: Retreat preparation includes rescued population

- **WHEN** 当前远征携带一名幸存者并接受撤退准备
- **THEN** v3 快照、深拷贝和准备事实 SHALL 都保存救援人口 1
- **AND** Settlement Population SHALL 尚未变化

### Requirement: Settlement validates the complete return plan before mutation

Settlement Runner SHALL 在同一 root 修改状态前共同验证既有回营字段、当前 Population、救援人口非负及加法不溢出。协议 v0/v1/v2 SHALL 要求救援人口为 0；未知、混写、负值、溢出或未来版本输入 SHALL 保持 pending，且 SHALL NOT 产生部分人口、库存、成长、历史或日历变化。

#### Scenario: Population would overflow

- **WHEN** 有效 v3 记录的救援人口无法加入当前 Population
- **THEN** 整份归来计划 SHALL 被拒绝
- **AND** Population、携带物、库存、猎人和日历 SHALL 保持原值

### Requirement: Current return outcomes commit exactly once

未应用的有效 v3 记录 SHALL 在一个 Settlement root 内增加计划中的救援人口，并与通用携带物、成长、历史和配置日历提交共同生效。`HuntHistory.RecordId` SHALL 继续作为整体结果唯一幂等键；提交事实 SHALL 只从 root outbox 发布。

#### Scenario: A rescued survivor returns to camp

- **WHEN** 未应用的 v3 记录包含救援人口 1
- **THEN** Population SHALL 恰好增加 1，其他回营结果 SHALL 在同一 root 提交
- **AND** 相同 RecordId 重试 SHALL NOT 再次增加人口

### Requirement: Legacy return records preserve prior side effects

协议 v0 SHALL 继续表示旧流程已处理货物与成长；v1 SHALL 只接受旧资源清单；v2 SHALL 只接受通用物品栈；三者的救援人口 SHALL 必须为 0。协议 v3 SHALL 只接受通用物品栈并可携带救援人口。高于当前版本的记录 SHALL fail closed。

#### Scenario: A v2 return is recovered after upgrade

- **WHEN** 合法 v2 记录使用通用物品栈且救援人口为 0
- **THEN** Settlement SHALL 按原 v2 语义提交
- **AND** 非零救援人口混入 v2 时 SHALL 拒绝整份计划
