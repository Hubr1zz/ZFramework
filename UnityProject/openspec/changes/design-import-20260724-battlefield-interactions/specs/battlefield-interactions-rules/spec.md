---
schemaVersion: 2
category: game-rule
title: 战场交互规则
---

# 战场交互规则

## ADDED Requirements

### Requirement: 战场实体声明边界属性
棋盘实体 SHALL 声明位置、类型、是否可重叠与是否可穿越；可破坏物体还 SHALL 声明生命、受伤效果和破坏效果。

#### Scenario: 移动进入受阻格
- **WHEN** 单位尝试进入不允许重叠或穿越的占位
- **THEN** 系统拒绝越过该边界并返回受阻结果

### Requirement: 击退碰撞产生伤害
被击退单位 SHALL 在不可重叠物体处停止并受到撞击伤害；与另一单位碰撞时双方同时受伤。

#### Scenario: 撞击另一单位
- **WHEN** 被击退单位进入另一单位占据的格子
- **THEN** 移动停止且双方同时获得撞击伤害结果

### Requirement: 地形提供战斗修正
草丛 SHALL 给予其上的猎人 1 点闪避；石块 SHALL 给予临时行动“投石”。

#### Scenario: 进入草丛
- **WHEN** 猎人处于草丛格
- **THEN** 战斗属性查询包含 1 点闪避加成

#### Scenario: 进入石块
- **WHEN** 猎人处于石块格
- **THEN** 可用行动中包含“投石”
