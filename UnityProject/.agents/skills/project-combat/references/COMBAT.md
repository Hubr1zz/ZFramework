# CardTactics — 战斗系统

> ZFramework 迁移路径：本文中的 `Assets/Scripts/` 统一对应
> `Assets/GameScripts/GameLogic/HuntingInDarkness/`；TEngine 事件与启动接缝见
> `tengine-dev`，不改变本文记录的战斗规则权威。

## 文件分布

| 文件 | 命名空间 | 职责 |
|---|---|---|
| `GameCore/Combat/*` | `HuntingInDarkness.GameCore.Combat` | 引擎无关战斗状态、判定、加权抽取、时点与回合规则 |
| `GameCore/Hunters/*` | `HuntingInDarkness.GameCore.Hunters` | 猎人四部位伤势、护甲减伤、死亡牌堆与永久损伤扩展口 |
| `GameCore/Board/*` | `HuntingInDarkness.GameCore.Board` | 引擎无关六边形拓扑、多实体占位、可破坏状态、击退与地形交互 |
| `Adapters/Unity/Combat/CombatManager.cs` | `CardTactics.CombatSystem` | 构建 Unity 异步管线、映射上下文、发布兼容事件 |
| `Adapters/Unity/Combat/BossController.cs` | `Core` | 映射 Boss SO、管理兼容卡池与 EventBus 战利品事件 |
| `Adapters/Unity/Combat/{TimelineManager,TurnStateMachine}.cs` | `Core` | GameCore 时点/回合规则到旧 API 与 EventBus 的桥接 |
| `Adapters/Unity/Board/{BoardManager,HexGrid}.cs` | `GameplayBase*` | `GridPosition` 与 Unity 坐标/世界坐标桥接 |
| `Adapters/Unity/Combat/LegacyShowdown/AttackPipeline.cs` | `GameplayBase.CombatSystem` | 顺序执行 IAttackStep，捕获 AttackAbortedException |
| `Adapters/Unity/Combat/LegacyShowdown/CharacterAttackSteps.cs` | `GameplayBase.CombatSystem` | DrawHitLocationStep、ResolveBodyPartsStep |
| `Adapters/Unity/Combat/LegacyShowdown/BossAttackSteps.cs` | `GameplayBase.CombatSystem` | BossAttackDodgeStep、BossAttackWoundStep |
| `Adapters/Unity/Combat/LegacyShowdown/CombatData.cs` | `GameplayBase.CombatSystem` | AttackContext 与 GameCore 部位/属性状态的兼容包装 |
| `Adapters/Unity/Combat/LegacyShowdown/CombatEvents.cs` | `GameplayBase.CombatSystem` | Unity EventBus 战斗事件 struct |
| `Adapters/Unity/Combat/LegacyShowdown/IPlayerInputProvider.cs` | `GameplayBase.CombatSystem` | UniTask/Unity 类型输入端口（由 ViewLayer 实现） |
| `ViewLayer/Combat/UIPlayerInputProvider.cs` | `GameplayBase.CombatSystem` | UGUI 输入与翻牌表现实现 |
| `Adapters/Unity/Combat/LegacyShowdown/Cards/Effect/AttackEffect.cs` | `GameplayBase.CombatSystem` | CharacterAttackEffect、BossAttackEffect |
| `UI/BossCardTable.cs` | `UI` | 部位卡 3D 展台；BodyPartCardView 内嵌类 |

---

## 战场交互规则层

- `BoardEntityDefinition` 声明实体类型、是否允许重叠/穿越、生命、受伤/破坏效果 ID、闪避修正和临时行动；`BoardEntityState` 保存运行时生命与摧毁状态。
- `BoardState` 支持同格多个实体。普通单位仍阻塞落点；草丛、石块等允许重叠的地形不会让旧 `GetEntityAt` 查询误判为单位占位；已摧毁物体不再阻挡移动。
- `KnockbackService.Plan` 根据起点、方向、距离与占位生成路径和碰撞计划，但不立刻移动或扣血。Adapter/ViewLayer 可先播放 `KnockbackPlan.Path`，动画完成后调用 `TryCommit` 提交位置与双方伤害。
- 碰撞伤害数值不在棋盘模块写死，由 `IImpactDamagePolicy` 根据 `ImpactContext` 返回；当前只提供接口边界。
- `BattlefieldTerrainQuery` 从单位所在格汇总草丛 `+1` 闪避与石块临时行动 `throw-rock`（显示名“投石”），不暴露 BoardState 内部容器。

---

