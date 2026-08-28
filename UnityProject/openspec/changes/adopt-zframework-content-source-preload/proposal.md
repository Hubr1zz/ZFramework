## Why

可游玩内容此前通过多个 `Resources.Load/LoadAll` 隐式发现，既绕过 ZFramework/YooAsset 生命周期，也让 Editor、Player 与测试可能装配出不同内容。正式启动需要在进入游戏逻辑前取得一份可验证、可释放的完整内容源闭包。

## What Changes

- 新增独立 `PlayableContentSourceManifest`，显式聚合启动配置、事件/血脉/物品/配方表、营地扩展与中文字体。
- 由 ZFramework `PlayableContentSourceSystem` 通过 `GameModule.Resource` 持有单一 Manifest 资源租约，并在 Singleton 生命周期结束时释放。
- `ProcedureStartGame` 先异步准备内容源，成功后才调用 `GameApp.Entrance` 并隐藏 Launcher；失败时保持启动界面并停止进入游戏。
- Campaign 候选、事件世代和 Settlement Plan 只消费显式 Bundle，不再扫描本地 Resources 路径。
- 内容资产迁移到 `AssetRaw`，保留稳定 GUID；Showdown、ActionQueue 与玩法规则不变。

## Capabilities

### Modified Capabilities

- `zframework-startup-lifecycle`
- `campaign-content-preflight-assembly`
- `settlement-content-plan-lifecycle`
- `player-build-bootstrap`

## Impact

这是已完成代码的 post-hoc adoption Change。影响 ZFramework 最终启动 Procedure、游戏组合根、内容装配输入、内容表兼容重建入口、YooAsset 收集资源及相关测试。正式 Specs 保持不变，待人工审查后显式 sync。

## Verification Boundary

Unity 6000.5.9f1 编译通过；内容源生命周期 4/4、装配事务 18/18、相关内容表 45/45；独立 C# 构建 0 error。未执行完整 Player 构建和全量测试。
