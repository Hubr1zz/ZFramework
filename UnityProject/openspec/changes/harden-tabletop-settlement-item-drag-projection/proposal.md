## Why

营地装备桌虽然已能提交装备与消耗品命令，但仓库卡未真正占用 SlotGrid，刷新、失败恢复和异步等待期间可能留下错误的卡槽关系或重复提交入口。需要把 3D 桌面投影收口到统一槽位语义，同时保持 Settlement ActionQueue 为唯一玩法权威。

## What Changes

- 仓库、装备与临时使用槽统一维护 CardSlot/CardView3D 双向关系，并在刷新前先解除槽位再销毁投影。
- 装备/卸装命令等待期间锁定重复拖拽与翻页；面板隐藏或重绑不得提前释放 gameplay pending。
- 消耗品使用槽仅表达命令意图，触发后立即恢复仓库卡，不持久占槽。
- CardView3D 将 Unity 鼠标、触摸/控制器适配器和测试输入收口到同一可配置阈值拖拽生命周期；指针手势本身不进入 ActionQueue。
- 增加 Collider/主相机射线、窄 View 与正式 GameManager 组合根 3D 拖拽回归。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `tabletop-settlement-equipment`: 明确 SlotGrid 投影一致性、跨展示生命周期的单命令门禁与权威提交后的主动重建。
- `tabletop-settlement-recovery`: 增加从仓库消耗品卡进入部位选择的临时 3D 投递契约。

## Impact

影响 `CardView3D`、`HunterEquipmentPanel3D` 及其 PlayMode 回归；复用既有物品内容、Settlement ActionSession/ActionQueue、猎人恢复面板与 GameManager 组合根。不改变物品规则、存档 schema、InteractionSystem、MonoBehaviour 权威或 Showdown 玩法。
