# zWorkFlow Feature Coverage

当前覆盖版本：2026-07-29。此文件是 setup 与发布压缩包的验收清单，不是功能路线图。

## 可移植性边界

- `PACKAGE_MANIFEST.json` 是分发白名单；发布目录只包含通用 skills、角色、setup、平台模板和可选包。
- 分发包不得包含具体项目的 `project-context`、`project-architecture`、`project-domain-*`、成员资料、设计来源路径、正式 Spec、Change、archive、Draft 内容或工作台偏好。
- setup 必须从目标项目事实生成内容层，不能从制作分发包的项目复制领域知识。
- Unity 工作台只在检测到 Unity、没有同类界面且全部目标路径未占用时成套安装；非 Unity 项目保持纯工作流模式。
- OpenSpec CLI 必须满足 `>=1.6.0 <2.0.0`；缺失或过旧时由完整 setup 自动安装兼容 1.x 并复验。复验失败时不得安装 Unity 工作台。

## 工作流能力

| 能力 | 权威来源 | setup 验收 |
| --- | --- | --- |
| GitHub CLI bootstrap | `package.json`、`bin/zworkflow.mjs`、`CLI_BOOTSTRAP.md` | `npx --yes github:Hubr1zz/zWorkFlow setup` 可下载到未占用的 `<项目>/zWorkFlow`；不复制 `.git`/`.github`/`node_modules`，不覆盖已有目录，并输出 Agent setup 指令 |
| OpenSpec CLI 前置门禁 | `PACKAGE_MANIFEST.json` 的 `runtimeRequirements.openSpecCli` | Node.js `>=20.19.0`；`openspec --version` 位于 `>=1.6.0 <2.0.0`；缺失或过旧时安装 `@fission-ai/openspec@^1.6.0` 后复验 |
| OpenSpec intake、propose、update、apply、sync、archive、explore | `.agents/skills/openspec-*` | skills 与 references 完整存在 |
| 设计导入、完整 Spec 去重、中央 Draft Store、成套规则/Feature/Architecture Draft、增量合并、冲突选择、`修改<id>` | `openspec-derive-design-specs/` | 每个玩法设计生成精简规则 Draft 与配套 Feature 实现 Draft；Change 使用标准 delta 标题并在批准前 strict validate；Architecture 只在共享契约变化时生成 |
| Spec 三分类、依赖树、Gap、Review Issues、readiness | `metadata-schema.md` | schema v4 与工作台解析模型一致；Gap 只表示依赖缺失 |
| Draft 审批生命周期 | `change-schema.md` | HelpBox 显示门禁结果；正式 Spec 或已批准的活动 Change 可满足审批依赖，apply 时再严格检查依赖已实现且未阻塞；Feature 单一批准入口、apply 沙箱、显式三方 sync、完成后按钮归档 |
| GUID 代码证据 | `verification.codeEvidence` | 显示有效/修改过/缺失/失效；修改过不阻止审批并在 apply 前自动重核验，失效时先调整 Change，缺失/失效/未核验阻止审批 |
| 架构资料发现与 Agent 工作流共存 | `ARCHITECTURE_SPEC_DISCOVERY.md`、`AGENT_WORKFLOW_COEXISTENCE.md` | 两条检测流程独立；工作流先只读分析，再对已验证迁入共享源的工具专属正文执行薄化，冲突和历史数据保持不变 |
| 多 AI 工具、团队并存与模型路由 | `AI_TOOL_ADAPTERS.md`、`adapters/registry.json`、`registry.schema.json` | 共享角色只声明模型需求 profile；只为当前 active 或用户明确指定的工具增量适配；已验证映射或唯一运行时候选自动选择，歧义由当前成员确认一次并存入本地忽略文件；无团队级唯一 active tool / model |
| 多路径设计来源 | `design-source.json` schema v2、`design-source-schema.md` | 多个等价路径使用稳定 ID；按 scope 跨路径收敛后再做语义类型过滤，同名来源可审计 |
| OpenSpec 中英文增量翻译 | `openspec-translate/`、`openspec/localization.json`、`openspec/translations/` | 原路径保持唯一权威；文件与块级 SHA-256 只重译变化块；Spec 条目名称按 capability ID 分别保存中英文并驱动当前语言引用 |
| C# 代码结构查询加速 | `codebase-query/` | Unity 项目在 `Assets` 或显式项目内 source roots 存在 C# 且 PowerShell 7+ 可用时安装；索引 v6 只保存 UTF-8 内容指纹与可移植 `/` 相对路径，正式索引进入 Git，本机 sidecar 保持忽略，并提供文件级增量提取、完整类型、继承、receiver 调用绑定、影响分级、原子发布和词法回退；缺少 `pwsh` 时经用户许可才尝试安装，否则回退原生检索 |
| C# 索引可视化 | `AgentWorkbenchWindow.CodeIndex.cs` | 主入口手动异步构建 `Assets/**/*.cs` 全量派生索引，展示构建进度、磁盘/索引文件数、覆盖率、缺失项、类型、方法与解析调用统计 |
| 实现路由索引与旁路实现收养 | `track-implementation-progress/`、`reconcile-direct-implementation/`、`implementation-audit.json` | 从正式 Spec 的 `implementation.json` 与 active Change 生成带输入 digest 的轻量路由摘要；Git/C# 变化只生成本地候选。直接实现若符合不变的正式 Spec则核验投影，否则生成 post-hoc adoption Change 供人类审查，不维护全局 Ledger |
| 共享功能目录边界 | `.agents/skills/<功能>/references/` | 保护清单与维护队列分离并按需读取；项目事实和完整功能文档只保留一个权威源；工具目录只含入口、命令、配置和薄 wrapper |
| 已有工作流内容保全接入 | `AGENT_WORKFLOW_COEXISTENCE.md` | 完整 Claude/Codex Skills、角色与项目代码流程先迁入共享源并校验，再把原路径薄化；冲突项不做半迁移，工具设置/凭据/历史保持不变 |
| 工程能力模块拆分 | `project-tooling/` | README/Wiki 核心模块经目录、接口、asmdef 或配置证据核验后分别建条目；总框架/启动 System 不得吞并 Resource、Event、Config、UI、Procedure 等稳定模块 |
| Architecture 工具类使用策略 | `project-tooling/` | 具有公共消费入口的 Architecture 首次建档时基于代码、接口与调用点自动生成一次中英文使用策略；重复 setup 不覆盖已有正文 |
| 无 Python 环境降级 | Agent 原生步骤 | Python helper 始终可选，不得阻塞导入、校验或发布 |
| 文档工作流与项目桥接 | `packages/document-workflow/`、`packages/document-project-bridge/` | 仅显式安装；至少一个设计文档路径有效时建立本机桥接，各来源独立重建可折叠 Markdown 结构，刷新时发现新增/删除文件、显示进度与实现后变更，不自动触发设计导入或 proposal |

