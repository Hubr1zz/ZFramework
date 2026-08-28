## Why

猎人压抑状态已经有稳定的 0..8 规则与三段分类，但事件选项仍无法按 `mad`、`normal`、`passive` 读取该状态。此次变更用于把已验证的直接实现纳入可审查的 OpenSpec 记录，避免把表内数值阈值或未经批准的剧情风险写入正式内容。

## What Changes

- 新增事件选项 `SuppressionState` 条件，使用稳定键 `mad`、`normal`、`passive`。
- 规则层按现有 `HunterSuppressionRules.Classify` 精确匹配，并保留既有反向条件语义。
- 内容表在装配时规范化合法键，拒绝非法键、缺失猎人和不适用的附加数值。
- 增加三段边界、反向、缺失输入、非法键及表转换校验测试。
- 不新增生产事件案例；现有候选会改变风险平衡，待人类确认内容规则后另行纳入。

## Capabilities

### New Capabilities

- `suppression-gated-event-options`: 事件选项可以按猎人现有压抑分类进行严格、可反向的可用性判断。

### Modified Capabilities

<!-- 正式 Spec 未修改。 -->

## Impact

- GameCore：`EventOptionConditionKind`、`EventOptionAvailabilityRules` 与既有压抑分类规则。
- Unity 内容表：`PlayableEventTable` 的条件转换与 fail-closed 校验。
- EditMode：GameCore 条件规则和 Adapter 表转换测试。
- 不触碰 GameManager、ActionQueue、View、Showdown 或正式既有 Spec。
