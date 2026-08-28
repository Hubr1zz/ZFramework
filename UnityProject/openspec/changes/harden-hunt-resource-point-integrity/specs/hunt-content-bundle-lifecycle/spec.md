---
schemaVersion: 2
category: system
title: "狩猎内容 Bundle 生命周期"
---

## MODIFIED Requirements

### Requirement: Stable references fail closed

每条路线 SHALL 拒绝缺少显式稳定 ContentId 的地块或 Hunt 事件、重复地块 ID、同一地块内重复的资源点稳定 ID、无效生成规则、计划外资源引用、路线事件池之外的翻牌事件以及不连续的噪音危险事件覆盖。事件表覆盖 SHALL 按 ContentId 合并，不得按 Unity 对象名称绑定。

资源点配置稳定 ID 的唯一性只约束同一地块的配置定义；运行态 SHALL 仍可按 `maxPerTile` 生成多个同类型实例。

#### Scenario: A tile references another content generation

- **WHEN** 地块资源无法在同批 Settlement RegistryBundle 解析为同一 ItemData 对象
- **THEN** 整个 Hunt Bundle 准备 SHALL 失败
- **AND** 不得发布部分路线

#### Scenario: One tile defines the same resource point ID twice

- **WHEN** 同一地块的两个资源点配置解析为相同稳定 ID
- **THEN** 整个 Hunt Bundle 准备 SHALL 失败并报告重复 ID
- **AND** 不得把运行态同类型重复与配置归属歧义混为一谈
