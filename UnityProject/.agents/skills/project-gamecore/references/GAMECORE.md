# GameCore 分层架构

> ZFramework 迁移路径：本文中的 `Assets/Scripts/` 统一对应
> `Assets/GameScripts/GameLogic/HuntingInDarkness/`。GameCore 仍由独立
> `HuntingInDarkness.GameCore` asmdef 强制 `noEngineReferences`。

## 目标与依赖方向

存在对应职责时，玩法及可复用规则采用以下单向依赖：

```text
ViewLayer (输入、动画、3D/uGUI)
        ↓
Adapters/Unity (SO 映射、UniTask、EventBus、Unity 坐标、组合根)
        ↓
GameCore (纯 C# 规则、状态、端口)
```

`Assets/Scripts/GameCore/HuntingInDarkness.GameCore.asmdef` 设置了
`noEngineReferences: true`。因此 GameCore 不能引用 UnityEngine，也不能引用位于
默认 `Assembly-CSharp` 中的项目类型。该约束是编译边界，不是命名约定。

依赖只能向下：ViewLayer 可以调用 Adapters 和 GameCore；Adapters 可以调用
GameCore；GameCore 不知道 Unity、ViewLayer、ScriptableObject、EventBus 或 UniTask。三层是职责边界，不是每个功能必须凑齐的目录模板：只有独立领域状态/规则、Unity 或外部桥接、表现输入三类职责真实存在时才拆出完整三层；局部 DTO、纯函数或单一边界归入最低且真实的 owner，不创建空壳层、镜像模型或只做转发的冗余类型。

## 当前模块

| GameCore 模块 | 职责 | Unity 侧适配器 |
|---|---|---|
| `Foundation` | 可注入随机源、领域事件输出端口 | `BossController` / `CombatManager` 注入 `SystemRandomSource`，EventBus 留在适配器 |
| `Combat` | 战斗属性、攻击判定、部位状态、加权抽取、时点规则、回合阶段 | `Adapters/Unity/Combat/` 保留原公共 API，并把 SO/事件转换到领域对象 |
| `Hunters` | 猎人四部位生命/护甲、伤害顺序、死亡牌堆与永久损伤扩展口 | `CharacterCombatStats` 持有每名猎人的独立运行时状态，Boss 伤口步骤把领域结果翻译为 EventBus 事件 |
| `Board` | 引擎无关坐标、六边形拓扑、多实体占位、可破坏状态、击退规划与地形能力查询 | `Adapters/Unity/Board/BoardManager` 负责 `Vector2Int` / `Vector3` 转换；表现层可先播放 `KnockbackPlan.Path` 再提交结算 |
| `Cards` | 行动卡朝向状态、通用条件组合规则 | `CharacterActionCardInstance` 映射 ScriptableObject 模板和运行时效果 |
| `Settlement` | 猎人状态、资源、出发、事件骰、发明与制造规则 | `Adapters/Unity/{Data,Settlement,Persistence}/` 映射 SO、EventBus 与 JsonUtility |
| `Hunt` | 探索地图、导航、翻图、资源采集和事件概率 | `Adapters/Unity/Hunt/` 映射 HexTileData、Vector2Int 与阶段事件 |

## 数据边界

- ScriptableObject 只作为 Unity 策划配置。进入规则层时映射为 `Definition` / `Profile`。
- GameCore 运行时状态是权威状态；适配器可提供旧 API 的只读或兼容属性。
- 猎人存活状态以 `HunterInjuryState` 为权威；旧 `TemporaryWounds` / `PermanentWounds` 字段只保留序列化兼容，不再通过固定阈值判定死亡。
- Unity 坐标只存在于适配器和表现层；GameCore 使用 `GridPosition`。
- 战场交互采用 plan/commit 两阶段：GameCore 先返回确定性路径与碰撞结果，Adapter 完成移动动画后再提交位置和伤害；碰撞伤害公式由 `IImpactDamagePolicy` 注入。
- 随机规则接收 `IRandomSource`，正式游戏默认 `SystemRandomSource`，模拟与测试可注入固定 seed。
- GameCore 不直接发布 EventBus。领域结果由适配器翻译成现有事件，确保表现层订阅不受迁移影响。

## Unity 装配与配置边界

- 新玩法先确定一个高内聚的 GameCore 规则/状态 owner，再由 Unity 适配器装配；如果功能只有单一职责，直接放入该 owner，不为满足三层形式新增转发层。不要以“能挂在物体上”为理由新增 `MonoBehaviour`。
- 运行生命周期优先由 ZFramework 的 Singleton System/Module/Procedure 持有。`MonoBehaviour` 只用于必须依赖 Unity 生命周期、场景身份、序列化引用或表现输入的边界；组合根保持轻薄，只转发 Unity 回调并装配 plain runtime/system，不持有跨阶段业务规则。
- `CardGame.ActionQueue` 只编排会改变权威游戏状态、产生随机或玩家选择结果、发布游戏性事实，或允许 Reactor 覆盖/注入的游戏性 Action。纯布局、Hover、按钮视觉和动画不进入队列；游戏性 Action 可以等待 Presenter 完成，但表现步骤本身没有可被 Reactor 单独拦截的 Action 身份。
- 旧 `HuntingInDarkness.GameCore.Cards.ActionQueue` 仅是战斗卡牌兼容层；不得承载新功能。后续以一次完整迁移把其调用方接入正式 `CardGame.ActionQueue` 环境，迁移完成前不做零碎扩张。
- 同一玩法的可调参数集中到少量 ScriptableObject，再一次性映射为 GameCore `Definition` / `Profile`；避免多个对象各自暴露一部分关联参数。场景引用只暴露必须由开发者选择具体实例的对象。
- Prefab/场景负责稳定结构和布局，代码负责数据绑定与规则执行。设计文档必须区分自动构造内容与人工 Inspector/场景操作，避免让开发者重复挂载可由组合根创建的对象。

## 扩展到其他玩法模块

新玩法规则应在 GameCore 下建立独立模块，复用 `Foundation`
和必要的 `Board` 类型。Unity 数据资产只负责映射配置，不把 ScriptableObject 传入规则对象；
视图只读取查询模型并提交命令，不直接修改领域状态。

## 兼容迁移规则

当前 `Core` / `GameplayBase` 命名空间为了避免全库破坏暂时保留。判断层级以程序集和目录为准，
不以旧命名空间为准。后续迁移应优先保持现有序列化 GUID 和公共 API，再逐步收缩兼容面。
