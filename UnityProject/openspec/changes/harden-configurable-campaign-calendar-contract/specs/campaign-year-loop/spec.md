---
schemaVersion: 2
category: system
title: 配置化战役季节出猎闭环
---

## ADDED Requirements

### Requirement: Calendar advancement has one gameplay authority

战役日历 SHALL 只由 Settlement Runner 首次接受稳定回营记录时推进。事件表和 ScriptableObject 内容 SHALL NOT 配置直接推进年份或季节的效果；保留的旧序列化效果值 SHALL 在内容预检与运行时 fail closed，且 SHALL NOT 报告成功。

#### Scenario: Legacy content requests AdvanceYear

- **WHEN** 事件表、营地事件资产或运行时旧内容包含保留的 `AdvanceYear` 效果
- **THEN** 内容或该效果 SHALL 被可诊断拒绝
- **AND** CurrentYear、CurrentSeasonIndex 与年度 Timeline SHALL 保持不变

### Requirement: Calendar facts preserve configured season identity

首次成功回营发布的 HuntCompleted 事实 SHALL 包含完成季与推进后季节的稳定 SeasonId 和显示名快照，并与同一冻结 CalendarId 的索引一致。旧事实缺少快照时表现 MAY 回退为季节序号，但 SHALL NOT 查询另一默认日历来猜测名称。

#### Scenario: A non-default calendar advances

- **WHEN** 使用受支持非默认 CalendarId 的战役成功回营
- **THEN** 回营事实 SHALL 携带该绑定日历中完成季与下一季的身份和显示名
- **AND** 3D 表现 SHALL NOT 使用当前默认日历的季节名称
