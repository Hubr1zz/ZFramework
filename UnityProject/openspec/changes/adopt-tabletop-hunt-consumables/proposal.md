## Why

狩猎事件已经能造成普通部位伤势并奖励包扎布，3D 状态桌也会展示猎人携带物，但消耗品只能回营后使用。玩家在远征中获得急救用品却无法处理伤势，现有内容之间存在直接的可玩流程断点。

## What Changes

- 当前行动猎人的可用 Consumable 携带卡成为可点击实体卡，并打开四张世界空间身体部位卡。
- Hunt ActionQueue 在单一 root 内重验当前猎人、冻结物品世代、携带数量、效果和部位伤势，再消耗一件并恢复普通生命。
- 成功使用发布 Hunt-scoped 玩法事实并触发现有活动狩猎检查点；读档继续复用现有生命与 Collectibles 字段。
- 首期只允许当前行动猎人使用自己的物品治疗自己，不增加跨猎人治疗、物品转移、持续效果或新存档字段。
- 直接复用 `weathered_field_dressing` 与 `mushroom_flesh_poultice`，不新增内容条目。

## Capabilities

### New Capabilities

- `tabletop-hunt-consumables`: 3D 携带物卡、部位选择与 Hunt ActionQueue 权威消耗品使用。

## Impact

- Hunt ActionSession 与一次远征的交互租约。
- 3D 狩猎状态桌、携带物卡和世界空间部位面板。
- 活动狩猎存档格式不变；不修改 GameManager、Campaign 日历或 Showdown。
