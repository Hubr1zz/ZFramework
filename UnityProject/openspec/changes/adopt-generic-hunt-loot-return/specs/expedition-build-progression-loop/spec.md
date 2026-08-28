---
schemaVersion: 2
category: feature
title: 远征奖励与营地构筑闭环
---

## MODIFIED Requirements

### Requirement: Hunt rewards cross the return boundary exactly once

狩猎事件或采集获得的资源与非资源物品 SHALL 先进入本次远征携带物；成功撤退后，Settlement 回营 Action SHALL 按物品类别将资源一次性提交到资源库存、将其他物品提交到通用仓库，并记录远征和推进一个配置季节。只有越过冻结日历的最后季节时才推进一年；制造、使用或装备操作不得在有效回营检查点清除前开始。

#### Scenario: A field dressing returns from an expedition

- **WHEN** 猎人在狩猎事件中获得一份 `weathered_field_dressing` 并成功撤退
- **THEN** 物品 SHALL 从远征携带物转入营地通用仓库，回营记录 SHALL 恰好应用一次
- **AND** 玩家 SHALL 能通过既有营地物品流程消费该物品，而无需资源专用迁移路径
