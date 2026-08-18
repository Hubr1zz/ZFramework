# Optional Agent Workbench

工作台是 Unity 项目的标准平台组件。用户明确要求执行完整 setup 后，setup 必须先按 `SETUP_NEW_PROJECT.md` 验证兼容的 OpenSpec CLI；只有验证成功，且检测到 Unity 项目、没有同类界面、目标路径全部未占用时才成套安装。CLI 未通过时工作台整体标记 `blocked-openspec-cli`；非 Unity 项目自动跳过。setup 仍先只读检测是否已有同类界面。

## 已有同类工作台

- 不修改窗口源码、菜单、页签、布局、配置键或持久化格式。
- 在工作流能力映射中记录它能展示和操作的数据。
- 与 zWorkFlow 功能等价时标记 `reuse-existing`，后续跳过安装。
- 只有部分能力时标记 `supplement-only`，但不自动往原窗口添加功能。
- 行为或数据写入冲突时只报告，等待用户决定。

## 没有同类工作台

完整 setup 请求即包含安装许可，可同时从 `setup/assets/AgentWorkbenchWindow*.cs.template` 与 `setup/assets/AgentWorkbenchSupport.cs.template` 创建独立窗口及其配置/本地化支持文件。`AgentWorkbenchWindow` 的主窗口、导入报告、正式 Change、引擎指引、数据访问、翻译显示层和内部模型模板必须成套安装；目标路径必须全部未被占用，并满足：

- 使用项目无关的命名空间、菜单与配置键。
- 不依赖第三方编辑器插件或运行时程序集。
- JSON 缺失或损坏时只显示警告，不影响其他功能。
- Draft 与正式 Change 的审核问题使用同一表格；非阻塞 warning/info 可逐行填写接受备注并勾选，blocking 行禁止接受。
- 外部设计文档只允许打开，不允许编辑。
- 设计来源配置使用 schema v2 `sources[]`，允许添加、替换和移除多个等价路径；每条路径持有稳定唯一 ID，不预先声明规则、内容或美术角色。schema v1 单路径自动迁移为 `primary`。
- 不替换或劫持项目原有菜单、工具栏按钮和工作台。

## 平台与 Unity 版本兼容

- 窗口源码整体受 `UNITY_EDITOR` 保护，不进入 Player 构建。
- 支持 Unity 2022.3 LTS 与 Unity 6 的公开 Editor API；项目建议使用 `.NET Standard 2.1` API Compatibility Level。
- 打开目录时，macOS 与 Windows 使用 `EditorUtility.RevealInFinder`，其他 Editor 平台回退到 `OpenWithDefaultApp`；不得调用 `explorer.exe`、`open`、Shell 或硬编码盘符。
- Unity 2021.2–2023.x 可在版本宏内使用现有主工具栏反射桥。Unity 6 及更高版本通过 `UNITY_6000_0_OR_NEWER` 禁用该内部 API 分支，稳定入口始终保留为 `Tools/Agent Workflow/Agent Workbench`。
- Unity 2021.2 及更高版本使用 `Path.GetRelativePath`；更旧版本使用 URI 相对路径兼容分支。所有持久化路径统一写成项目相对的 `/` 分隔形式。
- 安装后必须至少执行当前 Unity Editor 版本的脚本编译；若无法在 macOS 或 Unity 6 实机运行，只能报告“静态兼容检查通过”，不得声称完成实机验证。

## 可选展示能力

独立工作台可提供：

