---
schemaVersion: 2
category: feature
title: 远征奖励与营地构筑闭环
---

## ADDED Requirements

### Requirement: Returned materials become permanent campaign knowledge

素材 SHALL 只在成功进入营地权威库存后记录为已发现知识；Hunt 携带物、失败远征和未提交奖励 SHALL NOT 提前产生该知识。重复回营或重复奖励 SHALL 幂等。

#### Scenario: Black salt crosses the return boundary

- **WHEN** `black_salt` 先进入远征携带物并随后成功回营
- **THEN** 携带阶段不得记录发现，回营提交后 SHALL 记录一次稳定素材 ID
