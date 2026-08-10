---
schemaVersion: 2
category: feature
title: Boss 部位卡代码实现
---

# Boss 部位卡代码实现

## ADDED Requirements

### Requirement: 实现“Boss 部位卡规则设计”
实现 SHALL 以高内聚模块提供全部玩家规则，并只通过显式依赖端口与其他战斗模块协作。

#### Scenario: 独立验证模块
- **WHEN** 测试提供本模块输入与依赖端口替身
- **THEN** 本模块可独立产生可验证结果

### Requirement: 部位模块独立持有状态与效果解析
实现 SHALL 由纯 C# HitLocationState/Definition 持有权威状态，Adapter 只映射 HitLocationCardData 并发布表现事件。

#### Scenario: 跨攻击保留部位状态
- **WHEN** 一次攻击结束
- **THEN** 未摧毁部位翻回背面，而生命与摧毁状态保留到后续攻击

#### Scenario: 确定性解析效果
- **WHEN** 测试提交攻击结果与部位条件
- **THEN** 不依赖 Unity 对象即可得到唯一的效果执行顺序
