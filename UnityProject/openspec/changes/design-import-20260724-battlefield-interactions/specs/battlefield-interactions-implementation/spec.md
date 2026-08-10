---
schemaVersion: 2
category: feature
title: 战场交互代码实现
---

# 战场交互代码实现

## ADDED Requirements

### Requirement: 实现“战场交互规则设计”
实现 SHALL 以高内聚模块提供全部玩家规则，并只通过显式依赖端口与其他战斗模块协作。

#### Scenario: 独立验证模块
- **WHEN** 测试提供本模块输入与依赖端口替身
- **THEN** 本模块可独立产生可验证结果

### Requirement: 战场交互由棋盘领域模块结算
实现 SHALL 在纯 C# Board/Combat 边界中返回移动、碰撞、伤害与地形修正结果，Unity Adapter 只换算坐标和播放表现。

#### Scenario: 计算击退路径
- **WHEN** 测试提供起点、方向、距离和占位
- **THEN** 模块返回最终格、碰撞双方与伤害结果

#### Scenario: 查询地形能力
- **WHEN** 其他模块通过端口查询实体所在格
- **THEN** 战场模块返回稳定的属性修正和临时行动，不暴露内部容器
