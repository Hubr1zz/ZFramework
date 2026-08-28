---
schemaVersion: 2
category: feature
title: "狩猎桌面资源采集"
---

## REMOVED Requirements

### Requirement: Compatibility fallback remains available
**Reason**: 正式 Hunt 启动已经把 3D 地图、状态板、物理采集面板与 ActionSession 定义为一个原子可玩边界。保留 screen-space popup 会让无世界根的错误配置静默降级，并重新引入与 3D 桌面平行的交互路径。

**Migration**: 所有生产采集表现使用 `HuntHarvestPanel3D`。资源 marker 位置不可用时，按 `tabletop-hunt-squad-status` 既有契约使用地图交互锚点；整个 Hunt 世界根不可用时则启动 fail closed，不创建屏幕弹窗。
