---
schemaVersion: 2
category: feature
title: "营地事件结果记忆"
---

## ADDED Requirements

### Requirement: 已提交事件节点形成结构化长期记忆
系统 SHALL 在营地事件 Resolution 权威提交时，为根事件、子事件和无 Timeline 的触发事件分别保存稳定身份、事件与选项身份、选择来源、角色、判定、结果及结构化效果事实。

#### Scenario: 选择事件提交后读档
- **WHEN** 玩家或自动流程完成一个营地选择事件且 Resolution checkpoint 已提交
- **THEN** 存档中存在且仅存在一条对应事件 occurrence 的结构化记忆

#### Scenario: 表现确认失败
- **WHEN** Resolution 已提交但结果确认表现随后失败或取消
- **THEN** 已提交记忆、效果和 Timeline 完成态 SHALL 保留且不得重放

### Requirement: 事件记忆提交幂等且拒绝冲突
系统 SHALL 把相同记忆身份与相同事实视为幂等重放，并 SHALL 拒绝相同身份承载不同事实；持久集合 SHALL 保存调用事实的独立快照。

#### Scenario: 重复提交相同 checkpoint
- **WHEN** 相同 Resolution checkpoint 被恢复流程再次提交
- **THEN** 记忆集合数量不增加且原事实不改变

#### Scenario: 相同身份事实冲突
- **WHEN** 已存在的记忆身份收到不同选择、判定、结果或效果事实
- **THEN** 提交失败并保留原记忆

### Requirement: 事件记忆兼容旧档
系统 SHALL 对没有事件记忆字段的旧档初始化空集合且不推测历史结果，并 SHALL 对高于当前记忆 schema 的存档 fail-closed。

#### Scenario: 加载旧档
- **WHEN** 旧档包含已完成 Timeline 条目但没有事件记忆字段
- **THEN** 条目保持已完成并继续显示基础状态，系统不生成伪造记忆
