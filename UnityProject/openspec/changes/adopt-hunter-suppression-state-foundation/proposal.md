## Why

设计文档已经定义猎人压抑值的默认值、0–8 范围和“疯狂／正常／消极”三档，但生产代码仍允许事件无限增加，配置表也只拒绝负值。需要收养已实现的最小规则底座，让数值合法、旧档可恢复且玩家能在既有 3D 猎人信息中理解当前分类。

## What Changes

- 以纯 GameCore 规则定义默认 4、硬范围 0–8 和三档分类。
- `AddInsanity` 只接受正向玩法效果，并在 8 饱和；仍由既有事件 ActionQueue 提交。
- 内容表对越界猎人初始值和非正事件增量 fail closed。
- 旧档越界值在存档恢复边界归一，既有 3D 猎人卡和装备详情只读显示数值与分类。
- 修复事件表测试夹具对静态缓存顺序的依赖，使组合测试显式装配内容源。

## Capabilities

### New Capabilities

- `hunter-suppression-state-foundation`: 定义猎人压抑值的合法范围、分类投影、事件增长和旧档兼容契约。

### Modified Capabilities

无。

## Impact

影响猎人纯规则、事件效果、猎人/事件内容表校验、存档恢复和既有 3D 猎人信息文本。不新增分类效果、事件条件、出猎门禁、UI ActionQueue、MonoBehaviour 权威或 Showdown 玩法。
