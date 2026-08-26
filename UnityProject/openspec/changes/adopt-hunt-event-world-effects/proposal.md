## Why

设计文档允许狩猎事件改变地图或资源点状态，但现有事件效果只能修改资源、猎人和战役数据。若事件直接持有坐标或调用 View，会绕过 Hunt ActionQueue，并让读档恢复与 3D 棋子状态失去共同权威。

## What Changes

- 增加狩猎专属的短生命周期 world-effect 端口，只能作用于当前已提交事件地块。
- 增加 `ExhaustCurrentHuntTileResources` 读表效果；非 Hunt 内容、任意目标参数和缺失执行端口均失败关闭。
- 事件效果按既有批次报告受影响数量，并由 HuntManager 原子耗尽当前地块资源点。
- 资源点状态变化通过语义回调刷新 3D 棋子；View 仍不写玩法状态。
- “呼吸的采石场”冒险失败会获得石肺并埋没当前地块资源。

## Capabilities

### Modified Capabilities

- `table-driven-hunt-events`: 增加绑定当前已提交地块的 Hunt-only world effect。
- `tabletop-hunt-resource-markers`: 资源点被事件耗尽时也安全移除对应 3D 棋子。

## Impact

- 事件表校验、事件事务与 Hunt ActionQueue。
- HuntManager 资源点权威状态、活动狩猎检查点和 3D 资源棋子刷新。
- 不改变 Showdown、遭遇交接或存档 schema。