- OpenSpec：正式 Spec、Changes 分页；正式 Spec 按 System、Feature 实现、游戏规则筛选，legacy Architecture 归入 System。
- 顶部刷新按钮右侧提供轻量关系图谱入口：展示正式 System/Feature Spec、未同步目标及可选的正式 Change 节点，中心节点按被依赖次数确定，System、Feature、Change 分层排布。视口左上角按钮显隐紫色 Change 节点；隐藏期间仍保留 Change 节点坐标，再次显示不会重置布局。Change 通过目标边连接将修改的 Spec。节点正文始终使用当前语言显示名称，稳定 ID 只保留在 tooltip 与依赖键中。类型由主节点颜色与图例表达；状态以主节点下方独立的小型状态柄表达，五种状态分别为：待 Sync、实现中、待合入 Delta、已实现、阻塞。完成实现但尚未 sync 的 Change 为“待 Sync”；未开始、部分完成或含延迟缺口的 Change 为“实现中”；被其他节点妨碍实现的 Change 为“阻塞”；正式 Spec 存在未合入目标时显示“待合入 Delta”，代码核验完成后显示“已实现”。节点视口比旧布局增高 20%，下方信息区压缩并支持纵向滚动；核心实现思路按条目换行。支持节点拖动、画布平移和滚轮缩放，箭头指向被依赖项。
- 关系图谱只依赖 OpenSpec Spec/Change 与依赖 metadata，不依赖 `REFACTOR_QUEUE.md` 的存在、格式或内容。即使队列缺失/为空、正式 Spec 也为空，仍显示图谱标题、图例和空状态界面；队列解析错误只影响增量维护列表。
- 关系图谱按钮右侧提供“工程能力”入口，读取 `.agents/skills/project-tooling/references/tooling-catalog.json`。页面不绘制能力依赖图；左侧使用 Plugin、Architecture、System 类型标签筛选，并在目录声明 `layers` 时额外提供项目分层筛选；右侧显示所选条目的类型、涉及分层和详情，与 OpenSpec 主从布局保持一致。类型表示归属/复用边界，分层表示实现落点，两者互不替代。所有条目允许按当前语言编辑并保存 `usageNotes` / `usageNotesEn` 自由文本；`usagePolicy` 仍是只读机器门禁。Plugin 另允许编辑 `decisionBasis`；Architecture 显示 required/locked 门禁且不允许直接编辑。
- 工程能力目录的名称、描述、能力和约束随工作台语言切换中英文；稳定 ID、路径、版本、依赖和证据保持语言无关。
- Markdown 轻量渲染器为一级标题、副标题（其余标题级别）和正文使用三组不同颜色，并同时适配编辑器深色与浅色主题。
- 顶部使用紧凑的“项目根目录”按钮打开项目根目录，不直接显示机器完整路径。工程能力按钮旁边提供独立的“设计文档树”入口与“指令列表”入口；右侧状态灯只表示至少一个设计文档路径有效。设计文档树顶部只维护来源路径，不再维护单独文档根目录；每个来源成为树的顶层节点，检查时重扫新增/删除 Markdown 并保留用户折叠状态。
- 顶部工具栏按钮保持 Unity 默认高度；“增量维护 / OpenSpec / 导入报告”三大主入口统一为 40px，各功能内部页签统一为 32px。主入口与页签使用统一横向 Toggle 组，不使用会派生首/中/尾内部样式的 `GUILayout.Toolbar`；每个按钮共享相同 GUIStyle、显式高度和等分宽度。
- 增量维护卡片字段使用左右两列；导入报告的批次元数据与单条提案审计也使用双列布局。Draft Change 的审核问题与 Dependencies 使用固定宽度的 66%/34% 双栏，展开内容不得把相邻区块挤出窗口。
- 每条正式 Spec 的递归依赖树、readiness、Verification、缺失依赖和来源。
- 正式 Spec、Changes 与 Draft Change 的全部/System/Feature/游戏规则分类页签都位于各自左侧列表区；OpenSpec 顶部只保留“正式 Spec / Changes”主切换。正式 Spec 与 Change 左侧条目末端提供小型文件夹图标，通过菜单原位设置条目的文件夹分类；顶部只承担筛选、新建与空分类删除。Changes 使用与正式 Spec 相同的分类/文件夹式左侧导航、右侧详情及可视化 Tasks 进度，左侧条目显示完成百分比；详情操作区分行排列，审核问题改用自适应宽度卡片，所有正文和编辑区按右侧可用宽度换行，不得超出窗口。正式 Change 详情必须列出内部全部 delta Spec，配对 Change 在规则页签中优先显示 Game Rule、在 Feature 页签中优先显示 Feature，但始终保持一个 Change。导入报告的 Draft Change 与正式 Change 对 Review、Dependencies、Tasks、内部 Spec、Proposal、Design 等大区块使用同一折叠组件：整条标题可点击，默认展开，折叠状态按 Change 和区块分开保存于当前窗口会话。Tasks、内部 Spec、Proposal、Design 支持在结构化/分段渲染与 Markdown 编辑之间切换并保存。标题栏使用强调底色、主题色边线、粗体大字号和展开箭头。
- `change-review.category=paired` 是合法 Change 聚合分类。工作台从其内部 capability review/frontmatter 汇总三类筛选归属，配对 Change 可同时出现在多个分类页签，展示标签为“配对”，不得显示为“未分类”。
- 设计导入批次只作筛选；capability 按 Draft Change 分组，列表提供与 OpenSpec 一致的全部、System、Feature、游戏规则分类页签，同一 capability 必须原位更新，不生成并行冲突副本。删除操作以整个 Draft Change 为单位确认。
- 导入批次目录只在至少引用一个实际存在的 Draft Change 时保留。工作台在批准、删除和重新加载时同步中央 Draft 索引与 `draft-refs.json`；最后一个引用消失后安全删除该批次目录，不展示空导入记录。
- Feature/Architecture 的 capability review 可包含可选 `editorGuidance`。只有存在 Inspector 引用、集中配置、场景/Prefab 安装或首次使用动作时，Draft、正式 Spec 和正式 Change 详情才显示“引擎配置”按钮；游戏规则与无需人工接入的实现不显示。
- 增量维护队列的待处理、进行中、已维护分类；从工具栏进入关系图谱后，再次点击“增量维护”主入口必须返回待处理列表。
- 增量维护数据只读写 `.agents/skills/project-refactor-queue/references/REFACTOR_QUEUE.md`；`.claude/REFACTOR_QUEUE.md` 即使存在也只能是兼容薄入口。
- 使用介绍允许用标准 Markdown 图片语法显示项目内本地图片。解析不得越出项目根；相对路径按当前文档目录、项目根、`zWorkFlow/` 依次回退，纹理缓存必须在工作台关闭时释放。
- 使用介绍与快速上手的权威路径位于 `zWorkFlow/`。工作台优先读取该目录，只为旧版已安装项目只读回退根目录同名文件；setup 不再生成根目录人类文档副本。
- 正文按窗口宽度换行，不产生水平滚动条。
- 正式 Spec、正式 Change 与 Draft Change 的列表/详情 ScrollView 使用无水平滚动条样式，只保留纵向滚动；Markdown、代码块、审核/依赖文本和编辑区必须使用可用宽度计算换行高度，后续新增区块不得恢复横向滚动。
- Draft 批准时，外部依赖可由正式 Spec 或已批准为 `implementation-change` 的活动 Change 提供；依赖方 apply 时必须重新检查后者 Tasks 全完成、Verification 已验证、`codeReadiness=implemented`、`readiness=ready|implemented` 且无阻塞，否则拒绝。
- 正式 Spec 使用左侧列表、右侧固定详情与内部纵向滚动；自定义文件夹是 System/Feature/规则之外的二级分类，按 capability ID 持久化。
- 正式 Spec 与 Draft Spec 条目在中文、英文界面下拥有可分别重命名的显示名称；列表、依赖树、关系图和可读引用统一使用当前语言名称，稳定 capability ID、路径和依赖键不变。权威语言重命名同步 Spec/review/依赖 label，另一语言只更新 `openspec/localization.json.specTitles`。正式 Change 条目仍通过右键或双击重命名并同步 review 与 Proposal 标题。详情中的“原 MD 文本”提供显式编辑/保存，并在切换 Spec、页签或工作台功能前处理未保存内容。正式 Spec 与 Change 共用的自定义文件夹只有在两边都没有内容时才允许删除，并可从任一正式视图触发。
- 工作台文案按稳定 ID 从独立本地化表读取；当前语言、工作台正文/背景/面板颜色、Markdown H1-H6/正文的深浅主题颜色、最近窗口位置、偏好窗口尺寸、Spec 文件夹分配，以及关系图谱的节点坐标、画布平移和缩放保存在 `openspec/workbench-config.json`。窗口或图谱布局停止变化后自动写入，关闭时兜底保存，下次打开时恢复；图谱新增节点只生成新节点默认位置，不覆盖仍存在节点的手动布局。
- Git 同步的 `openspec/localization.json` 保存以后生成权威文件的默认语言：`source` 跟随设计文档，或明确选择 `zh-CN` / `en-US`；`specTitles` 以 capability ID 为键保存可选 `zhCN` / `enUS` 条目显示名。已有 Spec 的原路径与语言不因配置变化而迁移。
- `openspec/translations/<language>/` 与 `manifest.json` 是团队共享的只读显示副本和块级 hash 索引。工作台始终让 Agent/生命周期操作读取原权威路径；当前界面语言的翻译缺失或 source/translation hash 失效时不渲染正文，只显示同步提示和复制命令按钮。非权威 Markdown 不允许在工作台直接编辑。
- 工作台主题由自身配置中的浅色/深色模式控制，不读取 Unity 编辑器主题；工作台正文、背景、面板、标题与 Markdown 正文颜色读取所选工作台主题对应配置，警告与提示文字使用工作台正文颜色；深色主题的控件 tint 必须让按钮和输入框线框清晰区别于面板背景。
- 配置有效设计来源时，玩法规则 Spec 可按 `sourceReferences` 反向打开对应设计 Markdown；配置允许指定 Obsidian、VS Code 等默认 Markdown 应用，留空则使用系统默认。独立设计文档树的“刷新文档状态”读取项目 `openspec/implementation-ledger.json` 并重算当前指纹，再将设计 Markdown 重建为简化目录树：每个文件显示实现状态/百分比、实现后是否修改和摘要；没有新摘要的外部指纹变化显示“手动修改”。不得把项目路径或工程状态写入设计包，或自动调用设计导入。
- 指令列表列出设计导入与筛选、`检查文档及时性`、修改导入、Spec 翻译/同步、`apply <change-id>`、`sync specs <change-id>` 与 `archive <change-id>`；Change 详情提供复制 ID，并在 Tasks 全完成且 `specSyncStatus=synced` 时启用“归档”按钮。检查文档及时性只刷新实现后设计变更状态和摘要，不触发设计导入。归档按钮只以纯 C# 移动完整 Change 目录到带日期的 archive，不触发 sync；Delete 才是永久移除。OpenSpec 生命周期命令始终定位 Change，不使用 Spec ID。
- 安装时确保 `.gitignore` 排除 `.DS_Store`、Python 缓存、`openspec/workbench-config.json`、`openspec/design-source.json`、`.agent-memory/zworkflow/team/MAINTAINERS.md` 与 `.agent-memory/zworkflow/team/members/`；它们是系统垃圾、生成缓存、个人偏好或机器相关路径。

