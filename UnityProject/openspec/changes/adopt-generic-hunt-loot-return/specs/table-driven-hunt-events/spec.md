---
schemaVersion: 2
category: feature
title: 读表狩猎事件内容
---

## ADDED Requirements

### Requirement: Hunt item rewards use the current event actor

读表 Hunt 事件 MAY 使用 `AddItem` 向当前事件执行者的远征携带物加入已登记的非资源物品。该效果 SHALL 只由 Hunt ActionQueue 注入的短生命周期命令执行，并在提交时重新验证稳定物品 ID、正数量、非资源类别，以及执行者仍属于当前小队且存活。资源奖励 SHALL 继续使用 `AddResource`；非 Hunt 内容、缺失执行者或缺失命令的请求 MUST 失败关闭。

#### Scenario: A buried cache yields a field dressing

- **WHEN** 当前存活猎人通过“埋藏的旧物”成功分支
- **THEN** `weathered_field_dressing` SHALL 进入该猎人的远征携带物
- **AND** 变化事实 SHALL 由同一事件 Action 产生，View 不得直接写入物品

#### Scenario: Invalid item reward content is loaded

- **WHEN** `AddItem` 引用资源、未知 ID、非正数量或非 Hunt 事件
- **THEN** 内容候选 SHALL 在发布前被拒绝
- **AND** 运行时缺少有效执行者或命令时 SHALL 不修改任何携带物
