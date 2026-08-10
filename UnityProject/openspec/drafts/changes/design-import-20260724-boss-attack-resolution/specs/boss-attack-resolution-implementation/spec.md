---
schemaVersion: 2
category: feature
title: Boss 攻击结算代码实现
---

# Boss 攻击结算代码实现

## ADDED Requirements

### Requirement: 实现“Boss 攻击结算规则设计”
实现 SHALL 以高内聚模块提供全部玩家规则，并只通过显式依赖端口与其他战斗模块协作。

#### Scenario: 独立验证模块
- **WHEN** 测试提供本模块输入与依赖端口替身
- **THEN** 本模块可独立产生可验证结果

### Requirement: Boss 攻击使用确定性牌堆 resolver
实现 SHALL 由纯 C# BossAttackResolver 管理每次尝试的临时牌堆与结果，Adapter 负责抽牌表现和受击部位输入。

#### Scenario: 固定随机源
- **WHEN** 测试注入固定牌序
- **THEN** 多目标、多次攻击的命中与闪避结果可重复验证

#### Scenario: 完成伤势交接
- **WHEN** 抽到命中并选定受击部位
- **THEN** resolver 等待伤势模块返回伤害/死亡结果后完成该次攻击
