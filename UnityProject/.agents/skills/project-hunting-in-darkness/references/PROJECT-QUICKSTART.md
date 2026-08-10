# Hunting in Darkness — 项目速查

Unity 卡牌战术游戏（C# + UniTask）。受 *Kingdom Death: Monster* 启发。
顶层入口：`Assets/Scripts/`。AI 共享能力目录：`.agents/`；`.claude/` 保留兼容文档与 Claude Code 薄入口。

## 命名空间 → 目录

| 命名空间 | 目录 | 说明 |
|---|---|---|
| `HuntingInDarkness.GameCore.*` | `Scripts/GameCore/` | 纯 C# 规则层（独立 asmdef，`noEngineReferences`）：Combat / Board / Cards / Foundation |
| `Core` / `CardTactics.CombatSystem` | `Scripts/Adapters/Unity/Combat/` | 战斗 Unity 适配：CombatManager, TurnStateMachine, TimelineManager, BossController, CardSystem |
| `GameplayBase` / `GameplayBase.Board` | `Scripts/Adapters/Unity/Board/` | GameCore 棋盘到 Vector2Int / Vector3 的 Unity 适配 |
| `GameplayBase.CombatSystem` | `Scripts/ViewLayer/Combat/` | 战斗输入与表现入口（UIPlayerInputProvider，保留旧命名空间兼容） |
| `Core` | `Scripts/Core/` | GameManager, PhaseManager, EventBus 等组合根与跨阶段基础设施 |
| `GameplayBase` | `Scripts/GameplayBase/` | 跨模块上下文、状态接口、GameEnums 与配置入口 |
| `GameplayBase.CombatSystem` | `Scripts/Adapters/Unity/Combat/LegacyShowdown/` | UniTask 攻击管线、SO 映射、效果与兼容运行时类型 |
| `GameplayBase.Config` | `Scripts/GameplayBase/Config/` | CharacterConfigSO, BossConfigSO |
| `GameplayBase.Board` | `Scripts/Adapters/Unity/Board/LegacyContracts/` | IBoardGrid、HexDirection、HexBoardVisualizer 等 Unity 契约/表现 |
| `HuntingInDarkness.Data` | `Scripts/Data/` | SO 模板 + 运行时实例（Hunter, Item, Event, HexTile, Settlement） |
| `HuntingInDarkness.Settlement` | `Scripts/Settlement/` | SettlementManager, TimelineSystem, EventSystem, InventionSystem, WorkshopSystem |
| `HuntingInDarkness.Hunt` | `Scripts/Hunt/` | HuntManager, HexMapGenerator, ResourceSystem, HuntEventSystem |
| `Cards3D` | `Scripts/Cards3D/{Base,Views}/` | 3D 卡牌视图：CardView3D 基类、SlotGrid/CardSlot、各类 *Card3D（世界空间物体，非 uGUI） |
| `UI` | `Scripts/UI/` | CardDisplayManager, CharacterCardTable, BossCardTable, EntityCreator；`SettlementTable/` 子目录=营地 3D 桌面（SettlementTable3D + 四区 presenter HunterZone/ResourceZone/WorkshopZone/InventionZone） |
| `UI.Settlement` | `Scripts/UI/Settlement/` | 营地阶段 uGUI HUD（SettlementUIManager、HunterDetailPanel、DeparturePanel、EventPopup）|
| `UI.Hunt` | `Scripts/UI/Hunt/` | 狩猎阶段 uGUI |

## 架构要点

- `GameManager` — 单例 MonoBehaviour，持有 PhaseManager 和所有子系统，负责阶段根物体 Enable/Disable
- `PhaseManager` — 纯 C#，维护 GamePhase 状态机（Settlement / Hunt / BossFight），通过 EventBus 发布 `GamePhaseChangedEvent`
- `TurnStateMachine` / `TimelineManager` — Unity 适配器；权威回合/时点规则在 `GameCore.Combat`
- `EventBus` — 静态泛型，struct 事件，同步
- 棋盘：六边形 axial 坐标，`IBoardQuery` / `IBoardCommand` 接口隔离
- 数据层：ScriptableObject = 策划配置；运行时 C# class = 游戏状态；JsonUtility 存档

