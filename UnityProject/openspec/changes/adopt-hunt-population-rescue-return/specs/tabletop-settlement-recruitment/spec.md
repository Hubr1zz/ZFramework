---
schemaVersion: 2
category: feature
title: 营地桌面猎人招募
---

## MODIFIED Requirements

### Requirement: 人口是常规招募的配置化供给

招募内容 SHALL 配置人口成本。Population MAY 来自设施值守或已成功提交的 v3 狩猎救援记录；来源不得改变既有招募校验、成本、Reactor、命名和持久化事务。狩猎中的同行幸存者 SHALL NOT 在回营前成为可招募候选。

#### Scenario: 狩猎救援人口支持常规补员

- **WHEN** 一名匿名幸存者随远征成功回营，且营地满足既有资源、年度和名册约束
- **THEN** 3D 招募板 SHALL 可消费该人口并让玩家选择模板和命名
- **AND** Hunt 阶段 SHALL NOT 自动创建 Hunter 或打开招募板
