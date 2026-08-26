---
schemaVersion: 2
category: feature
title: "营地时间线事件稳定身份"
---

## ADDED Requirements

### Requirement: Timeline occurrence 精确链接事件结果记忆
系统 SHALL 在完成精确绑定的 Timeline occurrence 时，把该条目链接到同一次根事件 Resolution 的记忆，不得按 EventId 猜测或覆盖其他重复 occurrence。

#### Scenario: 同一事件多次出现
- **WHEN** Timeline 中存在两个相同 EventId 的不同 occurrence 且其中一个完成
- **THEN** 只有完成的条目链接其独立记忆，另一个条目保持未完成且无链接

#### Scenario: 子链容量失败
- **WHEN** 父事件已提交但其新增子 occurrence 超出恢复容量
- **THEN** 父 Timeline、父记忆、记忆链接和父提交事实保持一致，子链 diagnostic 阻止继续执行
