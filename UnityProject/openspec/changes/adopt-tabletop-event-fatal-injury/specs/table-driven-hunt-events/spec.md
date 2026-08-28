---
schemaVersion: 2
category: feature
title: 读表狩猎事件内容
---

## ADDED Requirements

### Requirement: Fatal injury content is phase-scoped and atomic

表驱动 `FatalInjury` 效果 SHALL 只允许出现在 Hunt Choice 的成功或失败效果中，要求有效选中猎人、稳定死亡牌堆 ID、有效部位和正伤害值，并 SHALL 独占该效果列表。Immediate、Settlement、Showdown、无 actor 或混合奖励记录 SHALL 在进入内容池前整体拒绝。

#### Scenario: A healthy hunter accepts the crushing slab risk

- **WHEN** `hunt_crushing_slab` 对默认健康猎人的手臂提交配置伤害
- **THEN** 规则计划 SHALL 进入死亡牌堆选择，而不是只产生普通伤势

#### Scenario: Fatal injury is mixed with a reward

- **WHEN** 同一结果列表同时配置 `FatalInjury` 和资源奖励
- **THEN** 整条事件记录 SHALL fail closed，且不得进入可玩事件池

### Requirement: Fatal injury survival follow-up is a closed content reference

表驱动 `FatalInjury` SHALL 配置稳定 `survivalEventId`，且目标 SHALL 是同一内容世代中的非自身 `Triggered` 事件。缺失、重复、非 `Triggered` 或直接自引用目标 SHALL 使来源事件在装配时 fail closed。该目标 SHALL 可由 Hunt 内容包按稳定 ID 恢复，但 SHALL NOT 进入随机 Hunt 根事件池。

#### Scenario: Survival follow-up is assembled

- **WHEN** `hunt_crushing_slab` 引用 `hunt_fatal_injury_survivor`
- **THEN** 内容包 SHALL 能按稳定 ID 解析“幸运儿”
- **AND** “幸运儿” SHALL NOT 成为随机抽取根事件

#### Scenario: Survival follow-up is invalid

- **WHEN** `survivalEventId` 缺失、指向自身或指向非 `Triggered` 内容
- **THEN** 来源事件 SHALL 在进入可玩内容包前整体拒绝
