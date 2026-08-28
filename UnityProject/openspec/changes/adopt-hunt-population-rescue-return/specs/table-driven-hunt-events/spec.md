---
schemaVersion: 2
category: feature
title: 读表狩猎事件内容
---

## ADDED Requirements

### Requirement: Hunt events may rescue anonymous population

读表 Hunt 事件 MAY 使用 `RescuePopulation` 为当前远征增加匿名同行幸存者。该效果 SHALL 只由 Hunt ActionQueue 注入的短生命周期命令执行，并在提交时重新验证正数量、整数容量及执行者仍属于当前小队且存活；它 SHALL NOT 直接修改 Settlement Population 或创建 Hunter。非 Hunt 内容、目标参数、缺失执行者或缺失命令 MUST 失败关闭。

#### Scenario: A lost survivor joins the return party

- **WHEN** 当前存活猎人选择“带幸存者同行”
- **THEN** 当前远征救援人口 SHALL 增加 1，Settlement Population SHALL 保持不变
- **AND** 同一事件 root SHALL 发布 Hunt-scoped 玩法事实，View 不得写入状态

#### Scenario: Invalid rescue content is loaded

- **WHEN** 救援数量非正、配置目标或部位、出现在非 Hunt 内容，或运行时执行者无效
- **THEN** 内容候选或命令 SHALL 失败且不改变远征或营地人口
