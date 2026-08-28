---
schemaVersion: 2
category: game-rule
title: 压抑状态门控事件选项
---

## ADDED Requirements

### Requirement: 事件选项按猎人压抑分类门控

事件选项条件 MUST 使用稳定键 `mad`、`normal` 或 `passive` 表示压抑分类，并 MUST 通过现有 `HunterSuppressionRules.Classify` 的唯一阈值进行精确匹配。

#### Scenario: 三段分类按边界匹配
- **WHEN** 选项分别要求 `mad`、`normal` 或 `passive`，且猎人压抑值处于对应分类边界
- **THEN** 仅与当前分类相同的选项可用

#### Scenario: 合法条件支持反向匹配
- **WHEN** 条件使用合法分类键并标记为 `Inverted`
- **THEN** 选项可用性 MUST 是该分类精确匹配结果的反向值

### Requirement: 非法或缺失输入必须 fail-closed

缺失猎人、缺失或非法 suppression 状态键，以及不适用的附加条件值 MUST 使该 suppression 条件不可用；该结果 MUST NOT 被 `Inverted` 翻转为可用。

#### Scenario: 缺失猎人
- **WHEN** suppression 条件评估时没有目标猎人
- **THEN** 条件结果为不可用，无论是否标记 `Inverted`

#### Scenario: 非法状态键
- **WHEN** 内容表包含空键、未知键或非 canonical suppression 键
- **THEN** 表校验失败或条件不可用，无论是否标记 `Inverted`

### Requirement: 内容表严格转换 suppression 条件

内容表适配 MUST 将合法 suppression 键规范化为 canonical 形式，并 MUST 拒绝非法键、缺失键或非零 `value`；既有非 suppression 条件的键语义 MUST 保持不变。

#### Scenario: 合法表记录
- **WHEN** 表记录使用大小写或空白包裹的合法 suppression 键且 `value` 为零
- **THEN** 转换结果保存 canonical 小写键并通过验证

#### Scenario: 非零附加值
- **WHEN** suppression 条件的 `value` 不为零
- **THEN** 内容表验证失败，且不会生成可用条件
