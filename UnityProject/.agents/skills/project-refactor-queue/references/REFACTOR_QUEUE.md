# REFACTOR_QUEUE — 增量重构工作台

> 仅在增量重构、处理技术债或打开重构工作台时读取。普通功能开发、设计与问答不得把本文件作为默认消息来源。

---

## 📋 待处理队列

> 来源：重构总结的「⏸ 注释暂留」和「❌ 建议」条目，由 agent 自动写入。用户也可手动添加。
> **agent 写入规则**：先搜索是否已有相同文件+类型的条目，有则更新描述，无则新增。

### [优先级: 低] WeaponEffect 归类/设计问题
- **文件**: `Assets/GameScripts/GameLogic/HuntingInDarkness/SO/Weapon/Weapon Effects/WeaponEffect.cs`
- **类型**: 职责 / 归类
- **描述**: 空 MonoBehaviour 占位，却放在 SO/ 下，且被 `WeaponData`(ScriptableObject) 以 `public WeaponEffect effect;` 引用（SO 引用场景 MonoBehaviour 本身可疑）。两者均为预留未实现。结构整理阶段未删（删除会破坏 WeaponData 编译/序列化）。待实现武器效果系统时一并设计正确位置与类型。
- **来源**: 文件结构整理任务
- **状态**: 待处理
- **维护人**: Hubr1zz
- **维护时间**: 2026-07-27 15:13:46
- **维护备注**: -

### [优先级: 中] Hunt UI + GameOverScreen 自建 → manager/creator 初始化
- **文件**: `UI/Hunt/{HuntUIManager,EventPopupHunt,HunterStatusOverlay,ResourceHarvestPopup}.cs`、`UI/GameOverScreen.cs`（及 `GameManager` 对 GameOverScreen 的 AddComponent）
- **类型**: 职责 / 可配置化
- **描述**: Settlement 域已按「生成者初始化生成物 / 高级 manager 初始化子面板」范式改完。狩猎资源点的正常高频路径现已使用世界空间 3D 翻牌，`ResourceHarvestPopup` 只保留无 3D 锚点时的兼容回退；但 `HuntUIManager`、顶部/底部栏、HunterStatusOverlay、EventPopupHunt 和回退 Popup 仍由运行时代码自建，`GameOverScreen` 也由 GameManager AddComponent。后续应把低频流程 UI 场景化或 Prefab 化，并继续评估事件展示是否应迁为世界空间事件卡，而不是把回退 Popup 再扩展成第二套主表现。
- **来源**: 重构任务（用户选择「先转 Settlement 两个」，Hunt 组暂缓）
- **状态**: 已维护
- **维护人**: Hubr1zz
- **维护时间**: 2026-07-27 15:13:45
- **维护备注**: 2026-08-20 资源采集主路径已迁入 3D；同日常规 Hunt 顶栏、猎人状态与流程引导迁入地图边缘实体状态桌，旧 uGUI 仅保留无 `HuntMapVisualizer` 的兼容宿主。本条后续范围收缩为兼容回退与 GameOverScreen。

### [优先级: 中] HuntManager 单赋值回调限制多 Presenter 扩展
- **文件**: `Assets/GameScripts/GameLogic/HuntingInDarkness/Adapters/Unity/Hunt/Legacy/HuntManager.cs`、`Assets/GameScripts/GameLogic/HuntingInDarkness/ViewLayer/Hunt/UI/HuntUIManager.cs`
- **类型**: 架构边界 / 事件扩展性
- **描述**: `OnResourcePointClicked` 等公开 `Action` 字段由 View 直接覆盖，当前只能安全容纳一个订阅者；后续加入事件覆盖、教程注入或多个 3D Presenter 时容易互相替换。应在完整梳理 Hunt 命令与已提交事实后，将输入请求收口为明确 Command Port，将多消费者刷新迁入 ActionQueue outbox + EventBus 的类型化已提交事件；迁移期间保留兼容桥，不并存两套权威写入路径。
- **来源**: 2026-08-20 狩猎 3D 状态桌实现审查
- **状态**: 待处理
- **维护人**: codex
- **维护时间**: 2026-08-20
- **维护备注**: 影响后续功能覆盖与流程注入；需要结合 Hunt runner/reactor 宏观职责一次性设计，暂不做局部反复重构。