## Unity 工作台能力

运行源码与 setup 模板必须成对存在且内容一致：

- `Assets/Scripts/Editor/AgentWorkbenchWindow.cs` ↔ `setup/assets/AgentWorkbenchWindow.cs.template`
- `Assets/Scripts/Editor/AgentWorkbenchWindow.ImportReports.cs` ↔ `setup/assets/AgentWorkbenchWindow.ImportReports.cs.template`
- `Assets/Scripts/Editor/AgentWorkbenchWindow.Changes.cs` ↔ `setup/assets/AgentWorkbenchWindow.Changes.cs.template`
- `Assets/Scripts/Editor/AgentWorkbenchWindow.CodeIndex.cs` ↔ `setup/assets/AgentWorkbenchWindow.CodeIndex.cs.template`
- `Assets/Scripts/Editor/AgentWorkbenchWindow.Data.cs` ↔ `setup/assets/AgentWorkbenchWindow.Data.cs.template`
- `Assets/Scripts/Editor/AgentWorkbenchWindow.Models.cs` ↔ `setup/assets/AgentWorkbenchWindow.Models.cs.template`
- `Assets/Scripts/Editor/AgentWorkbenchWindow.Translations.cs` ↔ `setup/assets/AgentWorkbenchWindow.Translations.cs.template`
- `Assets/Scripts/Editor/AgentWorkbenchWindow.EditorGuidance.cs` ↔ `setup/assets/AgentWorkbenchWindow.EditorGuidance.cs.template`
- `Assets/Scripts/Editor/AgentWorkbenchWindow.Engineering.cs` ↔ `setup/assets/AgentWorkbenchWindow.Engineering.cs.template`
- `Assets/Scripts/Editor/AgentWorkbenchSupport.cs` ↔ `setup/assets/AgentWorkbenchSupport.cs.template`

必须覆盖：

