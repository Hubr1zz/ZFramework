---
schemaVersion: 2
category: feature
title: 跨阶段桌面事件交互
---

## ADDED Requirements

### Requirement: Resource availability is projected from the active phase

共享世界空间事件 View SHALL 使用当前事件 Action 提供的只读资源作用域来显示、启用和返回资源门槛选项。View SHALL NOT 自行选择 Settlement 或 Hunt 库存，也 SHALL NOT 修改资源。

#### Scenario: The same resource condition is presented in different phases

- **WHEN** Settlement 事件显示资源门槛
- **THEN** 卡牌说明并读取营地库存
- **WHEN** Hunt 事件显示相同稳定资源 ID 的门槛
- **THEN** 卡牌说明并读取小队携带物
- **AND** 两个阶段都由各自 ActionQueue 在提交前重验
