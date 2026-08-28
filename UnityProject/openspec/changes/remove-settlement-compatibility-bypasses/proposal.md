## Why

Settlement 的成长、症状与出猎已经由 3D 桌面和阶段 ActionQueue 提供正式玩家入口，但 GameManager、SettlementManager 与旧屏幕 UI 仍保留未绑定的兼容旁路。它们会让调用者绕过正式 runner，造成第二套出猎/成长权威，并让旧提示与 SettlementNoticePresenter3D 产生重复反馈。

## What Changes

- 移除 GameManager 的旧成长 async facade 与旧出猎 facade，以及 SettlementManager 的 compatibility departure port。
- 保留 `ApplyAfterHunt` 两个兼容重载和 `HunterGrowthSpentEvent`，不改变 Showdown 链或回营语义。
- 保留 3D 编队、目的地 View、`IPlayableHuntDepartureInput` 和 typed `DepartForHuntAsync` 正式链。
- 删除无生产绑定的症状 screen-space Service/View 与三个旧 IMGUI Toast；长期提示只由 `SettlementNoticePresenter3D` 消费 after-commit facts。

## Capabilities

### Modified Capabilities

- `game-manager-orchestration`
- `tabletop-hunt-departure`
- `tabletop-settlement-advancement-ledger`
- `tabletop-settlement-symptoms`
- `tabletop-settlement-notices`

## Impact

这是已完成代码的 post-hoc adoption Change。影响 Settlement/Campaign 端口、GameManager 公开表面、成长适配器、旧屏幕表现和相关测试；不改变规则、存档 schema、ActionQueue、Combat 或 Showdown。

## Verification Boundary

Unity 编译和 EditMode 证据按已完成验证记录；PlayMode 当前因 Unity license handshake 暂标 blocked/pending，不宣称通过。正式 Specs 不在本 Change 中直接修改，待人工审查后显式 sync。
