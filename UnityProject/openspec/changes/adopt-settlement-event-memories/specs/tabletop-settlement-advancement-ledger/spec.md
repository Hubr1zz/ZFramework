---
schemaVersion: 2
category: feature
title: "营地桌面年鉴事件记忆展示"
---

## ADDED Requirements

### Requirement: 3D 年鉴展示事件决定与结果
3D 营地年鉴 SHALL 以玩家可读文本展示已链接根事件的选择来源、判定结果、结果文本与实际效果，并 SHALL 把未链接 Timeline 的已提交子事件或触发事件显示为独立事件余波。

#### Scenario: 玩家选择事件
- **WHEN** 玩家打开包含已提交选择事件的 3D 年鉴
- **THEN** 对应 3D 条目显示玩家选择、判定成功或失败及已生效或未生效的效果

#### Scenario: 自动选择事件
- **WHEN** 事件由无输入流程自动选择并提交
- **THEN** 年鉴标记为自动结算，不把它叙述成玩家选择

#### Scenario: 旧档已完成条目
- **WHEN** 已完成 Timeline 条目没有结果记忆链接
- **THEN** 年鉴保持“已发生”的兼容展示且不伪造详情
