# GameManager 架构迁移计划

## 结论

`Core.GameManager` 是接入 ZFramework 之前形成的测试组合根，当前已经成为正式流程的结构性阻塞点，需要一次有边界的渐进迁移。迁移期间保留其公开外观，逐步把权威职责移出，直到所有调用方改为窄接口后再删除旧实现。

## 已确认的结构问题

1. `Awake` 无条件构建 Boss 战，狩猎遭遇发生后再注入 `BattleSetup` 已经无效；代码中的 TODO 也明确记录了这一点。
2. 战斗对象、回合状态机、卡牌、掉落和可视化只构建一次，没有可靠的第二场战斗重置边界。
3. 单个 1214 行 MonoBehaviour 同时承担启动、阶段状态机、营地/狩猎/战斗会话、存档、UI 创建、输入转发、全局事件和开发命令。
4. 多个效果和 View 直接判断或持有 `GameManager`，使 Adapter/View 无法只依赖所需能力。
5. 正式启动由 `RuntimeInitializeOnLoadMethod` 创建管理器，而 ZFramework `ProcedureStartGame -> GameApp.Entrance` 只会查找现成管理器，两个入口存在时序竞争。
6. `OnDestroy` 调用全局 `EventBus.Clear()`，会删除不属于该对象的订阅；单例销毁时也没有清空 `Instance`。

## 目标结构

```text
ZFramework Procedure
  -> PlayableGameBootstrap（唯一组合根）
      -> CampaignFlowCoordinator（Settlement/Hunt/BossFight 生命周期）
          -> SettlementSession
          -> HuntSession
          -> CombatSession（每次遭遇重新创建并释放）
      -> View installers（只依赖 ReadModel / Commands）
```

GameCore 继续保存纯规则与持久状态；Unity Adapter 负责资产、场景和旧系统映射；View 只提交命令并读取快照。ZFramework 的 Procedure 负责应用启动，命名 FSM 负责战役阶段，不把具体玩法状态塞入框架模块。

## 单次迁移顺序

1. 入口归一：`GameApp` 显式请求 `PlayableGameBootstrap` 安装，移除早于 Procedure 抢跑的场景加载回调。
2. 建立窄契约：拆出 `IGameFlowReadModel`、`IGameFlowCommands`、`ISettlementReadModel/Commands`、现有战斗契约；View 与效果逐批去除具体类型判断。
3. 抽出 `CampaignFlowCoordinator`：统一阶段离开、FSM 切换、阶段进入、保存和失败回滚顺序。
4. 抽出 `CombatSession`：在进入 BossFight 时依据当前遭遇创建，离开时结算并 Dispose；允许连续多场战斗。
5. 抽出 `SettlementSession` 与 `HuntSession`，让组合根只负责构造、生命周期和 Unity 根节点。
6. 删除 `GameManager.Instance`、生产代码中的 `Dev*` 转发与全局 `EventBus.Clear()`，最后将旧类缩减为兼容外观并移除。

## 当前进度

- 第 1 步已完成：正式入口统一为 `ProcedureStartGame -> GameApp.Entrance -> PlayableGameBootstrap.EnsureInstalled`，场景加载回调不再抢跑。
- 第 4 步已完成主体抽取：战斗只在进入 `BossFight` 时创建，字段、装配、回合、卡牌、棋盘和表现适配器由 `PlayableCombatSession` 拥有，离开时通过 `PlayableCombatSessionScope` 显式释放；`GameManager` 只保留创建、结算与兼容接口转发。
- 战斗名册的存活标记和武器列表已从静态 Adapter 下沉到每个 `CharacterRuntimeData`，第二场装配不会覆盖第一场对象仍在完成的异步读取；静态 Adapter 只保留启动配置与武器资产投影缓存。
- 第 2、3、5 步仍待完成；其中 `CampaignFlowCoordinator` 应优先处理阶段转换的原子顺序、保存边界与失败回滚。
- 第 6 步已提前完成安全子项：移除 `GameManager.OnDestroy` 中的全局 `EventBus.Clear()`，并在销毁时清空单例；完全删除兼容单例仍须等待调用方迁移。

## 已知剩余风险

1. 单个行动卡效果契约尚不接受 `CancellationToken`。作用域守卫会阻止旧会话在 await 返回后推进状态机，但不能强制终止已开始的效果内部逻辑。
2. 阶段离开副作用目前早于 FSM 状态提交，转换异常时缺少事务式回滚；由 `CampaignFlowCoordinator` 统一解决，不在 `GameManager` 中继续增加临时分支。
3. `GameManager` 仍是兼容外观与临时装配点。新增玩法不得继续向其中加入领域规则，应进入 GameCore、会话对象或窄 Adapter。
4. 当前 `CombatSession` 仍通过全局 EventBus 接收伤亡、有效伤害等事实；事件尚未携带 SessionId。正常生命周期已在离场时退订，但未来并行模拟或异步事件跨帧延迟时，需要由 ActionEnvironment/Outbox 提供明确会话归属。

## 暂不改动

- 不在迁移中同时重写行动卡、Timeline、Boss AI 或结算规则。
- 不把 GameCore 变成 MonoBehaviour，也不把 Unity 资产引用下沉到纯规则层。
- 不为追求“全框架化”创建玩法专用全局 Module；会话对象由组合根拥有即可。

## 每步验收

- Unity MCP 编译与控制台无错误。
- 现有聚焦 EditMode 测试通过。
- 数据探针至少覆盖新游戏/读档、营地到狩猎、狩猎到决战、胜败返回营地。
- 第一场和第二场战斗的会话状态互不污染。
- 玩家真实存档哈希在探针中保持不变。
