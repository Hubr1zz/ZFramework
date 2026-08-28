## Why

狩猎携带物和活动存档已经能保存通用物品，但事件奖励与回营协议仍只接受资源，导致事件获得的消耗品或装备无法进入既有营地仓库和 3D 装备桌。继续为每类物品增加专用回营字段会重复扩展跨阶段事务，也会放大旧档迁移风险。

## What Changes

- 增加 Hunt-only `AddItem` 事件效果，只能把已登记的非资源物品交给当前小队的存活执行者。
- 回营协议升级为 v2，以稳定物品 ID 和聚合数量保存通用携带物；v1 资源记录继续只读迁移。
- Settlement 回营 root 在修改状态前统一预检物品身份、类别、数量和容量，再把资源送入资源库存、其他物品送入既有仓库。
- 3D 回营卡展示所有携带物，并允许紧急撤退从任意一类携带物中准确放弃一份。
- 用“旧式包扎布”提供一个事件获得、回营入库的代表性消耗品案例。

## Capabilities

### Modified Capabilities

- `table-driven-hunt-events`: 增加受 Hunt ActionQueue 约束的非资源物品奖励。
- `hunt-return-outcome-checkpoint`: 把资源专用快照升级为可迁移的通用物品回营协议。
- `tabletop-hunt-retreat`: 3D 回营决策从资源扩展到全部携带物。
- `expedition-build-progression-loop`: 构筑闭环接纳狩猎直接获得的非资源物品。

## Impact

- 事件表、Hunt 事件端口、回营纯规则、Settlement 提交和相关玩法事实。
- 3D 回营卡、营地年鉴/通知与既有物品仓库投影。
- 不推进 Showdown，不新增屏幕空间权威 UI，不改变活动狩猎 schema 或现有资源采集规则。
