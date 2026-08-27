# Hunting in Darkness

受 *Kingdom Death: Monster* 启发的回合制卡牌策略游戏原型（Unity 6000.5）。

---

## 快速开始

1. **Unity 版本**：6000.5.9f1（工程锁定版本）
2. 用 Unity Hub 打开 `D:\UnityProjects\ZFramework\UnityProject`
3. 打开场景 `Assets/Scenes/SampleScene.unity`（或已有的主场景）
4. 按 Play，游戏从**营地阶段**启动
5. 按 `F1` 打开开发者模式面板，可一键跳转到任意阶段

> **首次运行**：如果场景中没有配置 `HunterData` / `HexTileData` / `EventData` 等 ScriptableObject，
> 请先执行菜单 `HuntingInDarkness → Generate Test Assets` 生成测试用 SO 资产。

---

## 游戏循环

```
营地建设（Settlement）
  → 选择出发猎人（最多4人）→ 出发狩猎
狩猎阶段（Hunt）
  → 翻开六边形地块 → 采集资源 → 触发事件
  → 遭遇 Boss 地块
Boss决战（BossFight）
  → 回合制卡牌战斗（时点系统 + 部位卡）
  → Boss 击败 → 返回营地（年份推进 + 战利品结算）
全部猎人死亡 → 游戏结束画面（可重新开始）
```

---

## 阶段切换机制

单一场景，三个根物体通过 `SetActive()` 切换：

| 阶段 | 激活根物体 |
|---|---|
| 营地 | `SettlementRoot` + `Canvas/Settlement` |
| 狩猎 | `HuntRoot` + `Canvas/Hunt` |
| Boss决战 | `BossFightRoot` + `Canvas/BossFight` |

`Canvas/Shared` 节点（含开发者面板、游戏结束画面）始终可见。

---

## 目录结构

```
Assets/
├── Scripts/
│   ├── Core/             GameManager, TurnStateMachine, EventBus, SaveLoadSystem
│   ├── Data/             HunterData, ItemData, EventData, HexTileData, SettlementData（SO + 运行时类）
│   ├── Settlement/       SettlementManager, TimelineSystem, EventSystem, InventionSystem, WorkshopSystem, HunterManagementSystem
│   ├── Hunt/             HuntManager, HexMapGenerator, ResourceSystem, HuntEventSystem, HuntMapVisualizer
│   ├── GameplayBase/     BoardManager, CombatSystem, CardSystem, Interfaces
│   ├── UI/
│   │   ├── Settlement/   SettlementUIManager, HunterRosterPanel, ResourcePanel, EventPopup, DeparturePanel…
│   │   ├── Hunt/         HuntUIManager, HunterStatusOverlay, ResourceHarvestPopup
│   │   └── (Shared)      DevModePanel, GameOverScreen
│   └── Editor/           TestAssetGenerator（菜单工具）
└── ScriptableObjects/    运行时生成的测试 SO 资产
```

---

## 开发者模式（F1）

| 按钮 | 功能 |
|---|---|
| ▶ 营地 / 狩猎 / Boss | 直接跳转阶段（绕过正常流程） |
| ✓ Boss已击败 | 触发 `BossDefeatedEvent` → 结算返回营地 |
| + 资源 ×N | 向营地资源存储添加骨/皮/石/器官 |
| + 招募猎人 | 添加一名测试猎人 |
| + 推进1年 | 年份 +1（不触发事件） |
| 💾 保存 / 📂 读档 | 手动存档/读档 |
| 🗑 删除存档 | 清除 `persistentDataPath/settlement_save.json` |

---

## 存档

使用 `JsonUtility` 序列化 `SettlementInstance` 到：

```
Application.persistentDataPath/settlement_save.json
```

进入营地阶段时**自动保存**。

---

## 当前实现范围（M0–M4）

| 系统 | 状态 |
|---|---|
| 阶段状态机（Settlement/Hunt/BossFight） | ✅ 完整 |
| 营地 UI（花名册/资源/发明/工坊/出发） | ✅ 原型完整 |
| 事件系统（抉择/叙事 + 意志重投） | ✅ 原型完整 |
| 发明树 | ✅ 逻辑完整，UI 为线性列表（无连线） |
| 工坊制造 | ✅ 原型完整 |
| 猎人管理（装备/死亡判定） | ✅ 原型完整 |
| 六边形狩猎地图 | ✅ 程序生成，3D 圆柱体占位 |
| 狩猎资源采集（翻牌弹窗） | ✅ 原型完整 |
| 狩猎事件 | ✅ 复用事件系统 |
| Boss决战（时点/行动卡/部位卡） | ✅ 完整（M0前已有） |
| 存档/读档 | ✅ JsonUtility |
| 胜负条件（全员死亡=游戏结束） | ✅ 事件驱动 |
| 开发者面板 | ✅ uGUI，F1 切换 |

---

## 已知限制

- 发明树 UI 为缩进列表，暂无树形连线可视化
- 年鉴（Annals）面板按钮存在但未实现内容
- Boss AI 目标选择为占位符（不选择具体目标）
- 狩猎阶段 3D 地块为圆柱体占位，无正式美术资产
- `SettlementInstance` 中部分字段（`Equipment`、`Collectibles`）标记了 `[NonSerialized]`，存档后需通过 ID 重建引用（目前未实现完整引用重建）
- 持续效果 tick / buff-debuff 倒计时未实现（Boss决战）

---

## 架构速览

→ 详见 **`ARCHITECTURE.md`**