### [优先级: 中] 统一配置 TMP 中文字体与 fallback
- **文件**: `Assets/` 下使用 TextMeshPro 的营地、事件与桌面表现组件；项目 TMP Settings / 字体资产
- **类型**: 表现稳定性 / 全局资源配置
- **描述**: Unity MCP 运行验证中，默认 LiberationSans SDF 无法覆盖中文，产生缺字方框并持续刷出 Missing Character 警告。应集中建立覆盖项目常用中文字形的 TMP 字体资产，并在 TMP Settings 配置全局 fallback；各 View 只引用统一字体配置，避免逐标签修补。实体骰子已支持通过序列化字段指定结果字体，但不应成为全局问题的局部替代。
- **来源**: 2026-08-20 实体桌面随机交互运行验证
- **状态**: 待处理
- **维护人**: codex
- **维护时间**: 2026-08-20
- **维护备注**: 不阻塞本阶段流程；后续统一视觉资源阶段处理。

### [优先级: 中] 工坊建设迁入 3D 桌面与 Settlement ActionQueue
- **文件**: `ViewLayer/Settlement/PlayableWorkshopConstructionView.cs`、`Adapters/Unity/Settlement/PlayableWorkshopConstructionService.cs`
- **类型**: 交互一致性 / 流程编排
- **描述**: 工坊制作已由 3D 工坊卡进入 Settlement ActionQueue，但建设入口仍使用屏幕空间 IMGUI，并直接调用同步 Service 修改营地状态。后续应把可建工坊表现为桌面建筑蓝图卡，由 Adapter 建造 Action 原子消费资源、提交 BuiltWorkshops 并发布事务事实；保留现有 Service 作为规则映射或兼容入口，避免再次出现两套权威提交路径。
- **来源**: 2026-08-20 营地 3D 工坊制作闭环
- **状态**: 已维护
- **维护人**: codex
- **维护时间**: 2026-08-20
- **维护备注**: 2026-08-20 已由 `efc3aba` 完成：未建工坊使用 3D 蓝图卡和世界空间确认板，权威建设进入 Settlement ActionQueue；建成后刷新工坊制作入口。

### [优先级: 低] 移除已退出正常路径的营地 IMGUI Toast
- **文件**: `ViewLayer/Settlement/Playable{GrowthMilestone,HunterLoss,WeaponMastery}Toast.cs`
- **类型**: 兼容代码 / 清理
- **描述**: 正常启动路径已统一使用 `SettlementNoticePresenter3D`，三套旧 IMGUI Toast 不再创建。后续确认没有测试场景、Prefab 或外部程序集按类型挂载后，可一次删除旧脚本及其 meta，避免误配置造成 2D/3D 重复反馈。
- **来源**: 2026-08-20 营地实体消息桌实现审查
- **状态**: 待处理
- **维护人**: codex
- **维护时间**: 2026-08-20
- **维护备注**: 当前保留只为序列化兼容；不要重新接入正式组合根。

---

## 🕐 近期改动模块

> 历史兼容区，不再自动读取或更新。需要判断近期改动时使用 Git diff/log；后续模板不再创建本区块。
> **agent 写入规则**：按「功能模块」字段去重——先搜索是否已有相同功能模块的条目。
> - 已有 → 更新日期、状态、备注（追加本次进展）；不新增条目
> - 没有 → 新增条目
> 保留最近 5 个功能模块，超出时移除最旧的一条。

