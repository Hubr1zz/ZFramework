# GameManager 架构迁移计划

## 结论

`Core.GameManager` 已一次性收敛为 Unity `MonoBehaviour` 组合外壳：保留序列化引用、Unity 生命周期、场景根、本地化、表现装配与公开兼容 API。一次战役 lease、顶层阶段编排、跨阶段事务、持久化和统一读档恢复全部由 plain C# `CampaignFlowCoordinator` 持有；三个阶段管理器仍由 ZFramework `CampaignRuntime` 唯一管理。

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
- 第 3 步已完成：`CampaignFlowCoordinator` 是所有 transaction host、Campaign lease、持久化协调器和阶段切换的唯一 owner；`GameManager` 不再实现 transaction host，也不持有 runtime、phase manager、session 或 transaction。
- 第 2、5 步的权威生命周期边界已完成：Settlement/Hunt/Showdown phase manager 由 ZFramework `CampaignRuntime` 唯一持有，分别管理 generation、ActionSession 与表现绑定。`GameManager` 仍保留公开兼容外观，View 的进一步窄接口迁移不再影响核心编排边界。
- 第 6 步已提前完成安全子项：移除 `GameManager.OnDestroy` 中的全局 `EventBus.Clear()`，并在销毁时清空单例；完全删除兼容单例仍须等待调用方迁移。
- 跨阶段事件表现已统一为 `IPlayableEventInput` 驱动的世界空间 3D 卡牌面板；Settlement/Hunt Runner 仍分别拥有规则环境与 ActionQueue，物理骰子继续在 Runner 内等待。旧 `EventPopupHunt` 和营地 HUD 事件入口仅保留兼容用途。
- 营地出猎已改为世界空间猎人卡编队与地区卡选择：View 只暂存玩家意图，Settlement Runner 校验并提交名册，Campaign Runner 完成阶段切换。旧 `SettlementManager.TryDepart` 已降为注入式请求端口，不再写名册、发布出发事件或直接切阶段；`DepartureConfirmWindow` 与 `SettlementUIManager.ShowDepartureConfirm` 暂作场景兼容层，确认无序列化引用后再一次性删除。
- 狩猎主动回营已改为地图边缘实体卡确认：Hunt Runner 只准备结算快照，Campaign Runner 接受切换后才转移采集物、结算成长并把记录交给 Settlement；原 Hunt HUD 与流程引导不再提供绕过 Runner 的屏幕按钮。
- 营地发明与工坊建设已形成连续 3D 桌面流程：发明卡和蓝图卡只提交 Settlement Action，事务提交后重建工坊区并开放配置配方；正式 Bootstrap 不再创建旧屏幕空间建设窗，旧 HUD 也不再暴露发明、制造和装备库存旁路。
- 猎人休养已并入 3D 猎人装备桌：负伤猎人才显示休养入口，四张部位卡根据伤势与配置资源展示可用状态，恢复仍由 Settlement ActionQueue 提交；正式 Bootstrap 不再创建旧屏幕空间休养窗。
- 营火招募已迁入 3D 桌面：入口卡持续呈现年度与资源资格，候选模板以分页猎人卡展示，玩家通过世界空间命名牌输入名字后提交 Settlement Action；正式 Bootstrap 不再创建旧屏幕空间招募窗。
- 猎人成长、武器训练与营地年鉴已迁入 3D 桌面：成长分配新增 Settlement Action 与 Reactor 边界，流派训练复用配置目录和既有训练 Action，年鉴分页展示时间线与狩猎历史；正式 Bootstrap 不再创建旧 `PlayableSettlementHud`，3D 出发端口独立运行。
- 症状内化与克服已迁入 3D 猎人桌面：症状卡只提交稳定猎人/症状 ID 与选择，Settlement Action 在执行时重新验证并发布统一事务事实；正式 Bootstrap 不再创建旧屏幕症状窗口。
- 通用阶段入口已禁止未经准备的 Hunt → Settlement 直接切换；旧开发面板调用同一入口时会自动改走正式回营请求，避免调试操作静默丢失采集物。
- 共享事件节点新增阶段资源命令端口：营地事件继续写权威库存，狩猎事件只改写猎人 `Collectibles`，并在 Campaign 接受回营后统一转入库存；`GameManager` 不再需要按事件 ID 分流资源奖励。
- 狩猎状态桌已改为在事件节点提交事实后重读权威猎人状态；猎人卡与回营确认复用同一携带物只读投影，采集和事件奖励不再出现桌面摘要口径分叉。
- 狩猎行动资格已统一：事件杀死当前行动者时在提交事实前切换到存活队员；远征全员失能后地图与采集停止提交、3D 状态桌指向正式回营卡；采集过程中失去猎人会释放资源点预约而不产生素材。
- 事件效果提交已增加不可变的单项/批次结果：营地与狩猎 Root 可读取每项 Applied/Failed、失败原因和聚合计数；资源不足与无效猎人目标不再只写 Warning 或被误报为已处理。
- 年度推进已改为配置化季节日历：默认一年两个季节，每个稳定 HuntRecord 成功回营只提交一个季节，完成日历配置中的全部季节后才进入下一年并触发一次年度事件；失败、取消和读档恢复不推进。旧 `HuntsCompletedThisYear/HuntsPerYear` 只作为兼容迁移输入。

