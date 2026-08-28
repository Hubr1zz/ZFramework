## Why

狩猎事件已经能奖励非资源物品，但事件选项只能检查通用属性与资源，无法让玩家用当前猎人实际携带的物品解决事件。结果是物品与事件两套内容表彼此割裂，后续每增加一种道具交互都可能落入定制分支。

## What Changes

- 事件选项增加 actor-scoped 的携带物数量条件，UI 只依据当前事件执行猎人的权威携带状态显示可用性。
- 狩猎事件结果增加非资源物品扣除效果；同一结果分支先聚合并预检全部物品成本，再执行任何效果。
- 扣除只作用于当前事件执行猎人的 Collectibles，不读取队友、营地仓库或装备槽。
- 狩猎内容包在启动时校验物品引用必须是 Registry 中的 canonical 稳定 ContentId，且目标不得是 Resource。
- `hunt_worm_rain` 提供一个可验证案例：消耗一件 `weathered_field_dressing`，获得两份 `earthworm`。

## Capabilities

### New Capabilities

- `hunt-event-carried-item-costs`: 表驱动狩猎事件按执行猎人检查并消耗携带的非资源物品。

## Impact

- 扩展事件条件与效果表、事件选择事务和 Hunt 物品端口。
- 复用现有事件 root ActionQueue、事件 View、玩法事实和活动狩猎检查点。
- 不新增物品转移、营地仓库成本、装备消耗、UI ActionQueue 或 Showdown 规则。
