---
schemaVersion: 2
category: feature
title: 猎人攻击结算代码实现
---

# 猎人攻击结算代码实现

## ADDED Requirements

### Requirement: 实现“猎人攻击结算规则设计”
实现 SHALL 以高内聚模块提供全部玩家规则，并只通过显式依赖端口与其他战斗模块协作。

#### Scenario: 独立验证模块
- **WHEN** 测试提供本模块输入与依赖端口替身
- **THEN** 本模块可独立产生可验证结果

### Requirement: 猎人攻击结算独立于表现
实现 SHALL 由纯 C# resolver 构建和抽取结果牌堆，Adapter 负责武器选择、结果分配输入与动画。

#### Scenario: 注入固定牌序
- **WHEN** 测试提供固定随机源或牌序
- **THEN** 成功/失败牌堆、抽取与分配结果可重复验证

#### Scenario: 等待完整攻击结束
- **WHEN** 行动卡触发猎人攻击
- **THEN** 行动卡与回合推进等待分配、部位效果和结果展示全部完成
