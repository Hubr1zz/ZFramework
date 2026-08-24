# Hunting in Darkness — 架构文档

> 本文保留旧场景与命名空间映射，不能作为新生命周期设计的权威。当前迁移目标以 `project-gamecore/references/GAMECORE.md` 为准：ZFramework Singleton System 持有跨阶段 runtime/lifecycle，`GameManager` 逐步收缩为 Unity 组合根与序列化/View 边界。较大拆分只进行一次完整职责分析后实施。

> 规则层 / Unity 适配层 / 表现层的当前边界与扩展规范见 `.agents/skills/project-gamecore/references/GAMECORE.md`。

---

## 场景结构（单一主场景 MainScene）

```
MainScene
├── GameManager                ← 迁移期兼容组合根；目标仅转发 Unity 生命周期并装配 ZFramework System
├── SettlementRoot             ← 营地建设阶段的3D内容根节点（默认 active）
├── HuntRoot                   ← 狩猎阶段的3D内容根节点（默认 inactive）
├── BossFightRoot              ← Boss决战阶段的3D内容根节点（默认 inactive）
│   ├── Board                  ← 运行时生成的六边形棋盘
│   ├── Entities               ← 运行时生成的角色/Boss胶囊体
│   └── CardUI                 ← 运行时生成的卡牌展台3D物体
└── MainCanvas (Screen Space Overlay)
    ├── Settlement/            ← 营地阶段 uGUI
    ├── Hunt/                  ← 狩猎阶段 uGUI
    ├── BossFight/             ← Boss决战阶段 uGUI
    └── Shared/                ← 跨阶段通用 UI（开发者面板、全局通知等）
```

阶段切换通过 `GameManager.TransitionToPhase(GamePhase)` 触发，
内部由 `PhaseManager` 维护状态并回调 `ApplyPhaseRoots()` 做 Enable/Disable。

---

## 游戏循环

```
Settlement（营地建设）
    ↓ 出发狩猎
Hunt（狩猎阶段）
    ↓ 遭遇Boss / 开发者直跳
BossFight（Boss决战）
    ↓ 战斗结算
Settlement（下一年）
```

---

## 命名空间 / 目录约定

| 命名空间 | 目录 | 说明 |
|---|---|---|
| `HuntingInDarkness.GameCore.*` | `Scripts/GameCore/` | 纯 C# 规则与运行时状态；独立 asmdef，禁止 Unity 引用 |
| 兼容命名空间（`Core` / `GameplayBase`） | `Scripts/Adapters/Unity/` | SO、EventBus、UniTask、Unity 坐标与旧 API 的适配器 |
| 兼容表现命名空间（`UI*` / `GameplayBase*`） | `Scripts/ViewLayer/` | 输入、uGUI、3D 世界表现与相机（保留旧命名空间） |
| `Core` | `Scripts/Core/` | GameManager, PhaseManager, EventBus 等组合根与跨阶段基础设施 |
| `GameplayBase` | `Scripts/GameplayBase/` | 接口, GameEnums, Board, CombatSystem, Config |
| `HuntingInDarkness.Data` | `Scripts/Adapters/Unity/Data/` | ScriptableObject 模板与 JsonUtility 兼容包装 |
| `HuntingInDarkness.Settlement` | `Scripts/Adapters/Unity/Settlement/Legacy/` | 营地 Unity 适配与流程协调 |
| `HuntingInDarkness.Hunt` | `Scripts/Adapters/Unity/Hunt/Legacy/` | 狩猎 Unity 适配与流程协调 |
| `UI` | `Scripts/UI/` | 已有 Boss决战 UI（CardDisplayManager, BossCardTable 等） |
| `UI.Settlement` | `Scripts/ViewLayer/Settlement/UI/` | 营地阶段 uGUI |
| `UI.Hunt` | `Scripts/ViewLayer/Hunt/UI/` | 狩猎阶段 uGUI |

---

## 核心类职责

### `GameManager`（迁移期 MonoBehaviour 组合根）
- **当前兼容职责**：PhaseManager、阶段 Manager、序列化场景引用和阶段根物体切换。
- **目标职责**：仅转发 Unity 回调、持有 Inspector 引用并装配 ZFramework System/runtime host。
- **迁移规则**：持久化、阶段状态机、Action session 与跨阶段业务规则必须一次性按宏观职责迁出；在正式拆分前不继续向 GameManager 增加新业务职责，也不做反复的局部微重构。

