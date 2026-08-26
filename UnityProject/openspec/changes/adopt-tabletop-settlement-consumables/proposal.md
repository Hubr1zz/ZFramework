## Why

设计文档把消耗品定义为可提供即时收益的物品卡，但正式营地流程只支持资源、装备与资源付费休养。`ItemType.Consumable` 因而没有生产内容和使用入口，并会被旧装备规则错误地视为可装备物品。

## What Changes

- 增加读表消耗品效果配置，并以“菌肉敷剂”接通医疗工坊的基础生产与使用闭环。
- 将现有非资源物品仓库兼容字段扩展为装备与消耗品共用存储，同时只允许 Weapon/Armor 进入装备槽。
- 在 3D 猎人装备桌增加实体使用槽，复用四张身体部位卡选择恢复目标。
- 消耗与恢复由 Settlement ActionQueue 原子提交；拖拽、面板和提示保持表现职责。
- 旧存档中非法装备的消耗品幂等返还仓库，不改变存档 schema。

## Capabilities

### New Capabilities

- `tabletop-settlement-consumables`: 读表生产、3D 使用、部位选择和权威消耗闭环。

## Impact

- 营地物品表、配方表、内容计划校验与非资源物品仓库。
- Settlement runner、Reactor、事务事实和现有保存边界。
- 3D 装备桌与休养部位卡；不增加屏幕空间 UI。
- 不改变 Hunt、Showdown、ActionQueue 核心或存档 schema。
