---
schemaVersion: 2
category: game-rule
title: 猎人伤势与死亡规则
---

# 猎人伤势与死亡规则

## ADDED Requirements

### Requirement: 猎人按部位承受伤害
猎人 SHALL 分别追踪头、躯干、手臂和腿的生命与护甲；部位有生命时正常扣减，生命归零后再次承伤进入致命伤。

#### Scenario: 护甲吸收伤害
- **WHEN** 命中指定一个仍有护甲的部位
- **THEN** 系统先按护甲规则减少伤害，再更新该部位生命

#### Scenario: 零生命部位再次受伤
- **WHEN** 生命已归零的部位再次承受有效伤害
- **THEN** 系统开始致命伤流程

### Requirement: 致命伤使用可见死亡牌堆
初始死亡牌堆 SHALL 包含 1 张存活牌；每次致命伤抽 1 张，生还后加入 1 张死亡牌。

#### Scenario: 抽到存活
- **WHEN** 致命伤抽到存活牌
- **THEN** 猎人存活、可获得永久损伤，并向牌堆加入 1 张死亡牌

#### Scenario: 抽到死亡
- **WHEN** 致命伤抽到死亡牌
- **THEN** 猎人永久死亡且装备不随之销毁
