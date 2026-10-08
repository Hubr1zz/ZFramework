# UI/UX 重设计实施说明

## 正式入口与界面资产

正式游戏入口为 `ProcedureStartGame → GameApp.Entrance → PlayableGameBootstrap.EnsureInstalled`。组合根显式绑定棋盘相机和 screen presentation；战役阶段通过 `CampaignScreenWindow` 展示营地、管理、出发准备与已提交回营摘要，战斗/狩猎各由独立屏幕承载，实体骰子和抽牌共用 `PhysicalInteractionScreenWindow`。

持久化界面位于 `Assets/AssetRaw/UI/Prefabs/`：`CampaignScreen.prefab`、`CombatScreen.prefab`、`HuntScreen.prefab`、`PhysicalInteractionScreen.prefab`；事件和年鉴复用 `TabletopEventPanelScreen.prefab`、`CampLedgerPanelScreen.prefab`。布局、字体、滚动区域、舞台相机、物理锚点及交互尺寸由 Prefab/Inspector 字段调整。1920×1080 为基准，适配 1280×720；main 场景 UIRoot 的 CanvasScaler 实例覆盖为 1920×1080、match 0.5。

本轮一次性安装器、布局捕获脚本，以及主链烟测/战斗按钮辅助脚本均已删除；正式 Prefab 和运行时代码保留。请直接在 Prefab、场景和 Inspector 中维护界面，勿重新运行旧生成器覆盖人工编辑。

## 玩法边界

界面只读取现有权威模型、资格、费用、随机结果和事务，并调用原有命令；没有新增玩法或存档字段。物理事件骰稳定后仍按既有朝向取值，拖动只触发投掷；战斗专注骰只展示既有随机结果。卡牌只揭示当前所选结果，未知牌背保持一致。采集、撤退、回营和强制成长/事件决策继续遵循现有事务顺序。战斗没有接入的地形、投石与击退功能未加入。

回营摘要读取提交后的 HuntRecord/返回记录；成长、熟练度和新增永久损伤只在本次运行的出发快照可用时显示差异，恢复存档缺少基线时展示当前状态，不推测变化。

## 验证记录

最终正式入口烟测 **1/1 Passed（2026-09-28 06:03:10 UTC）**。它使用真实 UI 按钮覆盖营地、队伍与目的地选择、出发、狩猎、撤退、回营确认及一次战斗行动。烟测在回营后通过正式阶段 API 进入战斗，用于验证战斗屏幕与一次按钮提交；这不是由地图遭遇触发的完整 Boss 胜利流程。正式烟测的营地、出发、狩猎、回营和战斗选卡截图位于 `D:/UnityProjects/ZFramework/Temp/UiUxRedesignScreenshots/`，包括 `campaign-1280x720.png`、`departure-1280x720.png`、`hunt-1280x720.png`、`return-1280x720.png` 和 `combat-card-preview-1280x720.png`；对应 1920×1080 图也已生成。骰盘、死亡牌堆与翻开牌的 `PhysicalInteractionScreen_*` 截图来自布局捕获 fixture，不应视作运行态结算测试。

Campaign/事件 PlayMode 回归 **4/4 Passed**，包含真实出猎回营循环和事件面板暂时隐藏、恢复输入及重复点击保护。一次性主链烟测与其战斗按钮 helper 已删除；保留的回归测试为 `PhysicalInteractionScreenPrefabTests` 与 `TabletopEventPanelLifecyclePlayModeTests`。

最终 EditMode 测试 **70/70 Passed（2026-09-28 06:07:49 UTC）**。三份测试 XML 与日志位于仓库根目录 `D:/UnityProjects/ZFramework/Temp/`：`uiux-screen-smoke.xml` / `.log`、`uiux-playmode-final.xml` / `.log`、`uiux-rules-final.xml` / `.log`。EditMode 结果包含原有 38 项及 Boss 行动流程、猎人死亡后果、战斗灵感、部位结算、死亡牌堆和事件重投规则测试。目前没有对完整多部位逐张分配、Boss 偏移决策、致命伤重投的所有交互组合做人工全流程覆盖。
