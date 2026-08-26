---
schemaVersion: 2
category: feature
title: "营地时间线事件稳定身份"
---

# Settlement Timeline Event Identity Specification

## Purpose

让营地年度事件以稳定 ContentId 持久化，并把每一个年鉴 occurrence 精确交给 Settlement ActionQueue 完成，避免资产改名、重复事件或跨阶段复用导致错误恢复与误完成。

## Requirements

### Requirement: Timeline event identity is stable

MainStory、Random 与 Scheduled 年鉴条目 SHALL 把 EventData.ContentId 保存为 EventId；EventName 只作为显示快照。事件解析、随机事件近期排除与延迟调度 SHALL 使用稳定身份，而不得依赖 Unity 资产名。

#### Scenario: An event asset is renamed

- **WHEN** 已登记事件的 Unity 资产名发生变化，但 ContentId 保持不变
- **THEN** 年鉴条目 SHALL 继续解析到同一事件内容
- **AND** 新条目 SHALL NOT 保存资产名作为 EventId

### Requirement: Legacy identity migration is conservative

旧年鉴中的资产名 SHALL 仅在内容目录能唯一映射到一个事件时迁移为 ContentId。未知或歧义身份 SHALL 保留原值并记录诊断，且身份 schema SHALL NOT 前进；高于当前版本的 schema SHALL NOT 被降级或改写。

#### Scenario: A legacy alias is ambiguous

- **WHEN** 一个旧资产名与多个 canonical 或 alias 身份冲突
- **THEN** 内容目录 SHALL 拒绝成为可用目录
- **AND** 持久条目 SHALL 保留原身份等待内容修正

### Requirement: ActionQueue completes the exact timeline occurrence

每个年度事件工作项 SHALL 携带其精确 AnnalEntry。Settlement Runner SHALL 在事件效果提交检查点完成该条目；同 ContentId 的多个未完成条目 SHALL 保持独立，不得以 FindLast 或名称匹配完成其他 occurrence。

#### Scenario: The same event occurs in two years

- **WHEN** 两个未完成年鉴条目引用相同 ContentId，Runner 只执行第一个工作项
- **THEN** 只有绑定的第一个条目 SHALL 完成
- **AND** 第二个条目 SHALL 保持待处理

### Requirement: Calendar boundaries gate annual occurrence creation

年度 Timeline occurrence SHALL 只在配置化日历实际从最后季节进入下一年时创建。同年季节推进 SHALL 只记录 Hunt 与季节事实，不得生成下一年 MainStory、Random 或 Scheduled 条目。重复回营 RecordId 或回营检查点恢复 SHALL 复用既有精确 AnnalEntry，不得创建第二份年度 occurrence。

#### Scenario: A Hunt returns during the same year

- **WHEN** 默认两季日历从季节索引 0 推进到 1
- **THEN** Timeline SHALL NOT 创建下一年度条目
- **AND** 当前年度既有未完成 occurrence SHALL 保持原身份与状态

#### Scenario: A year-boundary return is retried

- **GIVEN** 跨年回营已经创建并保存下一年度的精确 AnnalEntry
- **WHEN** 相同 RecordId 因检查点恢复再次进入回营 Action
- **THEN** Timeline SHALL NOT 创建重复条目
- **AND** 恢复投影 SHALL 继续原 AnnalEntry 或其持久 child occurrence

### Requirement: Restore only projects due timeline occurrences

营地恢复投影 SHALL 只恢复年份不晚于当前战役年份的未完成 Timeline occurrence。未来年份的 Scheduled 条目 SHALL 保持未完成且不得提前解析或执行；当战役年份到达该条目的年份后，同一条目 SHALL 可被正常恢复。

#### Scenario: A future scheduled event exists in the save

- **GIVEN** 存档包含一个年份晚于 CurrentYear 的未完成 Scheduled 条目
- **WHEN** 营地恢复投影准备待办事件
- **THEN** 该条目 SHALL NOT 被解析或加入当前工作队列
- **AND** 该条目 SHALL 保持未完成，直到 CurrentYear 到达其排期年份

### Requirement: Committed parent is not replayed after presentation failure

事件效果与精确年鉴条目完成 SHALL 位于结果表现确认之前。确认失败后，已提交父条目 SHALL 保持完成，已登记的子 occurrence SHALL 留在事件链检查点等待恢复，父效果 SHALL NOT 重放。

#### Scenario: Result confirmation fails after the annual event commits

- **WHEN** 年度父事件已提交效果和子事件，随后结果确认失败
- **THEN** 父年鉴 occurrence SHALL 保持完成
- **AND** 恢复 SHALL 从待办子 occurrence 继续

### Requirement: Timeline completion is phase isolated

可恢复的年鉴 occurrence SHALL 由绑定精确 Settlement Timeline 工作项的 Settlement Runner 完成。共享事件节点的 standalone 或 Hunt 调用 SHALL NOT 隐式修改 Settlement Timeline；事件解析 SHALL 以拥有该 TimelineSystem 的内容池为边界，不依赖跨 session 静态解析结果。

#### Scenario: Hunt resolves content also known to Settlement

- **WHEN** Hunt Runner 执行一个与营地目录拥有相同 ContentId 的事件
- **THEN** Settlement Timeline 中的未完成条目 SHALL 保持不变
