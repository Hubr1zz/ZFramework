## Why

狩猎事件的 `AddResource` 与 `RemoveResource` 已经只读写当前远征携带物，但 `MinimumResource` 条件仍从营地库存读取。于是实体选项卡可能把刚获得的远征素材判为不可用，或用营地库存错误解锁一个实际提交时必然失败的选项，破坏 View 与 Hunt ActionQueue 的共同权威。

## What Changes

- 为阶段资源命令增加只读可用量能力，Settlement 与 Hunt 分别暴露营地库存和存活小队携带物。
- 事件 View、自动选择与 ActionQueue 二次校验使用同一个阶段资源作用域，不合并两个库存。
- 资源要求文案明确显示“营地拥有”或“小队携带”，View 只投影可用性，不扣除资源。
- 用正式“锈蚀葬坑 → 睁眼的石片”父子链验证远征奖励、条件解锁、消耗与恢复。

## Capabilities

### Modified Capabilities

- `hunt-event-resource-staging`: Hunt 资源条件与增减效果使用同一远征携带物作用域。
- `table-driven-hunt-events`: 读表资源门槛在 Hunt runner 中按阶段资源端口权威重验。
- `tabletop-event-interaction`: 世界空间选项卡显示并重验当前阶段的资源要求。

## Impact

- 共享事件资源端口、选项可用性、事件输入接口与事件事务。
- Hunt 携带物查询和 Settlement 库存兼容路径。
- 不改变事件表、活动狩猎 schema、日历、GameManager、阶段 manager 或 Showdown。
