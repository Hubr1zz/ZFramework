---
schemaVersion: 2
category: feature
title: 读表狩猎事件内容
---

## ADDED Requirements

### Requirement: Table-driven resource guards are phase authoritative

读表 Hunt 事件的 `MinimumResource` 条件 SHALL 通过当前 Hunt runner 注入的资源可用量端口判定，并在玩家选择返回后由同一 ActionQueue 节点再次校验。条件变化使显式选择失效时 SHALL 失败关闭并保留当前 occurrence，不得静默改选其他分支。

#### Scenario: A guarded child resumes from an active-hunt checkpoint

- **WHEN** 已提交父事件和待处理子 occurrence 从活动狩猎存档恢复
- **THEN** 子事件从恢复后的远征携带物重建资源可用性
- **AND** 已完成的父事件奖励不得重复提交
