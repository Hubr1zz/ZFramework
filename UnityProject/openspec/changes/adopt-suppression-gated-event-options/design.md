## Context

猎人压抑值已有统一的 0..8 分类规则，但事件选项条件尚未提供稳定的分类门控。此次 Change 记录已完成的直接实现，范围仅限 GameCore 规则、内容表适配与定向测试；正式内容案例仍需人类确认。

## Goals / Non-Goals

**Goals:**

- 复用 `HunterSuppressionRules` 的唯一阈值与分类结果。
- 支持 `mad`、`normal`、`passive` 及合法条件反向匹配。
- 让表适配对非法键、缺失猎人和不适用值 fail-closed。

**Non-Goals:**

- 不改变压抑数值、猎人状态、事件 ActionQueue 或表现层。
- 不添加生产事件选项；现有候选可能改变风险平衡，等待内容规则批准。

## Decisions

1. **复用既有分类规则。** 条件评估调用 `HunterSuppressionRules.Classify`，不复制边界常量；候选的数值阈值仍由该规则唯一拥有。
2. **只规范化新条件的键。** `PlayableEventTable` 保留旧条件的原始键语义，仅对 `SuppressionState` 接受并写入 canonical `mad|normal|passive`。
3. **先验证输入，再应用反向。** 缺失猎人或非法状态键始终不可用，不能被 `Inverted` 翻转为可用；合法输入才进行反向匹配。
4. **内容案例延后。** 不修改 `hunt-events.json`，待人类明确剧情风险与数值后再单独添加案例。

## Risks / Trade-offs

- [内容覆盖暂缺] 当前没有生产事件案例验证玩家流程 → 通过明确记录为后续内容决策，避免未经授权改变风险平衡。
- [兼容键语义] 旧条件不统一 Trim，可能保留历史空白键 → 仅新条件采用 canonical 化，避免静默改变既有表行为。

## Migration Plan

无需存档或内容迁移。部署代码后，已有事件表继续按旧语义解析；新增 suppression 条件必须使用 canonical 键并通过表校验。回滚只需移除新条件转换分支，不影响既有事件数据。

## Open Questions

- 哪些正式 Hunt 事件应使用三种 suppression 分类，以及对应文案和风险/收益，等待人类内容规则确认。
