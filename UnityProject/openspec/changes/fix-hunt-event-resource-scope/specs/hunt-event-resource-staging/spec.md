---
schemaVersion: 2
category: feature
title: 狩猎事件资源暂存与回营提交
---

## ADDED Requirements

### Requirement: Hunt resource conditions share the mutation scope

Hunt 事件的资源条件 MUST 只读取当前存活小队的远征携带物，并与同一节点的 `AddResource`、`RemoveResource` 使用相同阶段资源端口。Settlement 库存 MUST NOT 解锁 Hunt 资源选项，也 MUST NOT 补足远征携带物不足。

#### Scenario: A parent reward unlocks its child option

- **WHEN** 父事件把一份资源加入当前远征携带物
- **AND** Triggered 子事件要求小队携带该资源
- **THEN** 子事件的实体选项卡与 Hunt ActionQueue 二次校验都允许该选项
- **AND** 提交 `RemoveResource` 后只减少远征携带物

#### Scenario: Settlement owns the required resource but the squad does not

- **WHEN** 营地库存有该资源，而当前远征小队没有携带
- **THEN** Hunt 事件选项保持不可用并说明小队携带要求
- **AND** 直接提交不得从营地库存扣除或借用资源
