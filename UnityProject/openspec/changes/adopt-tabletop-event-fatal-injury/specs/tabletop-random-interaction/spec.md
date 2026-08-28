---
schemaVersion: 2
category: feature
title: 桌游式随机交互
---

## ADDED Requirements

### Requirement: Death deck selection uses physical cards and stable positions

死亡牌请求 SHALL 说明当前存活/死亡牌构成，为每个洗牌后位置创建不可从卡背区分的 Cards3D 实体，并只返回一个稳定位置 ID。玩家选择后 SHALL 翻开该位置的真实“存活”或“死亡”牌面；View SHALL NOT 决定或提交结果。

#### Scenario: The player selects a death-deck card

- **WHEN** 玩家短按一张背面死亡判定牌
- **THEN** 该卡 SHALL 翻开并显示规则计划提供的真实牌面
- **AND** 返回结果 SHALL 只包含匹配请求 ID 与牌堆范围的稳定位置

#### Scenario: No tabletop presenter is installed

- **WHEN** 无头 Hunt 事件进入死亡牌判定
- **THEN** Runner SHALL 使用已洗牌顺序的位置 0 完成同一事务，且不消费提交期随机源来选择位置
