---
schemaVersion: 2
category: feature
title: 猎人伤势与死亡代码实现
---

# 猎人伤势与死亡代码实现

## ADDED Requirements

### Requirement: 实现“猎人伤势与死亡规则设计”
实现 SHALL 以高内聚模块提供全部玩家规则，并只通过显式依赖端口与其他战斗模块协作。

#### Scenario: 独立验证模块
- **WHEN** 测试提供本模块输入与依赖端口替身
- **THEN** 本模块可独立产生可验证结果

### Requirement: 伤势模块独立持有身体与死亡牌堆状态
实现 SHALL 由纯 C# HunterInjuryState/DeathDeck 处理部位生命、护甲、致命伤与死亡结果。

#### Scenario: 注入死亡牌序
- **WHEN** 测试提供固定死亡牌顺序
- **THEN** 连续生还、牌堆增长和死亡结果可重复验证

#### Scenario: 角色死亡交接
- **WHEN** 伤势模块产生永久死亡结果
- **THEN** Adapter 发布角色死亡事件并把装备保留交给上层角色/战役模块
