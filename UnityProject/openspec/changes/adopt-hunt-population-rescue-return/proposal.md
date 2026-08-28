## Why

永久伤亡已经进入战役循环，但可玩补员的人口来源仍只来自营地设施。设计文档明确允许狩猎事件带回人口；如果事件直接写入 Settlement，会越过活动狩猎检查点并在保存失败时形成双权威状态。

## What Changes

- 增加 Hunt-only `RescuePopulation` 事件效果，把匿名幸存者暂存在当前远征，不直接创建或命名猎人。
- 活动狩猎快照升级为 v3，并从 v2 明确迁移零名救援人口。
- 回营协议升级为 v3，在同一 Settlement root 内把救援人口、通用携带物、成长、历史和日历恰好提交一次。
- 3D 狩猎状态桌与回营卡显示同行幸存者；成功回营后复用既有 3D 招募板完成模板选择和命名。
- 用“迷路的幸存者”提供一个代表性读表案例。

## Capabilities

### Modified Capabilities

- `table-driven-hunt-events`: 增加由 Hunt ActionQueue 掌权的人口救援效果。
- `active-hunt-persistence`: 保存和恢复远征内救援人口。
- `hunt-return-outcome-checkpoint`: v3 原子提交人口与既有回营结果。
- `tabletop-hunt-retreat`: 世界空间卡展示同行幸存者且不把其作为弃置货物。
- `tabletop-settlement-recruitment`: 接纳狩猎归来人口作为既有招募供给。

## Impact

- Hunt 事件端口、活动狩猎存档、回营纯规则与 Settlement 回营 Action。
- 3D Hunt 状态桌、回营卡与既有 3D 招募闭环。
- 不修改 GameManager 边界，不推进 Showdown，不在 Hunt 中创建 Hunter，不新增屏幕空间权威 UI。
