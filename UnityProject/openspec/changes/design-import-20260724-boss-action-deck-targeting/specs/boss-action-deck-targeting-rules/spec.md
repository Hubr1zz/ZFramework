---
schemaVersion: 2
category: game-rule
title: Boss 行动牌堆与目标选择规则
---

# Boss 行动牌堆与目标选择规则

## ADDED Requirements

### Requirement: Boss 从行动牌堆顶执行能力
Boss 主动能力 SHALL 由攻击型或非攻击型行动卡表达；怪物回合开始时翻开行动牌堆顶卡并执行。

#### Scenario: 执行攻击型行动
- **WHEN** 翻开的行动卡包含攻击
- **THEN** 系统按伤害、精准、次数、时点和目标策略创建攻击请求

#### Scenario: 执行非攻击型行动
- **WHEN** 翻开的行动卡不含攻击
- **THEN** 系统执行移动、Buff、牌堆改写或场地效果而不进入攻击结算

### Requirement: Boss 行动声明目标策略
行动卡 SHALL 支持无目标、格子、方向范围或指定猎人，并可使用最近、伤势最重、固定编号随机或完全随机等策略。

#### Scenario: 选择最近猎人
- **WHEN** 行动卡使用最近目标策略
- **THEN** 系统依据棋盘距离选择合法的最近存活猎人

#### Scenario: 没有合法目标
- **WHEN** 目标策略找不到任何合法目标
- **THEN** 系统按卡牌的无目标处理契约结束或跳过该效果
