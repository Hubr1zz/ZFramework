---
schemaVersion: 2
category: feature
title: 石脸回声选择弧
---

## ADDED Requirements

### Requirement: The stone-face echo returns as one stable scheduled choice

`main_giant_face` SHALL 使用稳定事件 ID 安排两年后的 `main_face_echo`。该日程 SHALL 只在进入到期年份的年度事件批次时出现；季节推进、重复查询、取消或读档恢复本身 SHALL NOT 创建第二份日程或重复提交已完成 occurrence。

#### Scenario: The second year has elapsed

- **GIVEN** `main_giant_face` 已在第 1 年安排两年后的回声
- **WHEN** 战役权威日历进入第 3 年
- **THEN** `main_face_echo` SHALL 作为一个 Scheduled Choice 出现
- **AND** 同一 Timeline session 重复获取 SHALL NOT 再返回该 pending 条目

### Requirement: The echo offers one risky and one safe tabletop route

`main_face_echo` SHALL 在既有世界空间事件卡中提供两条公开路线。风险路线 SHALL 选择一名猎人并使用 `Understanding 7` 的一枚 d10 物理骰判定；成功 SHALL 增加 1 点知识和 1 份 `broken_stone`，失败 SHALL 产生 1 点手臂普通伤势且不发放成功奖励。稳妥路线 SHALL 不要求猎人或判定，并增加 2 份 `broken_stone`。

#### Scenario: The player reads the fragment successfully

- **WHEN** 风险路线的物理骰判定成功
- **THEN** 所选猎人的知识 SHALL 增加 1
- **AND** 营地 SHALL 获得 1 份 `broken_stone`
- **AND** 猎人的手臂 SHALL 不受伤

#### Scenario: The player fails to read the fragment

- **WHEN** 风险路线的物理骰判定失败
- **THEN** 所选猎人的手臂普通生命 SHALL 减少 1
- **AND** 知识与 `broken_stone` SHALL 不增加

#### Scenario: No hunter is available

- **GIVEN** 当前营地没有可选猎人
- **WHEN** 玩家选择稳妥路线
- **THEN** 事件 SHALL 可完成
- **AND** 营地 SHALL 获得 2 份 `broken_stone`

### Requirement: Choice results commit through the existing gameplay action boundary

选择、判定、效果和 occurrence 完成 SHALL 由当前 Settlement ActionQueue Root 提交。3D View SHALL 只提供输入和表现；UI、动画与确认事件 SHALL NOT 进入 ActionQueue。Reactor 阻止、取消或尚未确认 SHALL 不写入资源、成长、伤势或完成事实；同一已准备事务重复提交 SHALL 不叠加结果。

#### Scenario: A prepared choice is committed twice

- **WHEN** 同一个 `PlayableEventChoiceTransaction` 被重复提交
- **THEN** 选择效果 SHALL 只应用一次
- **AND** 事件 SHALL 继续保留 `triggered_face_memory` 作为后续叙事节点
