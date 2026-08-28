## Why

各阶段的玩法事务已有分段验证，但玩家从 3D 营地桌面连续完成一次非战斗远征仍缺少统一的生产输入契约。需要收养已实现的物理卡牌与世界空间指针闭环，确保它只转发玩法意图，并由既有阶段 ActionQueue、回营事务和日历权威完成提交。

## What Changes

- 统一实体卡牌的短按释放路径，鼠标、触摸、控制器和测试适配器共享同一 View 输入生命周期。
- 让已解析的地块与资源棋子世界指针入口不受无关鼠标 UI 状态误拦截；Unity 鼠标回调仍阻止 UI 穿透。
- 验证营地事件、3D 编队与路线、地块侦察、移动、采集、回营事件、季节提交和 Continue 恢复组成连续闭环。
- 保持手势与演出为 View 状态；只有既有端口接受意图后才进入阶段 ActionQueue。

## Capabilities

### New Capabilities

- `physical-tabletop-campaign-loop`: 定义玩家通过 3D 实体输入完成一次非战斗战役循环及可靠恢复的集成契约。

### Modified Capabilities

无。

## Impact

影响 `CardView3D` 的既有输入缝隙、事件/出发/采集实体卡、地块与资源棋子点击适配器，以及两条生产 PlayMode 回归。不改变领域规则、ActionQueue 边界、存档 schema、GameManager 职责、Showdown 或 InteractionSystem。