### [2026-07-14] 战斗核心与 Unity 引擎解耦
- **功能模块**: `GameCoreArchitecture`
- **涉及文件**: `GameCore/{Foundation,Combat,Board,Cards,Settlement,Hunt}/`；`Adapters/Unity/{Combat,Board,Data,Settlement,Hunt,Persistence}/`；`ViewLayer/{Combat,Settlement,Hunt}/`；`Core/GameManager.cs`；更新项目入口与 `.claude/{GAMECORE,SETTLEMENT_HUNT,ARCHITECTURE,COMBAT}.md`
- **改动类型**: 架构重构（已完成）
- **备注**: 建立 GameCore → Adapters → ViewLayer 单向依赖。GameCore asmdef 启用 `noEngineReferences`；除战斗/棋盘外，追加迁移营地资源、猎人、事件骰、出发、发明/制造，以及狩猎地图、导航、翻图、采集、事件概率规则。SO、JsonUtility、UniTask、EventBus、Vector2Int 和公共命名空间由适配器桥接；营地/狩猎 UI 与世界表现迁入 ViewLayer，全部移动保留 GUID；测试内容未迁移。

### [2026-07-22] Claude Code / Codex 共享工作流适配
- **功能模块**: `AgentWorkflow`
- **涉及文件**: `AGENTS.md`、`AGENT_WORKFLOW_README.md`、`WORKFLOW_QUICKSTART.md`、`.agents/{agent-roles,skills}/`、`.claude/{agents,commands,skills}/`、`.codex/agents/`、`openspec/{spec-metadata,design-imports}/`、`Assets/Scripts/Editor/AgentWorkbenchWindow.cs`、`zWorkFlow/`
- **改动类型**: 配置维护（已完成）
- **备注**: 保留 `.agents/` 为共享完整源，Claude/Codex 使用薄壳。本轮增加模型分层、显式设计文档转 Spec、结构化 gap/dependency 与 apply readiness 门禁；Python 仅作可选加速器。Unity 工作台已改为项目无关的 `AgentWorkflow.Editor`，由主工具栏 Agent 按钮进入；设计导入配置与导入报告拆页，报告按批次和单条 Spec 展示 `spec-review.json`、verification、Gaps、Dependencies，并允许无未解决 Gap 的 Spec 进入 OpenSpec Changes 等待实现。工作流拆为独立项目包、独立文档包和可选 proposal-only 中间层。本次继续把增量维护按状态拆页并展示维护备注，把 OpenSpec 拆为正式 Spec、关系图谱与 Changes 三页；设计文档同步设置移入顶部工具栏右侧自动同步灯旁，Root 路径可点击打开资源管理器，Spec 正文随窗口自动换行。随后把正式 Spec 固定为架构、Feature 实现、游戏规则三类，加入分类依赖矩阵与逐 Spec 依赖树；setup 会扫描代码和架构文档生成架构基线，不再显示独立架构规范面板。导入报告与 OpenSpec Change 统一持久化目录，审批时整体移动；Gap 收敛为缺失依赖项，Changes 改为左右分栏并可视化追踪 Tasks。本次进一步把 setup 改为旁路适配：分发包不再携带具体框架、具体项目领域和来源项目记录；架构资料与 Agent 工作流拆成独立检测流程。setup 对已有工作流只读，通过能力映射、来源指纹和精简路由摘要复用同类能力、跳过重复步骤并降低后续 token 消耗。关系图谱采用 Unity IMGUI/Handles，无额外包依赖；架构内环、Feature 外环，支持节点拖动、画布平移和滚轮缩放。缩放同步影响节点、文字、边框与箭头；关系图谱页移除垂直滚动条，画布高度随窗口调整且设上限，下方保留固定节点信息区，窗口最小尺寸为 760×600。Markdown 标题、副标题和正文采用不同主题色；增量维护与导入报告信息改为双列。顶部工具栏保持默认高度；三大主入口统一为 40px，内部页签统一为 32px，均通过固定 GUIStyle 高度避免状态间漂移。本次补充同步设置指令列表、Draft Change 修改入口、代码证据跳转高亮、逐级标题配色与 Requirement 卡片；Gap 原始 ID/状态保留在数据层，视图改为可读中文。

  - **后续 UI 修复**: 证据路径兼容反引号、行号及项目相对路径；点击只在 Unity Project 窗口中定位高亮，圆点与路径使用同一控件避免断行。导入批次与 Spec 提案改为左侧单栏两级导航，为详情区释放水平空间。
  - **工作台配置与 Spec 浏览**: 正式 Spec 改为左侧紧凑列表、右侧固定元数据与内部滚动预览；打开文件按钮移入详情。新增按 capability ID 持久化的自定义二级文件夹，以及独立 `openspec/workbench-config.json`；工作台文案按 ID 从本地化表读取，支持中英文切换，H1-H6 深浅主题色与默认窗口尺寸均由配置驱动。
  - **Spec 编辑与本地偏好**: Spec 列表压缩为 29px 单行条目，名称居中、一级类别靠右；右键可在保持 capability 不变的前提下重命名显示标题。空二级文件夹可删除；详情内置原 Markdown 编辑/保存，并在切换 Spec、页签或工作台功能前处理未保存内容。工作台偏好、设计源绝对路径与成员个人规则由 `.gitignore` 排除；主题跟随 Unity 深浅模式，原生按钮与面板自动换色。
  - **人类文档、跨平台 setup 与稳定代码证据**: setup 会幂等确保项目根 `.gitignore` 存在并合并 zWorkFlow 本地偏好规则，同时分发最新 `WORKFLOW_OVERVIEW.md` 与 `WORKFLOW_QUICKSTART.md`。工作台标题栏新增“介绍”，在独立面板中用同一 Markdown 渲染器切换阅读版本介绍和使用指南。代码证据升级为 schema-v3 `Unity GUID + 显示路径 + 行号`，跳转只按 GUID 反查资源，兼容旧路径证据并已迁移当前可解析记录。新增 macOS 目录打开分支与 Unity 6 宏保护，禁用高版本不安全的内部主工具栏反射；setup 增加完整功能覆盖清单并重建最新分发压缩包。
  - **主题、布局与文档反向跳转**: 工作台主题改为完全独立的浅色/深色背景与字体方案，不再用全局 `GUI.contentColor` 污染警告文案；H1-H6 与 Markdown 正文均可配置。统一修正 Spec/导入字段基线、Change 进度居中及 Change 文件夹创建/分配；修复图谱跳转并移除图谱原 MD 入口。bridge 启用时，玩法规则 Spec 可反向打开来源设计 Markdown，且配置支持指定 Obsidian 等默认应用。
  - **OpenSpec 指令入口**: 指令面板补齐 `apply <change-id>`、`sync specs <change-id>`、`archive <change-id>`，Changes 详情支持一键复制 ID；浅色背景收敛到更接近 Unity 默认控件的中性灰。
  - **zWorkFlow 路由与工作台主题配置**: Intake Gate 收敛为非平凡游戏内容与运行框架改动；引擎/工具、Agent 工作流、纯重构维护和小型改动默认不进入，用户显式标注“框架”时必须进入。工作台配置 schema 升级为 v3，深色/浅色各新增“工作台正文”和“工作台背景”颜色，旧配置自动补默认值，当前项目与 setup 分发模板同步。
  - **Change 大区块折叠与标题强调**: 导入报告的 Draft Change 与 OpenSpec 正式 Change 共用折叠区块组件；Review、Dependencies、Tasks、Proposal、Design 默认展开、可分别收起，状态按 Change/区块在当前窗口会话内隔离。标题栏增加强调底色、主题色边线、14px 粗体和展开箭头；当前项目、setup 分发模板及人类文档已同步。
  - **工作台面板、提示文字与窗口状态**: 配置 schema 升级为 v5，深色/浅色新增工作台面板颜色；Unity HelpBox 与自定义警告文字统一跟随工作台正文颜色。Draft Change 审核问题/Dependencies 固定为 66%/34% 双栏。窗口位置和偏好尺寸在停止调整 0.5 秒后写入本地配置、关闭时兜底保存，并在下次打开时恢复；当前项目与 setup 模板同步。
  - **多 AI 工具与团队适配**: setup 新增 schema-v1 声明式适配器注册表，正式覆盖 Codex、Claude Code、Cursor、GitHub Copilot、Gemini CLI、Windsurf 与 Kimi Code CLI。完整 Skills 继续只维护在 `.agents/skills/`，Gemini 使用薄 `GEMINI.md`，Kimi 直接使用 `AGENTS.md` 与 `.agents/skills/`。仓库可并行保留多种工具适配；每位成员的当前工具和版本按昵称写入 Git 忽略的 `.agent-memory/zworkflow/local/tool-selections/`，不保存团队级唯一 active tool。
  - **多路径设计文档来源**: `openspec/design-source.json` 升级为 schema v2 `sources[]`，支持工作台添加、替换和移除多个等价路径；旧单路径自动迁移为 `primary`。路径不预设规则/内容/美术角色，导入先跨全部路径按 scope 收敛，再逐句语义过滤；`sourceId::relativePath:line`、`sources.json.sourceId` 与重复预检共同解决跨路径同名文件。扫描脚本支持重复 `--source [ID=]PATH`，当前 Unity Editor 工程编译及双路径同名文件测试通过。
  - **共享功能归位与 Claude 薄入口**: 增量维护队列的唯一权威源迁至 `.agents/skills/project-refactor-queue/references/REFACTOR_QUEUE.md`，工作台同步改读写该路径；架构、战斗、GameCore、营地/狩猎、重构规范、工作流、反思和项目说明归入各自 Skill references。`.claude/` 根目录对应文件全部收敛为薄转接口，不再保存完整功能正文或共享状态。

