# 营地建设与探索狩猎分层

## 营地建设

| 层 | 目录 | 职责 |
|---|---|---|
| GameCore | `GameCore/Settlement/` | 猎人状态、资源账本、意志/死亡判定、出发限制、事件骰、发明与配方规则 |
| Adapter | `Adapters/Unity/Settlement/Legacy/` | SO 定义映射、EventBus 兼容事件、年份/事件链协调、日志 |
| Adapter | `Adapters/Unity/Data/` | Hunter/Item/Event/Invention 等 ScriptableObject，以及 JsonUtility 兼容运行时包装 |
| Adapter | `Adapters/Unity/Persistence/` | `Application.persistentDataPath` 与 JsonUtility 存档 |
| ViewLayer | `ViewLayer/Settlement/UI/`、`Table3D/` | 营地 HUD、事件弹窗、猎人详情和 3D 桌面分区 |

`HunterInstance` 继承 GameCore 的 `HunterState`；Unity 专属的 `ItemData`、装备实例引用与
Inspector 模板仍由 Adapter 子类持有。资源存储的列表格式保留，以维持 JsonUtility 存档字段兼容，
但增减、消费和非负约束由 `ResourceRules` 执行。

## 探索狩猎

| 层 | 目录 | 职责 |
|---|---|---|
| GameCore | `GameCore/Hunt/` | 地图生成、地块可见状态、翻图扩散、队伍导航、资源生成/采集、事件概率 |
| Adapter | `Adapters/Unity/Hunt/Legacy/` | HexTileData 映射、EventSystem 协调、EventBus 与 Unity 坐标桥接 |
| ViewLayer | `ViewLayer/Hunt/World/` | 地图物体、相机、地块点击输入 |
| ViewLayer | `ViewLayer/Hunt/UI/` | 狩猎 HUD、资源采集与事件弹窗 |

GameCore 使用 `GridPosition` 和 `HuntTileState`。`HexTileInstance` 是兼容包装，持有 SO 引用、
Vector2Int 坐标和对应的 GameCore 状态；世界坐标换算只存在于 Adapter/ViewLayer。

## 事件与随机

- 营地和狩猎共用 `IRandomSource`，同一阶段的子系统共享一个实例，固定 seed 时可重放。
- GameCore 返回状态变化或判定结果，不调用 EventBus、不记录 Unity 日志。
- Adapter 将结果翻译成既有 `YearAdvancedEvent`、`ResourceChangedEvent`、
  `GameEventTriggeredEvent`、`HuntDepartedEvent` 等事件，现有表现订阅无需改写。
- ScriptableObject 仍是策划入口；进入 GameCore 前转换为 `Definition` / `ResourceCost` 等纯数据。
