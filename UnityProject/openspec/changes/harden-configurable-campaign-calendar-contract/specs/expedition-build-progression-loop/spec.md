---
schemaVersion: 2
category: feature
title: 远征奖励与营地构筑闭环
---

## MODIFIED Requirements

### Requirement: Hunt rewards cross the return boundary exactly once

狩猎事件或采集获得的资源 SHALL 先进入本次远征携带物；成功撤退后，Settlement 回营 Action SHALL 将其一次性提交到营地库存、记录远征并推进一个配置季节。只有越过末季时 SHALL 推进年份。制造或装备操作不得在有效回营检查点清除前开始。

#### Scenario: Black salt returns from an expedition

- **WHEN** 猎人在狩猎中获得一份 `black_salt` 并成功撤退
- **THEN** 资源 SHALL 从远征携带物转入营地库存，回营记录 SHALL 被应用且配置日历 SHALL 恰好推进一个季节
- **AND** 非末季回营 SHALL 保持当前年份不变