### [2026-06-23] Settlement UI 初始化范式重构 + 出发流程改造
- **功能模块**: `SettlementUI`
- **涉及文件**: `UI/Settlement/{SettlementUIManager,EventPopup,EventOptionButton,DepartureConfirmWindow}.cs`、`UI/SettlementTable/{SettlementTable3D,SquadZone,DepartureCard}.cs`、`Core/GameManager.cs`；删除 `UI/Settlement/{DeparturePanel,HunterSelectRow}.cs`
- **改动类型**: 重构 + 新功能（已完成）
- **备注**: ①范式：「生成者初始化生成物 / 高级 manager 初始化子面板」。SettlementUIManager 全场景驱动（删 BuildUI，骨架 [SerializeField]+TMP，Init 逐项校验报错，GameManager 改 [SerializeField] 场景引用+一次性 Init 守卫，DevLoad 不销毁常驻 HUD）；EventPopup 删 Awake→BuildLayout（骨架 [SerializeField]，选项按钮=EventOptionButton 模板 Bind）；删 CreateText/FullStretch 死代码。②出发流程改造：废弃 DeparturePanel(2D 选人)+HunterSelectRow，改为 SettlementTable3D 上 SquadZone（4 槽 SlotGrid 拖入 HunterCard3D 组队）+ 固定 DepartureCard（点击）→ SettlementTable3D.OnDepartureRequested → SettlementUIManager.ShowDepartureConfirm → DepartureConfirmWindow(2D，显示小队/物资 TODO)→ TryDepart。移除 SettlementUIManager 底部出发按钮。SquadZone/DepartureCard 场景预置无回退。