### `PhaseManager` (纯 C#)
- 维护 `GamePhase` 状态机（Settlement / Hunt / BossFight）
- 通过 `OnPhaseTransition` 回调通知 GameManager
- 通过 `EventBus` 发布 `GamePhaseChangedEvent`

### `SettlementManager`（Unity Adapter）
- 协调 Timeline、事件系统、发明系统、工坊系统，并翻译 EventBus/日志。
- 权威资源、猎人、出发、事件判定、发明和制造规则位于 `GameCore.Settlement`。

### `HuntManager`（Unity Adapter）
- 协调 SO 地块映射、资源表现、狩猎事件和 Boss 遭遇。
- 权威地图、导航、翻图、采集与事件概率规则位于 `GameCore.Hunt`。

### `TurnStateMachine` / `TimelineManager`（Unity 适配器）
- 位于 `Adapters/Unity/Combat/`，保持现有 GameManager API 与 EventBus 事件。
- 权威回合阶段和时点规则分别委托给 GameCore 的 `CombatTurnFlow` / `TimelineService`。
- 仅在 BossFight 阶段运行。

---

## 数据层设计原则

- **ScriptableObject** = 策划配置/模板（发明树节点、事件模板、地块配置、猎人模板）
- **运行时实例（普通 C# class）** = 游戏状态（HunterInstance, SettlementInstance, HexTileInstance）
- **存档** = `JsonUtility.ToJson(SettlementInstance)` → `Application.persistentDataPath`
- **随机** = GameCore 统一依赖 `IRandomSource`；默认实现基于 `System.Random`（可 seed），禁止规则层使用 `UnityEngine.Random`

---

## ScriptableObjects 文件夹

```
Assets/ScriptableObjects/
├── Settlement/     ← 发明树节点 .asset
├── Hunt/           ← 地块配置 .asset
├── Events/         ← 事件模板 .asset
├── Hunters/        ← 猎人模板 .asset
└── （已有）BossConfig.asset, CharacterConfig/, ...
```

---

## Prefabs 文件夹

```
Assets/Prefabs/
├── Settlement/     ← 猎人条目 Prefab、发明节点 Button Prefab
├── Hunt/           ← 地块 Prefab（3D六边形卡片）、资源点 Prefab、猎人 Token Prefab
└── Shared/         ← 事件弹窗 Prefab、通用弹窗 Prefab
```

---

## EventBus 事件一览（HuntingInDarkness新增）

| 事件 | 触发时机 |
|---|---|
| `GamePhaseChangedEvent` | 阶段切换时 |
| `YearAdvancedEvent` | 年份推进时 |
| `GameEventTriggeredEvent` | 叙事/抉择/战斗事件触发时 |
| `HunterRosterChangedEvent` | 猎人招募/死亡时 |
| `ResourceChangedEvent` | 资源存储变化时 |
| `HuntDepartedEvent` | 猎人小队出发时 |
| `HuntCompletedEvent` | 狩猎阶段结束时 |
| `BossDefeatedEvent` | Boss被击败时（也可由开发者面板手动触发） |
| `GameOverEvent` | 全部猎人死亡时 |

---

## 胜负条件

- **游戏结束**：`HunterManagementSystem.KillHunter()` → `HunterRosterChangedEvent`
  → `GameManager.OnHunterRosterChanged()` 检查存活数为0 → `GameOverEvent` → `GameOverScreen`
- **Boss击败**：`BossDefeatedEvent` → `GameManager.OnBossDefeated()`
  → `HuntManager.CompleteHunt(bossDefeated:true)` → 年份推进 + 战利品结算 → 返回 Settlement

---

## 存档系统

- 类：`Core.SaveLoadSystem`（静态工具类）
- 路径：`Application.persistentDataPath/settlement_save.json`
- 格式：`JsonUtility.ToJson(SettlementInstance, prettyPrint:true)`
- 触发：进入 Settlement 阶段时自动保存；开发者面板可手动操作
- **限制**：`[System.NonSerialized]` 字段（装备对象引用等）不被序列化，
  仅保留 InstanceId 列表，读档后需重建引用（当前版本未完整实现）

---

## 已知约束

- **单一场景**：禁止 `SceneManager.LoadScene`，阶段切换仅 Enable/Disable 根物体
- **UI 框架**：全部使用 uGUI（UnityEngine.UI），不使用 UI Toolkit
- **开发者模式**：`GameManager.devMode = true` + `devStartPhase` 可跳过营地直接进 Boss决战
- **Boss决战** 系统（TurnStateMachine, TimelineManager, BossController 等）不修改，仅归入 BossFightRoot
