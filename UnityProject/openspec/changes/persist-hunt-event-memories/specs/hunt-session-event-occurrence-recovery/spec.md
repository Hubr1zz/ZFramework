---
schemaVersion: 2
category: feature
title: 狩猎会话事件链恢复
---

## ADDED Requirements

### Requirement: Resolution memory shares the occurrence commit boundary

Hunt Runner SHALL 只在事件 Resolution checkpoint 为当前 occurrence 写入结果记忆，并与消费父 occurrence、追加直接子 occurrence 共享同一玩法提交边界。prevent、取消和仅重掷 SHALL NOT 生成结果记忆；同一 MemoryId 的等价重放 SHALL 幂等，不一致事实 SHALL fail closed。

#### Scenario: Result presentation fails after resolution commit

- **WHEN** 事件效果、父 occurrence 和结果记忆已经提交，但结果表现确认失败
- **THEN** 已提交记忆 SHALL 保留且不得重复
- **AND** 下一次恢复 SHALL 从第一个待办子 occurrence 继续

#### Scenario: A reactor prevents execution before resolution

- **WHEN** Before Reactor 在事件 Resolution 之前阻止当前节点
- **THEN** occurrence SHALL 按既有规则保持或跳过
- **AND** 结果记忆集合 SHALL NOT 增加