### [2026-06-23] SettlementTable 分区 presenter 重构
- **功能模块**: `SettlementTable3D`
- **涉及文件**: 新增 `UI/SettlementTable/{HunterZone,ResourceZone,WorkshopZone,InventionZone}.cs`；`UI/SettlementTable/SettlementTable3D.cs`(迁入+重构)；删除 `UI/Settlement/{HunterRosterPanel,ResourcePanel,WorkshopPanel,InventionPanel}.cs`(已死，被 3D 取代)
- **改动类型**: 重构（已完成）
- **备注**: 4 个零引用的旧 uGUI 面板（已被 SettlementTable3D 的 Fill 逻辑取代）重生为四区 presenter：每个 = `[SerializeField] SlotGrid + Fill/Refresh/Clear`；SettlementTable3D 由「自己 Fill 四区」改为「持有 4 个 zone 并委托」，回退路径程序化创建 zone+grid 并 SetRefs(grid)。公共 API（OnHunterClicked/OnInventionEffectRequested/OnWorkshopClicked + Init/Refresh/RefreshCards）与「先设回调再 Init」顺序保持，GameManager 无需改。SettlementTable3D.cs.meta 保留 GUID。命名空间 UI（与 SettlementTable3D 一致）。
  - **EntityCreator 静态化**（单例门面）：`EntityCreator` 加 `static Instance`（Awake 注册/OnDestroy 清空），CreateXxx 全改 `static`（经 Instance 读 Prefab，Instance 为空→程序化回退）。调用方一律 `EntityCreator.CreateXxx(...)` 直接调，不再持引用：zone/table 删除 `_creator` 字段与 SetRefs 的 creator 参数；GameManager 调用改静态、EnsureEntityCreator 基于 Instance 保证存在（保留 entityCreator 字段供 CombatTestBootstrap 注入）。

