## Context

`HunterEquipmentPanel3D` 同时承担仓库和装备投影、拖拽意图与异步命令反馈。原仓库卡仅被放到槽位坐标，没有建立 `CardSlot.OccupantCard` 与 `CardView3D.CurrentSlot`；此外 presentation generation 与 gameplay pending 共用生命周期，隐藏面板可能提前开放第二次命令。

## Goals / Non-Goals

**Goals:**

- 让所有持久展示卡都满足 Slot/Card 双向一致，并安全清理。
- 让装备/卸装在 ActionQueue 完成前保持单命令门禁，即使面板隐藏或重绑。
- 让成功、失败与 Reactor prevent 都从权威状态收敛到可重试的 3D 表现。
- 固定消耗品槽为 transient intent target。
- 让物理 Collider 鼠标与其他世界空间指针适配器复用同一拖拽阈值、投影和结束路径。

**Non-Goals:**

- 不新增领域模型、拖拽 gameplay Action、InteractionSystem 或屏幕 UI。
- 不改变装备限制、消耗品效果、物品内容或 Showdown。
- 不让 SlotGrid 成为存档或玩法权威。

## Decisions

- 仓库卡通过 `CardSlot.PlaceCard` 建立双向投影；清理统一采用 `ClearCard` 后销毁，避免 Unity fake-null 掩盖陈旧引用。
- `command token` 只由匹配的异步完成释放；`presentationGeneration` 只阻止旧展示写入。pending 中的新展示可重建，但所有命令卡保持禁用。
- 成功后主动读取 Settlement/Hunter 权威状态重建，不依赖表现事件的到达时序。
- 消耗品命中 use grid 后立即恢复原槽，再进入既有部位选择和 ActionQueue 恢复命令。
- `CardView3D` 暴露窄指针输入缝隙：Unity `OnMouse*` 只做代理，屏幕指针由主相机投影到卡牌平面，已解析的触摸/控制器世界落点可直接复用；只有卡槽解释落点时才允许进入玩法命令入口。

## Risks / Trade-offs

- [隐藏期间命令完成] → token 先释放 gameplay pending；下次显示从权威状态重建，不让旧结果写入旧卡。
- [刷新与异步完成交错] → pending 刷新只记录请求，完成时统一重建。
- [缺少真实硬件输入回放] → PlayMode 已验证物理 Collider 射线命中、主相机投影和同一生产拖拽路径；不同设备手感与演出仍留给体验阶段。
