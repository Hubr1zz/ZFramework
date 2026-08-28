---
schemaVersion: 2
category: feature
title: 事件驱动猎人永久死亡
---

## ADDED Requirements

### Requirement: Fatal injury resolves through the persistent death deck

Hunt 致命伤 SHALL 在表现前从猎人持久伤势与死亡牌构成准备只读计划。抽到存活牌 SHALL 保持猎人存活并向死亡牌堆加入一张死亡牌；抽到死亡牌 SHALL 只通过 `IHunterDeathCommand` 提交永久死亡、装备归还、年鉴、激励和名册事实。相同准备结果 SHALL NOT 成功提交两次。

#### Scenario: The hunter draws survival

- **WHEN** 玩家从只有一张存活牌的死亡牌堆选择该牌
- **THEN** 猎人 SHALL 存活，死亡牌堆 SHALL 恰好增加一张死亡牌

#### Scenario: The hunter draws death

- **WHEN** 玩家选择映射为死亡的稳定背面位置
- **THEN** 唯一死亡事务 SHALL 提交一次完整后果
- **AND** 最后一名猎人死亡 SHALL 截断子事件并交给既有战役失败流程
