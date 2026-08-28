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

### Requirement: Ordinary survival creates one dedicated survival event

Hunt 致命伤抽到普通存活牌后 SHALL 恰好排入配置的专属存活事件；死亡牌、未触发死亡抽牌、Reactor prevent 或提交前取消 SHALL NOT 排入该事件。代表内容“幸运儿” SHALL 由既有事件效果为同一猎人增加 1 点命运值。

#### Scenario: The survivor receives the follow-up

- **WHEN** “塌落的石板”的执行猎人抽到存活牌
- **THEN** “幸运儿” SHALL 作为后续游戏性事件由 Hunt ActionQueue 执行
- **AND** 1 点命运值 SHALL 给予该执行猎人

#### Scenario: Death does not create a survival event

- **WHEN** 致命伤抽到死亡牌或没有进入死亡牌抽取
- **THEN** 专属存活事件 SHALL NOT 被排入
