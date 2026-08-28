## Why

事件效果解析已被阶段 ActionQueue 复用，但 `EventSystem` 仍保留共享队列、隐式执行猎人与 UI 回调；同时项目仍包含无生产序列化引用的营地 HUD 和狩猎屏幕弹窗。这些入口可以绕过阶段 runner，并与 3D 桌面产生第二套流程权威。

## What Changes

- **BREAKING** 移除 `EventSystem` 的共享事件队列、隐式 actor、UI callback 与有状态结算入口；保留显式 actor/context 的效果解析 adapter。
- 营地组合根不再接收或刷新兼容 2D HUD，猎人查看、装备和出发继续由 3D 桌面入口承担。
- 狩猎表现 owner 只创建 3D 小队状态板和 3D 采集面板，删除顶栏、猎人 overlay、采集弹窗与事件弹窗的 screen-space fallback。
- EventBus 仍只用于 Action root after-commit 事实与表现通知，本次不修改事件规则、ActionQueue、Showdown 或存档 schema。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `action-environment-lifecycle`: 明确共享事件 resolver 是 Action root 内的无队列 adapter，不得维护平行游戏流程。
- `tabletop-event-interaction`: 正式组合根不再依赖旧营地 HUD 开关或兼容事件面板。
- `hunt-tabletop-harvest`: 删除无世界锚点时回退至屏幕采集弹窗的旧契约，正式狩猎始终要求 3D 表现边界。

## Impact

影响事件 resolver、营地阶段表现窄端口、狩猎 3D presenter owner、六个无序列化引用的兼容 UI 脚本及相关定向测试。不改变事件表、效果语义、阶段交接、GameManager 既有边界或 Showdown 玩法。