- 增量维护三状态页签、维护人和维护备注。
- 正式 Spec 与正式 Change 的分类页签位于各自左侧列表区，与 Draft Change 布局一致；支持三分类、自定义二级文件夹、原 Markdown 编辑与未保存确认。正式 Spec、正式 Change 与 Draft Change 条目支持右键或双击重命名显示名称，且不改变 ID/目录；共享自定义文件夹在不含正式 Spec 或 Change 时，可从任一正式视图删除。
- 配对 Draft 批准为正式 Change 后保留 `paired` 展示类型，并从内部 capability 汇总真实分类；同一个配对 Change 可出现在其 System、Feature、游戏规则对应页签中，不得降级为“未分类”。
- 单条 Spec 详情固定布局、内部滚动、Code Readiness、依赖与 Gap。
- Unity GUID + 显示路径 + 脚本 SHA-256 + 入口行 + 具体大功能代码证据；hash 改变显示“修改过”但不阻止批准，apply 前自动语义复核。
- 顶部关系图谱入口、System/Feature 颜色节点（legacy Architecture 兼容归入 System）、待合入 Delta 徽标与所属 Change、彩色标题与紧凑详情、核心实现思路、缩放/拖动/平移和动态尺寸；阻塞项可定位依赖节点，待合入项可打开所属 Change。
- 关系图谱与 `REFACTOR_QUEUE.md` 解耦：队列缺失、格式错误或为空时仍显示图谱/空状态；工程能力页采用左侧分类/条目筛选与右侧详情布局，不绘制能力关系图，并随工作台语言显示中英文目录内容。
- 关系图谱右侧提供工程能力入口；按 Plugin/Architecture/System 展示 Git 同步目录与依赖图，Plugin 可保存判断依据，Architecture 强制 required/locked 且只能在用户确认后由 Agent 修改。
- Changes 与正式 Spec 共用分类/文件夹式导航，条目显示 Tasks 进度；正式/Draft Spec 内容与 Change 详情的 Review、Dependencies、Tasks、Proposal、Design 共用显眼的可折叠标题栏，并可分别编辑保存对应 Markdown。
- 正式 Spec、正式 Change、Draft Change 的列表与详情 ScrollView 只允许纵向滚动；所有正文、Markdown、代码块与编辑文本按可用宽度自动换行，禁止出现水平滚动条。
- 导入批次/Draft Change capability 分组导航；进入批次默认单独显示导入记录信息与提示，左侧返回按钮右侧提供切换入口，点击 Spec 条目后右侧只显示对应详情。Draft 列表与 OpenSpec 一样支持全部、System、Feature、游戏规则分类筛选，并提供删除确认、Change ID 复制、Review Issue 接受和审批门禁；Game Rule 不显示共享 Proposal/Tasks，Design 只显示反向来源文档链接与配对 Feature 跳转。
- Draft 与正式 Change 的审核问题统一使用表格展示；blocking/warning/info 以迷你图标和颜色区分，非阻塞项支持逐行勾选接受及编辑 `acceptanceNote`，并同步 Change 与 capability 两层审核记录。
- 配对 Feature 是详细信息与唯一审批入口，承担共享 Proposal/Design/Tasks、审核、依赖、实现差异和实现 Spec；Game Rule 只显示来源、配对 Feature 跳转和规则专属信息，并随 Feature 自动批准与同步。
- Feature/System 仅在 Change 的 `spec-review.json.editorGuidance` 或正式 `implementation.json.editorGuidance` 含有效人工动作时显示“引擎配置”按钮；正式 Change 汇总内部 capability，Draft 只显示当前 capability，纯 C#/无人工接入内容不显示空按钮。
- Draft Change 在导入时立即生成完整工件并记录正式目标 hash 与存在时的 Change 内快照；所有类别审批后都只整体转移到正式 Change。apply 不触碰正式 Spec。显式 sync 比较 base/current/Delta：非重叠 Requirement 可保留式合并，目标删除、重叠或旧基线无快照进入审查，只有确认覆盖/语义混杂才阻止。普通 EventBus 等工具不建节点，只有项目公共契约变化或至少两个 capability 依赖稳定语义时才生成 System 节点；真 Architecture 进入工程能力目录。
- 正式 Change 的归档按钮只在 Tasks 全完成且 Spec 已同步时启用；纯代码移动到日期 archive，不自动 sync，且与永久 Delete 明确区分。
- 导入批次只在仍引用至少一个现存 Draft Change 时保留；批准、删除或外部移动最后一个 Draft 后自动删除该导入记录，并清除中央 Draft 索引中的悬空批次引用。
- “设计文档树”顶部可添加、替换和移除多个等价路径；工具栏状态灯只表示桥接有效。树按 Markdown 目录显示与正式 capability 关联的进度，且只消费通过 input manifest 校验的路由摘要；缺失或过期时 fail-closed。独立“指令列表”覆盖设计导入与筛选、检查文档及时性、修改导入、Spec 翻译/同步、apply、sync 和归档。
- 中英文、独立浅色/深色主题切换、深色控件线框增强、工作台正文/背景/面板颜色、警告与提示文字跟随正文、Markdown H1-H6/正文颜色、Markdown 默认应用、窗口位置与偏好尺寸自动恢复，以及本地配置隔离；不得读取 Unity 主题。
- 生成权威语言由 Git 同步的 `openspec/localization.json` 配置，默认跟随设计文档并支持中英文；同一文件还按 capability ID 保存可分别重命名的 `zhCN` / `enUS` Spec 条目显示名。翻译副本位于 `openspec/translations/<language>/`，只有文件/块 hash 与权威内容同步时才显示。缺失或失效时只显示翻译指令，非权威 Markdown 在工作台只读。
- 配置了有效设计来源时，玩法规则 Spec 可按 `<source-id>::<relative-path>:<line>` 来源引用反向打开设计 Markdown；旧单路径引用继续兼容，不依赖 bridge 开关。
- 指令面板覆盖 apply、sync specs、archive 的 Change-ID 形式，Changes 详情可复制 ID。
- 顶部 Root、配置、介绍入口；介绍面板切换使用介绍与快速上手，二次开发者说明仅作为附带文件。

