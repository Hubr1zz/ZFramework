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
