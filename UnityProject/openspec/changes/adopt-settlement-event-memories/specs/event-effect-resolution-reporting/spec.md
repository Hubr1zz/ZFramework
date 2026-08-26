---
schemaVersion: 2
category: feature
title: "事件效果结构化结算结果"
---

## ADDED Requirements

### Requirement: 营地事件持久化结构化效果事实
营地事件记忆 SHALL 按原顺序保存每个已尝试效果的稳定类型、解析后目标、成功状态、状态变化前后值和失败原因，不得仅保存本地化结果文案。

#### Scenario: 部分效果失败
- **WHEN** 一个已提交事件包含成功与失败的效果尝试
- **THEN** 事件记忆按原顺序保存全部尝试，年鉴分别显示实际变化和未生效原因
