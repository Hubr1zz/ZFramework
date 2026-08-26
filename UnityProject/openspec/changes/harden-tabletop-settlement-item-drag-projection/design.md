## Context

`HunterEquipmentPanel3D` 同时承担仓库和装备投影、拖拽意图与异步命令反馈。原仓库卡仅被放到槽位坐标，没有建立 `CardSlot.OccupantCard` 与 `CardView3D.CurrentSlot`；此外 presentation generation 与 gameplay pending 共用生命周期，隐藏面板可能提前开放第二次命令。

## Goals / Non-Goals

**Goals:**

- 让所有持久展示卡都满足 Slot/Card 双向一致，并安全清理。
- 让装备/卸装在 ActionQueue 完成前保持单命令门禁，即使面板隐藏或重绑。
- 让成功、失败与 Reactor prevent 都从权威状态收敛到可重试的 3D 表现。
- 固定消耗品槽为 transient intent target。

**Non-Goals:**

- 不新增领域模型、拖拽 gameplay Action、InteractionSystem 或屏幕 UI。
- 不改变装备限制、消耗品效果、物品内容或 Showdown。
- 不让 SlotGrid 成为存档或玩法权威。

## Decisions

- 仓库卡通过 `CardSlot.PlaceCard` 建立双向投影；清理统一采用 `ClearCard` 后销毁，避免 Unity fake-null 掩盖陈旧引用。
- `command token` 只由匹配的异步完成释放；`presentationGeneration` 只阻止旧展示写入。pending 中的新展示可重建，但所有命令卡保持禁用。
- 成功后主动读取 Settlement/Hunter 权威状态重建，不依赖表现事件的到达时序。
- 消耗品命中 use grid 后立即恢复原槽，再进入既有部位选择和 ActionQueue 恢复命令。

## Risks / Trade-offs

- [隐藏期间命令完成] → token 先释放 gameplay pending；下次显示从权威状态重建，不让旧结果写入旧卡。
- [刷新与异步完成交错] → pending 刷新只记录请求，完成时统一重建。
- [真实鼠标射线未覆盖] → 定向测试驱动生产 CardView3D 拖拽生命周期与正式 GameManager 命令根；射线和演出留给后续体验阶段。