### [2026-06-28] 角色 Prefab 化（CharacterEntity）+ Boss 战相机交互
- **功能模块**: `CharacterEntity`
- **涉及文件**: `UI/CharacterEntity.cs`(新), `UI/CombatPanelLayout.cs`(新), `UI/EntityCreator.cs`, `Core/GameManager.cs`, `UI/CardDisplayManager.cs`, `UI/CameraController.cs`, `Board/EntityVisualizer.cs`, `Core/EventBus.cs`；删除 `UI/CombatCardPanel3D.cs`
- **改动类型**: 新功能（已完成）
- **备注**: 角色改由 `EntityCreator.CreateCharacterEntity`（Prefab/程序化回退）生成，`CharacterEntity` 持有 头部锚点/TimePointLabel/三区域SlotGrid/面板根 并自订阅 TP/移动/翻牌刷新；CardDisplayManager 改用注册表显隐、不再建角色展台；CombatCardPanel3D 删除，布局逻辑移入 CombatPanelLayout。TP 标签更名 TimePointLabel。相机加 WASD 平移 orbitTarget + 随棋盘大小动态死区（BoardReadyEvent 注入）。本次追加轨道滚轮距离、角色注视改为「移动到角色锚点局部 offset 位置并注视角色-Boss 中点」（offset 在远/近两值间按距离插值，<=0 最远距离使用棋盘直径，X 侧向自动镜像选择最短移动）、Mind/Info ViewPoint 近景及子碰撞体生命周期，相机内部改为 Orbit/CharacterFocus/DetailFocus 轻量状态机。**本期只迁角色**，Boss/组件仍走 EntityVisualizer。占位：思想/装备区内容、组件可视化、Boss prefab 化、狩猎注入。

<!--
格式：
### [YYYY-MM-DD] 任务简述
- **功能模块**: `BossCardTable`        ← 去重 key，同一模块多次会话只保留一条并持续更新
- **涉及文件**: `BossCardTable.cs`, `CombatManager.cs`
- **改动类型**: 新功能（进行中） / 新功能（已完成） / 重构 / 修复
- **备注**: 本次进展说明；多次会话时追加，不覆盖前次内容
-->

---

## 📝 重构决策日志

> 历史兼容区，不再自动读取或追加。架构/功能决策进入 ADR 或 OpenSpec；可复用规则/排障约束进入所属 skill/reference，其余依赖 Git 或任务记录。旧通用记忆目录已停用。

### [2026-06-21] 拆分类一律保留原命名空间
- **操作**: 拆分 CardSystem/BossController/ResourceHarvestPopup/HuntMapVisualizer 时，拆出的类放入独立 .cs 但保持原命名空间（Core / UI.Hunt / HuntingInDarkness.Hunt）
- **原因**: 跨命名空间迁移会引发全库 using 改动，且本环境无法 Unity 编译验证，风险高于收益。分文件已达成「一文件一职责」
- **影响范围**: Core/、UI/Hunt/、Hunt/

### [2026-06-21] 3D 卡牌独立为 Cards3D 领域
- **操作**: 19 个 3D 卡牌类从 namespace UI / CardTest3D 统一迁入 namespace Cards3D（Scripts/Cards3D/{Base,Views}），.cs+.meta 一并移动保 GUID
- **原因**: 3D 卡牌是世界空间物体非 uGUI，且 ResourceCard 等跨三阶段复用，不应埋在 UI/ 或名为 test 的 3DCardTest/ 下。CombatTestSetup 等调试启动器按用户要求留 3DCardTest/，仅更新 using
- **影响范围**: Cards3D/、UI/、3DCardTest/、Core/GameManager、Editor/CardPrefabCreator