这些能力只描述新建独立窗口的契约，不构成修改已有界面的授权。

## 数据边界

- Draft Change 与正式 Change 使用相同目录结构，并在导入时立即生成完整工件和 `syncTargets` 正式目标基线；已存在目标还保存 Change 内 `.sync-baseline` 快照。规则与配套 Feature 默认放在同一 Change。EventBus、状态机基类、注册表等普通工具默认只进入实现思路/代码证据；只有项目公共派发或生命周期语义发生变化，或至少两个 capability 依赖稳定契约时，才追加以契约命名的 System capability、节点和真实依赖边。可独立复用的 Architecture 进入工程能力目录。所有类别批准时只整体移动到正式 Change；apply 保持独立沙箱。用户显式 sync 前比较 base/current/Delta，目标创建、删除或修改不自动等同冲突：Requirement 不重叠时保留式合并，重叠、删除或旧基线无快照时进入语义审查，只有确认会覆盖功能或混杂语义才阻止。
- 配对 Feature 是交付主 capability 与唯一批准入口，集中承载共享 Proposal/Design/Tasks、代码核验、Gap、外部依赖、Review Issues 和实现 Spec。Game Rule 只保存玩家可观察规则、来源和配对引用；其工作台详情不显示共享 Proposal/Tasks/实现审核，批准和 sync 均随 Feature 成对完成。
- 配对 Change 的审核区聚合 Change review 与各 capability review 的代码依据；`partial` 只表示仍有实现差异，不隐藏已存在依据。依赖区只展示跨 Change 的外部依赖，同一 Change 内规则指向配对实现等内部边不计入数量或列表。
- schema-v5 `spec-review.json` 保存单条 Spec 的 Verification、Review Issues、readiness、gap IDs、dependency IDs、来源与可选 `editorGuidance`。代码证据保存 Unity GUID、显示路径、脚本 SHA-256、入口行和具体大功能；功能描述必须说明代码做什么，不得只写脚本名或“脚本主要职责”，GUID/hash 相同时复用。
- 正式 Spec 与 Draft Change 内的 Spec 使用相同折叠标题组件，并可编辑保存当前 Change 内的 Spec。
- Markdown 显示层把 Purpose 段落中的 capability ID 与依赖边转换为可读名称；缺失 Dependency 使用小警告图标，目标名称与期望分类同行显示，不改写底层 Markdown 或机器 ID。
- Review Issues 在界面合并显示设计冲突、依赖缺失和实现差异；底层 Gap 仍只表示依赖树缺失。blocking 必须解决，warning/info 只有用户显式接受后才通过审批。
- Gap 只表示缺失依赖。
- 状态修改必须保留执行者与时间；已有身份映射文件只读，除非用户另行要求维护它。
- 文档桥接属于独立可选能力，不因安装工作台自动启用。