## 战斗系统

→ 详见 `.agents/skills/project-combat/references/COMBAT.md`（攻击流程、部位卡状态模型、EventBus 事件、时点系统、BossCardTable 视觉逻辑）

## 分层规则

→ 详见 `.agents/skills/project-gamecore/references/GAMECORE.md`（GameCore / Adapters / ViewLayer 的依赖方向、数据映射与复用规范）

## 完整架构

→ 详见 `.agents/skills/project-context/references/ARCHITECTURE.md`

## 卡牌 UI

- 3D 物体，TMP 3D 文字（`Euler(90,0,0)` 朝上，适配俯视相机）
- `EntityCreator` 只保存 Prefab 引用并负责实例化，不持有卡牌或卡堆表现参数；卡堆表现参数由 `CardSlot` 自身维护，动态卡堆使用默认值并可通过明确的 public 配置函数覆盖。
- 点击角色 → 显示其 CharacterCardTable；再次点击收起
- BossCardTable：游戏开始铺开所有部位卡（背面朝上），响应洗牌/翻牌/摧毁事件更新视觉

---

## ⚙️ Agent 工作流

### 权限边界

- **项目内**（`Assets/` 及根目录配置文件）：默认完整读写权限，无需询问用户
- **`.agents/` 目录**：可读写，保存共享 Skills、项目事实和增量维护队列
- **工具专属目录**：只保存入口、命令、配置与薄 wrapper
- **项目外**：无任何文件操作权限，执行前必须确认路径在项目根目录内
- **受保护文件**：任务可能修改项目文件时读取 `.agents/skills/project-refactor-queue/references/PROTECTED_FILES.md`；列表内文件不得修改或删除

### 文档路由（开工前按任务读取）

| 任务 | 开工前必读 |
|---|---|
| 实现新功能 | `.agents/skills/workflow-refactor/references/WORKFLOW.md`（Agent 分配 + 流程一 + **新功能实现规范**） |
| 重构 / 优化 / 维护 / 增量重构 | `.agents/skills/workflow-refactor/references/{Optimize,WORKFLOW}.md` |
| 战斗系统相关 | `.agents/skills/project-combat/references/COMBAT.md` |
| 理解整体架构 | `.agents/skills/project-context/references/ARCHITECTURE.md` |
| 沉淀经验 / 优化 skill | `.agents/skills/workflow-reflection/references/Reflection.md` |

> **必须遵守**：上述任务在动手前先读对应文档，不要凭记忆执行。「实现/重构/优化/维护」类指令均以对应文档的流程与规范为准。

## 反思 / Skill 自优化

> **术语约定**：本项目语境中「skill / skills」一律指 `.claude/` 下的 `*.md` 指导文档（如 `WORKFLOW.md`、`Optimize.md`、`Reflection.md`），**不是** Claude Code 或 Codex 原生的 `SKILL.md`。要求「优化/新建 skill」时，操作对象是这些 md 文档。

在每次实质性请求过程中或收尾时，按 `.agents/skills/workflow-reflection/references/Reflection.md` 留意值得沉淀的经验，提炼后更新对应共享 Skill。与现有内容冲突或不确定是否修改时，先与用户确认最终版本再落笔。

## Claude Code / Codex 兼容约定

- Claude Code 以 `.claude/CLAUDE.md` 为入口；Codex 以项目根目录 `AGENTS.md` 为入口。
- 两者共用 `.claude/` 下的架构、战斗、工作流、重构与反思文档，修改共享规则时不要复制出第二套。
- `.claude/settings.local.json` 仅供 Claude Code 使用；Codex 忽略该文件，不影响项目工作流。