## 人类文档与本地文件

- `WORKFLOW_OVERVIEW.md`：面向无开发经验使用者的简短介绍。
- `assets/zworkflow-lifecycle.png`：使用介绍中的工作流阶段图，必须随 setup 包和 Git 一起分发。
- `WORKFLOW_QUICKSTART.md`：用最简单的自然语言带普通用户完成第一次使用。
- `WORKFLOW_DEVELOPER_GUIDE.md`：面向二次开发者的产品结构、安全边界和实现约束。
- `LICENSE`：Apache License 2.0 完整许可证文本，随目录与压缩包一起分发。
- `THIRD_PARTY_NOTICES.md`：保留 OPSX 衍生内容的来源与 MIT 许可声明。
- 三份人类文档只保存在 `zWorkFlow/` 安装源顶层，不再维护 `setup/templates/` 或目标项目根副本。
- 工作台优先读取 `zWorkFlow/` 内的人类文档，并只为旧版安装只读回退项目根。Markdown 渲染器支持项目目录内的本地图片；相对路径依次按文档目录、项目根和 `zWorkFlow/` 解析，图片缓存随窗口关闭释放。
- 工作台读写 `.agents/skills/project-refactor-queue/references/REFACTOR_QUEUE.md`，不得把 `.claude/REFACTOR_QUEUE.md` 当作数据源。
- 根 `.gitignore` 由 setup 幂等创建或合并，至少忽略 `.DS_Store`、Python 缓存、工作台配置、设计源机器路径、个人成员偏好和 `.agent-memory/zworkflow/local/` 工具选择。

## 平台兼容矩阵

| 环境 | 行为 |
| --- | --- |
| Windows Editor | 目录通过 `RevealInFinder` 打开；Unity 2022.3 主工具栏桥可用 |
| macOS Editor | 目录通过 `RevealInFinder` 打开；不调用 Windows shell 或盘符路径 |
| Linux Editor | 目录回退 `OpenWithDefaultApp` |
| Unity 2022.3 | 编译主工作台与受版本宏保护的旧主工具栏桥 |
| Unity 6+ | `UNITY_6000_0_OR_NEWER` 排除内部主工具栏反射；通过稳定 Tools 菜单打开工作台 |

所有工作台源码受 `UNITY_EDITOR` 保护。宏分支静态检查不能替代对应操作系统和 Unity 版本的实机启动测试。

## 发布验收

1. 编译当前 Unity Editor 程序集，要求 0 error。
2. 额外启用 `UNITY_6000_0_OR_NEWER` 编译宏，确认内部工具栏反射代码不参与编译。
3. 比较运行源码和 setup assets 哈希。
4. 按 `PACKAGE_MANIFEST.json` 检查所有通用/条件 skills、roles、references、packages 和三份人类文档均进入分发目录，并确认没有项目内容层或运行历史；`codebase-query` 必须包含主脚本、类型绑定库、契约和回归测试。
5. 运行 `npm test`，并至少在临时空目录验证 CLI 不覆盖目标、不携带仓库私有状态；正式发布 npm 前只宣称 GitHub `npx` 入口。
6. 解析 `setup/adapters/registry.json` 并按 schema 校验，检查 `active-or-explicit-only`、adapter id 唯一、角色 profile 引用有效、每个 adapter 都有模型路由、每项安装模式合法、安装源存在、共享源存在，并确认 available/repository-supported 不触发安装、模型歧义不自动决定、无团队级唯一 active tool / model。
7. 删除 `__pycache__`、`*.pyc`、`.DS_Store` 等机器生成物。
8. 重新生成 `zWorkFlow.zip`，并核对压缩包内容与干净分发目录；压缩包不得包含自身或制作目录中的额外文件。