## 角色攻击 Boss 完整流程

```
打出攻击牌
  └→ CharacterAttackEffect.Execute()
       └→ CombatManager.CharacterAttackBoss()
            └→ AttackPipeline.Run()
                 ├─ [Step 1] DrawHitLocationStep
                 │    • 从 context.AllBodyPartStates 中筛除已摧毁的卡
                 │    • 按 drawWeight 加权随机、不放回抽取 Speed 张
                 │    • 调用 input.PlayShuffleAndReveal()
                 │       ├ 发布 BodyPartShuffleStartedEvent（BossCardTable 重排视觉顺序）
                 │       ├ 500ms 占位延迟（后续替换为 Timeline/Animation 信号）
                 │       ├ 逐张设置 state.IsFaceUp = true
                 │       ├ 发布 BodyPartFlippedFaceUpEvent（BossCardTable 翻正面）
                 │       └ 300ms 占位延迟
                 │
                 └─ [Step 2] ResolveBodyPartsStep
                      • 循环直到所有翻开的卡全部结算：
                        1. input.RequestSelectRevealedCard() — 玩家点击选一张
                        2. input.RequestRoll() — 玩家掷骰
                        3. 判定：TotalAttackPower >= toughness → Success / Failure
                        4. Success → state.CurrentHp -= 1
                              HP ≤ 0 → state.IsDestroyed = true
                                       发布 BodyPartDestroyedEvent
                        5. DefaultHitLocationEffectResolver.ResolveBodyPartEffects()
                        6. input.ShowResult()
                      • 所有卡结算完毕后：
                        非摧毁的卡 → state.IsFaceUp = false
                                     发布 BodyPartFlippedFaceDownEvent（BossCardTable 翻背面）
```

---

## 部位卡状态模型

### HitLocationCardData（ScriptableObject）
- `locationName` / `description`
- `toughness`：攻击力阈值（TotalAttackPower >= toughness → 命中）
- `drawWeight`：加权随机权重
- `maxHp`：部位血量上限（默认 1，成功命中 -1 HP）
- `effects`：`List<HitLocationEffectEntry>`（OnSuccess / OnFailure / Always 触发）

### HitLocationRuntimeState（运行时，由 BossController 持有）
- `Data`：指向对应 ScriptableObject
- `CurrentHp`：剩余血量（初始化为 `Data.maxHp`）
- `IsDestroyed`：HP 归零，本局永久正面朝上，不再参与抽取
- `IsFaceUp`：当前翻面状态（供 BossCardTable 判断渲染）

**生命周期：**
- 游戏开始：BossController 构造时逐一创建，初始 IsFaceUp = false（背面）
- 攻击时：被抽中 → IsFaceUp = true（正面）
- 结算后：未摧毁 → IsFaceUp = false（翻回背面）；摧毁 → IsDestroyed = true、IsFaceUp 永久 true

---

## EventBus 战斗事件

| 事件 struct | 发布时机 | 主要订阅方 |
|---|---|---|
| `AttackCompletedEvent` | 每次攻击流程结束 | 日志、外部监听 |
| `CharacterWoundedEvent` | 角色受伤 | UI |
| `CharacterDiedEvent` | 角色死亡 | GameManager |
| `BodyPartRevealedEvent` | 旧事件，保留兼容 | — |
| `BodyPartShuffleStartedEvent` | 洗牌动画触发 | BossCardTable |
| `BodyPartFlippedFaceUpEvent` | 部位卡翻至正面 | BossCardTable |
| `BodyPartFlippedFaceDownEvent` | 部位卡翻回背面 | BossCardTable |
| `BodyPartDestroyedEvent` | 部位 HP 归零 | BossCardTable |

---

## Boss 攻击角色流程

```
BossAttackEffect.ExecuteAsync()
  └→ CombatManager.BossAttackCharacter()
       └→ AttackPipeline.Run()
            ├─ BossAttackDodgeStep  — 闪避判定（roll + Evasion vs 阈值）
            └─ BossAttackWoundStep  — 指定部位伤害结算
                 ├─ 护甲规则先抵消伤害，再扣该部位生命
                 ├─ 部位生命降至 0 时不立即死亡
                 └─ 后续有效伤害触发致命伤：抽死亡牌
                      ├─ 死亡牌 → 角色永久死亡
                      └─ 存活牌 → 牌留在牌堆，加入 1 张死亡牌，并调用永久损伤扩展口
```

### 猎人伤势状态

