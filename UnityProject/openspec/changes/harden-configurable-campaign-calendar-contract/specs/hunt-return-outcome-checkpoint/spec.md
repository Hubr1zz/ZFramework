---
schemaVersion: 2
category: feature
title: 狩猎归来结果检查点
---

## MODIFIED Requirements

### Requirement: Current return outcomes commit exactly once

未应用的当前版本记录 SHALL 在一个 Settlement root 内转入记录中的资源、清除参战者携带物、只推进存活参战者并处理退休、追加 HuntHistory、推进恰好一个配置季节，并仅在越过末季时物化下一年度 Timeline。死亡参战者 SHALL NOT 成长；提交事实 SHALL 仅从 root 的 EventBus outbox 发布。

#### Scenario: A current return commits

- **WHEN** 有效 RecordId 尚未出现在 HuntHistory
- **THEN** 资源、成长或退休、历史和季节 SHALL 各提交一次
- **AND** 只有末季回营 SHALL 推进年份并物化年度事件

#### Scenario: The same return is replayed

- **WHEN** 相同 RecordId 已存在于 HuntHistory
- **THEN** Settlement SHALL 幂等成功，且 SHALL NOT 重复资源、成长、退休、季节、年份或年度事件

### Requirement: Legacy return records preserve prior side effects

协议版本 0 SHALL 视为旧流程已经处理资源和成长，只兼容提交历史与一个配置季节；高于当前版本的记录 SHALL fail closed。兼容流程 SHALL NOT 根据当前营地名册猜测旧参战者，也 SHALL NOT 在非末季生成年度 Timeline。

#### Scenario: A legacy return is recovered

- **WHEN** 版本 0 记录具有有效 RecordId 和当前年份
- **THEN** Settlement SHALL 只幂等提交历史与一个配置季节，不再次转入资源或推进猎人
