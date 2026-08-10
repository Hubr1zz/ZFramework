---
schemaVersion: 2
category: game-rule
title: Boss 部位卡规则
---

# Boss 部位卡规则

## ADDED Requirements

### Requirement: 部位卡拥有独立运行时状态
每张 Boss 部位卡 SHALL 拥有独立生命、翻面和摧毁状态；生命归零后移出后续抽取。

#### Scenario: 展示部位
- **WHEN** 攻击要求展示一个可用部位
- **THEN** 系统只从未摧毁部位中抽取并翻至正面

#### Scenario: 摧毁部位
- **WHEN** 部位生命降至 0
- **THEN** 系统触发摧毁效果、保持摧毁展示且不再抽取该部位

### Requirement: 部位条件具有明确优先级
部位效果 SHALL 支持失败、成功、暴击、摧毁等条件，并在多个互斥条件同时满足时按配置优先级结算。

#### Scenario: 暴击覆盖普通成功
- **WHEN** 同一结果同时满足暴击和普通成功
- **THEN** 系统按优先级执行暴击结果而不重复执行被覆盖的普通成功