## 已知剩余风险

1. 单个行动卡效果契约尚不接受 `CancellationToken`。作用域守卫会阻止旧会话在 await 返回后推进状态机，但不能强制终止已开始的效果内部逻辑。
2. 阶段切换和恢复已收归 `CampaignFlowCoordinator`，并保留候选 prepare/swap/release 与提交前回滚；新增跨阶段流程必须增加窄 transaction，不得把分支重新放回 `GameManager`。
3. `GameManager` 仍是公开兼容外观与 Unity 表现装配点。删除 `Instance`、Dev API 或旧序列化表面需要先迁移所有场景/工具调用方，但不再阻塞核心流程扩展。
4. 当前 `CombatSession` 仍通过全局 EventBus 接收伤亡、有效伤害等事实；事件尚未携带 SessionId。正常生命周期已在离场时退订，但未来并行模拟或异步事件跨帧延迟时，需要由 ActionEnvironment/Outbox 提供明确会话归属。
5. 事件输入端口由 Flow 分发给 Hunt/Settlement 的独立执行环境；`GameManager` 只保留 pre-Awake 注册兼容，确保世界空间 View 在 Awake 前安装时不丢失。未来 View Installer 完成迁移后可删除该兼容缓存。
6. `TriggerCombat` 已使用带 SessionId、来源阶段与 EncounterId 的结构化请求，并由 Campaign Runner 校验、解析和切换；营地年度事件也已迁入 Settlement Runner。旧 `EventSystem` 的共享队列 API 已无生产调用者，但旧类和两套兼容 UI 尚未删除；确认场景引用后应一起收口，避免新内容重新接入双编排入口。
7. 进入营地的自动保存仍是异步 `.Forget()`，领域切换成功与磁盘失败没有统一结果；后续需要显式保存重试/退出策略，不能把文件 IO 伪装成可回滚领域事务。
8. 战斗事件目前默认使用当前狩猎小队，或营地全部可用猎人；事件级参与者选择规则尚未定稿。正式出现单挑、护送或临时盟友遭遇时，应让遭遇定义产生显式 Roster Plan，而不是在 `GameManager` 增加名称判断。
9. `PlayableSettlementEventView` 已承担营地与狩猎共用表现，类名不再准确。未来迁移 View Installer 时应在确认场景序列化引用后一次性改名，避免为了命名洁癖引入多轮兼容迁移。
10. 出猎已由 `CampaignHuntDepartureTransaction` 串联 Settlement 与 Campaign Runner，并由 Flow 持有；后续仍需通过该事务扩展补偿，不得增加第二套入口。
11. `InventionSystem` 仍通过 `effectDescription` 关键词直接修改猎人，并把资源消费、解锁和效果应用包在一个不可回滚的方法内。当前 Settlement Action 已补齐串行入口、重复成本校验和提交事件；后续表驱动效果落地时，应把发明效果改为结构化定义，并由可预检、可补偿的领域事务执行，避免文本变化或效果异常造成部分提交。
12. 招募曾把“可出战猎人数”误作“存活猎人数”，导致暂时不可用的活人不计入人口与成本；本阶段已统一现有招募入口与 Action 为存活人数。后续人口规则若要区分退休、失踪和外出状态，应建立显式人口口径枚举，不再复用语义含混的列表数量。
13. 当前全部猎人死亡会立即进入 GameOver，但招募规则又定义了“无人守火时免费援助”；两者在最后一名猎人死亡的路径上互相冲突。应在正式确定败局条件时选择其一：保留救援窗口并延迟败局，或删除空营免费招募语义，避免玩家看到无法兑现的规则。
14. 已关闭：`GameManager` 的旧成长异步 facade、`PlayableHunterAdvancementAdapter.TrySpendGrowth` 与旧成长 IMGUI 表现已移除；成长分配统一经 Settlement ActionQueue/3D 桌面提交。
15. 已关闭：`PlayableSymptomGrowthService` 与 `PlayableSymptomGrowthView` 无生产绑定或序列化引用，已删除；症状内化/克服统一由 3D 症状面板和 Settlement ActionQueue 提交。
16. 全量 EditMode 在 Play Mode/重编译后的首次运行中，既有 `PlayableHuntActionSessionTests` 偶发出现事件输入未启动；同一夹具立即独立复跑 9/9、随后全量 358/358 通过，说明测试仍依赖未显式重置的静态或异步环境。后续应定位共享状态并在夹具 SetUp/TearDown 中隔离，不能靠重跑作为长期门禁。
17. `EventSystem` 的效果列表仍按顺序直接提交，现已把单项失败与批次计数交还所属阶段 Root，但不能撤销前序效果。大量复合事件进入内容表前，仍需按具体内容明确“允许部分成功”与“必须原子提交”两类策略；后者应使用可预检、可补偿的专用复合效果，不要让 View 或 `GameManager` 做补偿。
18. 已关闭：`ISettlementDepartureRequestPort`、`SettlementManager.TryDepart` 与 `GameManager` 旧出猎旁路已移除；正式 3D 编队/目的地 View 直接调用 `DepartForHuntAsync`，继续由 Campaign Hunt Departure transaction 统一提交。
19. 继续战役现以 `AnnalEntry` 引用重建未完成营地事件，但事件完成仍由旧 `EventSystem` 按 EventId 查找最后一个未完成条目。当前顺序链最终能收敛；若后续允许同 ID 多实例并行、事件中途 checkpoint 或跨阶段恢复，应先为年鉴条目增加稳定实例 ID，并让 Action 按实例提交完成状态，不要继续扩大 EventId 猜测规则。
18. `HuntUIManager` 仍需逐项订阅地块、采集和事件节点提交事实来刷新同一状态桌。继续增加狩猎写路径后，应由 `HuntSession` 发布带会话身份的统一 ReadModel 失效事实，避免 View 遗漏新事实或收到旧会话的延迟刷新；在 Session 抽取前不为此单独重构全局事件总线。
19. 远征小队全员死亡时，本阶段只关闭探索并保留回营结算，没有决定死者携带物、装备和尸体如何返回营地。该规则会显著改变惩罚强度与玩家负反馈，需先在设计层明确“自动回收、部分遗失或救援事件”后，再把对应结算策略注入 `PrepareHuntRetreatAction`，不得散落在 View 或 `ResourceSystem` 中。
20. 营地直接事件链已用稳定 chain/occurrence 检查点封闭“父事件已提交、子事件只在内存”的恢复缺口，但保存通知仍通过既有异步订阅落盘，当前保证的是同一内存状态边界完整，不宣称跨进程磁盘事务。若未来效果写入独立存储域，应引入 occurrence 提交令牌与幂等效果协议；内容定义 revision 的迁移也应在读表版本体系确定后统一补充。
21. Hunt 回营交接由 `CampaignHuntReturnTransaction` 与 Flow 的提交边界持有，持久化 `PendingHuntReturn` 和稳定 `RecordId` 继续提供恢复门禁。未来新增跨域副作用必须进入显式计划或阶段 Action，不能插入权威检查点之前。

## 暂不改动

- 不在迁移中同时重写行动卡、Timeline、Boss AI 或结算规则。
- 不把 GameCore 变成 MonoBehaviour，也不把 Unity 资产引用下沉到纯规则层。
- 不为追求“全框架化”创建玩法专用全局 Module；会话对象由组合根拥有即可。

## 每步验收

- Unity CLI 编译与控制台无错误。
- 现有聚焦 EditMode 测试通过。
- 数据探针至少覆盖新游戏/读档、营地到狩猎、狩猎到决战、胜败返回营地。
- 第一场和第二场战斗的会话状态互不污染。
- 玩家真实存档哈希在探针中保持不变。