### [2026-06-28] AI 工作流文档采用单一共享源
- **操作**: 保留 `.claude/` 为共享文档目录，Claude Code 与 Codex 分别通过 `CLAUDE.md` 和根目录 `AGENTS.md` 进入；共享工作流不再绑定具体模型名。
- **原因**: 避免两套文档漂移，同时保留 Claude Code 现有配置与 Codex 的 `AGENTS.md` 自动发现机制。
- **影响范围**: `AGENTS.md`、`.claude/{WORKFLOW,Reflection,REFACTOR_QUEUE}.md`

### [2026-07-14] GameCore 使用程序集强制引擎隔离
- **操作**: 新建 `HuntingInDarkness.GameCore` asmdef 并启用 `noEngineReferences`；规则状态与算法迁入该程序集，Unity 相关兼容控制器移至 `Adapters/Unity`，输入实现移至 `ViewLayer`。
- **原因**: 仅靠命名空间或“纯 C# 类”约定不能防止 Unity API 回流；独立程序集可在编译期保证规则层不依赖 Unity，并允许未来玩法模块复用与独立模拟。
- **影响范围**: `GameCore/`、`Adapters/Unity/`、`ViewLayer/Combat/`、战斗与棋盘调用链

### [2026-07-23] 工具专属目录只保存适配层
- **操作**: 将 `.claude/` 中的完整共享功能文档与增量维护队列迁入对应 `.agents/skills/<功能>/references/`，并把原文件替换为指向权威源的 Claude Code 薄转接口；Unity 工作台改读写共享队列路径。
- **原因**: 工具专属目录只能承载入口、命令、配置和 wrapper。把共享事实或可变状态放在 `.claude/` 会让其他 AI 工具依赖 Claude 私有目录，并产生多份权威源漂移。
- **影响范围**: `.agents/skills/{project-context,project-combat,project-gamecore,project-refactor-queue,workflow-refactor,workflow-reflection}/`、`.claude/`、`AGENTS.md`、`AgentWorkbenchWindow.cs`、工作流介绍与 setup 分发包

### [2026-07-14] 营地与狩猎复用同一三层边界
- **操作**: 将营地和狩猎的状态/规则迁入 `GameCore.Settlement` 与 `GameCore.Hunt`；SO、存档、事件和坐标桥接移入 `Adapters/Unity`，HUD、3D桌面、地图、相机和输入移入 `ViewLayer`。
- **原因**: 三个阶段需要统一依赖方向，才能在无 Unity 环境下模拟规则、固定随机种子，并避免后续玩法把 SO/Vector/EventBus 重新带回领域层。
- **影响范围**: `GameCore/{Settlement,Hunt}`、`Adapters/Unity/{Data,Settlement,Hunt,Persistence}`、`ViewLayer/{Settlement,Hunt}`

### [2026-07-23] setup 使用声明式薄接口安装清单
- **操作**: 为七类 AI 工具适配器统一增加 `install` 声明；Claude Code、Codex、Gemini CLI 从分发包按缺失文件安装薄接口，Cursor、Copilot、Windsurf、Kimi Code CLI 直接读取共享源且零复制。移除 Kimi 注册表中的 `.kimi-code/skills/` 候选路径。
- **原因**: setup 需要在一次执行中适配团队成员选中的多个工具，同时逐文件避开已有配置；显式安装清单也能阻止工具目录重新演变为完整 Skills 或共享状态副本。
- **影响范围**: `zWorkFlow/setup/adapters/`、`AI_TOOL_ADAPTERS.md`、`SETUP_NEW_PROJECT.md`、版本介绍与快速上手文档

<!--
格式：
### [YYYY-MM-DD] 决策标题
- **操作**: 具体做了什么
- **原因**: 为什么这么做
- **影响范围**: 涉及哪些文件/系统
-->
