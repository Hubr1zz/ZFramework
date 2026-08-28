---
schemaVersion: 2
category: feature
title: 3D 营地消息记录
---

## ADDED Requirements

### Requirement: Hunter retirement is presented as a readable archive card

首次提交的 `HunterRetiredEvent` SHALL 在营地通知队列中生成一张世界空间退休归档卡，说明猎人名称、退休年龄、实际归还装备数量与年鉴年份。该卡 SHALL 位于随后发布的狩猎摘要之前，且 SHALL NOT 修改名册、仓库、ActionQueue 或存档。

#### Scenario: Retirement and hunt summary commit together

- **WHEN** 退休事实先于同一归来的狩猎完成事实发布
- **THEN** 玩家 SHALL 先读到退休归档，再读到狩猎摘要
- **AND** 两条通知 SHALL 都保留在既有有界队列中

#### Scenario: A hunter retires without equipment

- **WHEN** 退休事实的实际归还数量为零
- **THEN** 归档卡 SHALL 明确没有需要归还的装备，而不是省略或猜测库存变化
