---
schemaVersion: 2
category: feature
title: "Boss 行动牌堆与目标选择代码实现"
---

# Boss 行动牌堆与目标选择代码实现

## ADDED Requirements

### Requirement: 实现“Boss 行动牌堆与目标选择规则设计”
实现 SHALL 以高内聚模块提供全部玩家规则，并只通过显式依赖端口与其他战斗模块协作。

#### Scenario: 独立验证模块
- **WHEN** 测试提供本模块输入与依赖端口替身
- **THEN** 本模块可独立产生可验证结果

### Requirement: 行动牌堆与目标策略可独立替换
实现 SHALL 由纯 C# BossActionDeck 管理牌序，由 TargetSelectionPolicy 通过棋盘查询端口选择目标。

#### Scenario: 固定牌序
- **WHEN** 测试构造一个已知行动牌堆
- **THEN** 抽顶、重排和牌堆改写产生确定结果

#### Scenario: 固定棋盘快照
- **WHEN** 测试提供角色位置与伤势快照
- **THEN** 各目标策略返回符合契约的目标且不读取 Unity 场景