- `HunterInjuryState` 独立保存头、躯干、手臂、腿的生命与护甲；`CharacterCombatStats` 为每个运行时角色持有一份实例，避免共享 ScriptableObject 状态。
- `IArmorMitigationRule` 决定护甲后的有效伤害，默认实现为直接减去护甲；`IPermanentInjuryResolver` 为存活后的永久损伤规则保留接口。
- `DeathDeck` 初始包含 1 张存活牌。抽牌不移除原牌；每次抽到存活牌后追加 1 张死亡牌，因此后续致命伤死亡概率逐步提高。
- `TemporaryWounds` / `PermanentWounds` 仅用于旧序列化与显示兼容，固定临时伤口死亡阈值已移除，不能作为权威死亡判断。

---

## 时点系统（攻击费用相关）

- Boss 行动卡的 `timePointCost` → `TimelineManager.RoundLimit`（每回合玩家行动上限）
- 一次行动超出上限 → 行动照常结算，`TimelineActionStatus.Exhausted`，并立即结束该猎人本轮行动
- 所有角色 Done → 自动切换 Boss 回合
- 溢出惩罚：超出部分在下回合起始 TP 为负数（允许负值，不截断）
- 负结转绝对值大于新上限 → `TimelineActionStatus.Overtime`，不能正常行动
- 另一猎人可通过 `TryAssistOvertimeCharacter` 消耗 1 意志，使目标 TP +1；目标回到新上限边界后恢复 Ready
- 顺序约束：TP 严格最高的角色不可行动；最后一个活跃角色无此限制

## 猎人行动卡费用与结算队列

- `CharacterActionCardData.costs` 集中配置行动卡费用；旧资产未配置列表时继续将 `timePointCost` 映射为时点费用。
- 运行时费用统一映射为 `ActionCardCostDefinition`，当前支持时点、战斗灵感、意志和“翻转其他卡牌”等特殊费用。
- `ActionCardCostTransaction` 在全部资源与特殊选择重新校验通过后一次提交；取消选择或目标失效不会执行卡牌效果，也不会留下部分支付。
- `PlayableCombatSession` 是单场战斗专属管理器，持有角色、Boss、棋盘、卡牌、回合与组合生命周期；它不自行解释队列协议。
- `PlayableCombatActionSession` 独占 Combat `ActionEnvironment`，把出牌、主动恢复、爆发、玩家回合开始和 Boss 回合转换为根 `GameAction`。费用提交、效果 child、卡牌状态和事实 outbox 在同一因果链中顺序结算，并开放 Reactor/Gate 注入。
- GameCore 只保存确定性状态和规则，不再定义战斗专用 ActionQueue 或异步 runner。攻击与移动效果的权威入口由 typed `GameAction` 调度，不得从 Resolver 以 fire-and-forget 越过未完成步骤。
- 基础行动卡仍是 `CharacterConfigSO.startingCards` 配置产生的普通行动卡。包含意志费用的卡自动按意志行动处理：不可爆发、每个玩家回合重置可用状态，但已消耗意志不自动恢复。

---

## 异步约定

- GameCore 不引用 UniTask；规则保持同步、确定性，通过返回值/状态表达结果。
- 等待玩家输入、动画和 Unity 生命周期属于 Adapter / ViewLayer，可继续使用 UniTask。
- 角色行动卡和 Boss 行动统一由 Combat `ActionEnvironment` 顺序等待 typed `GameAction`；兼容同步入口不得用于推进权威结算。
- `CombatTurnFlow` 在 Boss 回合持有完成门禁；只有全部 Boss 效果完成并发出信号后才能返回玩家回合
- 攻击管线（AttackPipeline）全程 `async UniTask`
- `RequestSelectTile` 返回 `UniTask<Vector2Int?>`，null = 右键取消
- `PlayShuffleAndReveal` 的延迟目前是硬编码占位，后续替换为动画完成信号

---

## BossCardTable 视觉逻辑

- `Setup(IBossState, IReadOnlyList<HitLocationRuntimeState>)` — 初始化，横向排布所有部位卡（背面朝上）
- 每张卡由内嵌类 `BodyPartCardView` 管理，三种视觉状态：
  - **背面**（IsFaceUp=false, IsDestroyed=false）：深红色，无文字
  - **正面**（IsFaceUp=true）：米黄色，显示名称/韧性/当前HP
  - **摧毁**（IsDestroyed=true）：橙褐色，显示"★摧毁"
- `OnShuffleStarted`：随机重排卡牌位置（Fisher-Yates shuffle）
- 洗牌动画精细化：当前为瞬间重排，后续可改为 DOTween/协程补间
