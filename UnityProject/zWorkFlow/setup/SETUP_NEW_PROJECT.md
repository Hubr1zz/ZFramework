# Setup New Project

本文件用于在用户明确要求 setup、检测或导入时，把分发目录中的通用 zWorkFlow 安装到当前项目。一次明确的完整 setup 请求已经授权检测并安装兼容的 OpenSpec CLI、安装包内通用核心，以及在检测到 Unity 项目且不存在同类界面时安装配套工作台；也授权对已验证可完整迁入共享 `.agents/` 的工具专属工作流执行内容保全式迁移和薄化。不授权安装 Node.js、丢弃内容、覆盖冲突目标、修改工具设置/凭据或迁移无法证明等价的工作流。

## 适用场景

- 用户把 `zWorkFlow/` 作为独立来源目录放入项目并要求 setup。
- 用户把新版 `zWorkFlow Pack/` 放在已有 zWorkFlow 内并要求 setup/升级。
- 用户要求检测项目中的架构资料或 Agent 工作流。
- zWorkFlow 与架构资料、Agent 工作流以任意先后顺序导入后，用户要求重新检测。

未经用户明确触发，不运行完整 setup 或后台融合扫描。

## Phase 0：校验分发包与目标边界

1. 把包含本文件的目录识别为只读安装源。若目录名为 `zWorkFlow Pack` 且父目录已有 `setup/PACKAGE_MANIFEST.json`，先读取 [UPGRADE_EXISTING_INSTALLATION.md](UPGRADE_EXISTING_INSTALLATION.md)，比较 `packageVersion` 并完成或跳过版本化升级，再以父目录的父目录作为目标项目根；其他情况把来源目录的父目录识别为目标项目根。不得把安装源自身当作目标项目。
2. 读取 `setup/PACKAGE_MANIFEST.json`，确认其中列出的必需文件、通用 skills、角色、setup 文档和工作台模板全部存在。清单缺失、JSON 无法解析或来源包含清单声明的禁入内容时停止 setup。
3. 建立目标项目只读基线。目标中已经存在的入口、skills、commands、agent 配置、自动化、工作流数据与 OpenSpec 数据先保持只读；只有版本升级清单声明的 managed 程序内容，或 Phase 2 内容保全迁移完成全部校验后的工具专属正文，才允许替换。冲突目标和保留数据一律不得覆盖。
4. 分发包只提供通用工作流，不携带任何预生成的 `project-context`、`project-architecture`、`project-domain-*`、成员资料、设计源机器路径、正式 Spec、Change、archive 或导入历史。

## Phase 0.25：验证 OpenSpec CLI

OpenSpec CLI 是 OpenSpec 生命周期和 Unity 工作台的必需依赖。读取 `PACKAGE_MANIFEST.json` 的 `runtimeRequirements.openSpecCli`，按下列顺序处理：

1. 运行 `node --version`，要求 Node.js `>=20.19.0`。缺失或过旧时停止 OpenSpec CLI 安装，记录 `blocked-node-missing`；不得自动安装 Node.js、调用 `sudo` 或修改 PATH。
2. 运行 `openspec --version` 并解析语义版本。版本位于清单声明的 `>=1.6.0 <2.0.0` 时记录 `available`，不得为了追逐最新版而改动有效环境。
3. CLI 缺失、无法执行或版本低于最低值时，完整 setup 请求即授权运行清单声明的跨平台 npm 安装命令 `npm install -g @fission-ai/openspec@^1.6.0`。不得从非官方包名或任意脚本安装。
4. 已安装版本达到或超过不兼容上界时不得自动降级，记录 `blocked-incompatible-version`，等待用户决定。
5. 安装后必须在同一 setup 中重新运行 `openspec --version`。只有版本进入兼容范围才记录 `installed-and-verified`；命令仍不可见时，检查 npm 全局可执行目录是否已在当前进程 PATH 中，但不得静默永久修改 PATH。
6. 验证失败不删除已安装内容，也不继续安装 Unity 工作台。其余不依赖 CLI 的只读发现和通用文件安装可以继续，但 OpenSpec 命令能力必须标为不可用。

Node 与安装命令以 OpenSpec 官方安装说明为准：[OpenSpec Installation](https://openspec.dev/docs/installation)。

## Phase 0.5：安装通用核心

逐文件安装，目标不存在时复制，目标已存在时跳过并记录 `reuse-existing` 或 `conflict`：

1. 只在项目根缺少 `AGENTS.md` 时，从 `setup/templates/AGENTS.md.template` 安装薄入口；完整通用规则继续保存在安装源 `zWorkFlow/AGENT_WORKFLOW_README.md` 与 `zWorkFlow/AGENTS.md`，不得复制到项目根。已有同名入口保持只读。
2. 将清单中的通用 `.agents/skills/<skill>/` 和 `.agents/agent-roles/` 安装到项目根对应路径。不得安装分发包清单之外的项目内容 skill。
3. 安装 `.agent-memory/README.md`、`setup/templates/.agent-memory/zworkflow/team/MAINTAINERS.md` 与 `zworkflow/team/members/_TEMPLATE.md`；旧版 `.agent-memory/team/` 存在时先逐文件迁移到该命名空间并核对哈希，再移除旧路径。不复制安装源中的具体成员文件。成员映射在 setup 能确认昵称且目标路径未占用时才按 `team-member-preferences` 创建。
4. 从 `setup/templates/openspec/` 幂等创建缺失的空 Draft Change 索引、`openspec/drafts/changes/` 和 Spec metadata；不得创建正式 Spec、Change、archive、导入历史或已废止的 `openspec/drafts/specs/`。
5. 幂等创建 `openspec/localization.json` 与空的 `openspec/translations/manifest.json`。能读取设计来源时，把 `generationLanguage` 初始化为设计文档主语言；尚未配置来源时保留 `source`。`specTitles` 初始化为空数组，后续以 capability ID 保存中英文条目显示名。翻译目录和 manifest 必须参与 Git，同 setup 不携带任何项目翻译正文。
5. 通用核心是本次 setup 的默认安装项，不需要第二次确认；可选文档包与项目桥接仍必须显式选择。
6. 清单中的条件 skill 只在目标满足 `when` 时逐文件安装。`codebase-query` 要求 Unity 项目的 `Assets` 或用户显式提供的项目内 source roots 存在 C#，且当前环境可执行 PowerShell 7+（`pwsh`）；不要求 `Assets/Scripts`。安装后幂等创建或追加根 `.ignore` 中的 `**/code-query-index.json`，避免通用 `rg` 扫描 Git 管理的正式索引或旧迁移索引；不得覆盖其他规则。先运行 `pwsh --version` 验证主版本。缺失时，setup 只可在用户明确许可安装本机依赖后尝试：Windows 客户端优先使用微软推荐的 `winget install --id Microsoft.PowerShell --source winget`；macOS 优先提示安装微软签名的 PKG，也可在用户接受社区维护方式且已安装 Homebrew 时运行 `brew install powershell`，或在已有 .NET SDK 时运行 `dotnet tool install --global PowerShell`。安装后重新探测 `pwsh`；未获许可、无法安装或验证失败时记录 `skipped-condition`，继续使用 Agent 原生 `rg` 与源码读取，不得阻塞 setup。不得静默执行系统级安装、`sudo` 或修改用户 PATH。

安装方式只引用微软当前官方文档：[Windows 安装 PowerShell 7](https://learn.microsoft.com/powershell/scripting/install/install-powershell-on-windows) 与 [macOS 安装 PowerShell 7](https://learn.microsoft.com/powershell/scripting/install/install-powershell-on-macos)。Homebrew 属于微软文档列出的社区维护替代方式，setup 报告必须明确这一点。

## Phase 0.75：确保本地忽略规则与 zWorkFlow 人类文档

每次显式 setup 都必须先完成以下幂等步骤：

1. 检查项目根目录 `.gitignore`；不存在时自动创建空文件。
2. 读取 `setup/templates/gitignore-agent-workflow.txt`，将其中带开始/结束标记的完整区块追加到根 `.gitignore`。已有相同标记或等价规则时不得重复追加，也不得改写用户的其他 ignore 规则；其中必须包含每位成员本地工具选择目录 `.agent-memory/zworkflow/local/`。
3. 确认 `zWorkFlow/WORKFLOW_OVERVIEW.md`、`zWorkFlow/WORKFLOW_QUICKSTART.md`、`zWorkFlow/WORKFLOW_DEVELOPER_GUIDE.md` 与 `zWorkFlow/assets/zworkflow-lifecycle.png` 存在，前三者分别作为普通使用者介绍、快速上手和二次开发者说明，图片供介绍页与 Markdown 渲染器使用。
4. 不把上述人类文档复制到项目根。工作台固定优先读取 `zWorkFlow/` 内的使用介绍和快速上手，二次开发者说明作为安装源附带文件保留；仅为旧版安装兼容时才允许只读回退到项目根已有文件。

这一步是 setup 对现有工作流只读原则的窄例外：只允许创建 `.gitignore` 或幂等追加 zWorkFlow 自己的标记区块，不得删除、排序或重写其他规则。

## 不干扰边界

setup 开始时先建立只读基线，列出已有入口、skills、commands、agent 配置、自动化、工作流数据与 OpenSpec 数据。

- 基线中的工作流在分析和迁移准备阶段保持只读；仅当共享目标未冲突、内容已逐文件迁入、旧路径引用已更新、能力与哈希清单验证通过时，允许把对应工具专属正文替换为薄 wrapper。
- 已有根 `AGENTS.md` 保持入口职责；根/`.claude/CLAUDE.md` 中的项目代码规则先迁入项目共享 skill，再保留或生成只指向 `AGENTS.md`/`.agents` 的薄入口。
- 不修改工具设置、凭据、用户历史和无法归类的持久化数据。迁移冲突时保留原文件并报告，不做半迁移。
- 目标路径发生冲突时跳过安装该文件，在能力映射中记录复用或冲突；不得覆盖。
- 已有历史、memory、队列、Spec、Change、ADR 和报告保持原样。
- setup 只可写入未占用的通用核心、zWorkFlow 自有项目内容文件、共存分析缓存，以及用户明确要求生成的 System Spec。
- 无法从项目确认的事实标为缺失或待确认，不编造架构。

## Phase 1：只读发现项目事实

优先用文件清单和定向搜索收敛范围，避免读取整个仓库。

识别：

- 项目类型、语言、依赖管理、构建与验证入口。
- Plugin、可独立复用 Architecture 与项目耦合 System 的证据；依赖清单、插件目录、DLL/asmdef、Scripting Define 和命名空间引用只做定向扫描。
- README/Wiki 中列出的核心模块，并以独立目录、公共接口/入口类型、asmdef 或配置输出交叉核验。满足稳定模块边界的 Resource/Event/Config/UI/Procedure 等必须分别建工程能力条目，不能只保留总框架或启动系统。
- 应用、工具、测试或编辑器入口。
- 源码、资源、配置、测试、文档与生成物目录。
- 模块边界、数据流、全局服务、组合根、状态机和跨系统调度。
- Architecture 候选是否具有供其他模块或业务代码调用的公共 API、服务入口、模块接口、静态工具或基础设施能力；同时记录首选入口、调用层级、所有权、初始化、取消、释放和已知误用证据，用于判断是否为工具类并生成使用策略。
- README、docs、ADR、模块说明与已有正式 Spec。
- 已有 Agent 入口、skills、commands、agents、自动化和持久化数据，包括 Codex、Claude Code、Cursor、GitHub Copilot、Gemini CLI、Windsurf、Kimi Code CLI 等工具专属路径。
- 已有 zWorkFlow 自有缓存和产物。

先读索引和摘要；只有在判断能力、冲突或代码证据时才读取具体文件。报告每类来源的读取范围，避免后续任务重复扫描。

## Phase 2：分析已有 Agent 工作流

先执行 [AI_TOOL_ADAPTERS.md](AI_TOOL_ADAPTERS.md)，读取 `setup/adapters/registry.json`，识别当前成员使用的工具、本机可用工具和仓库已支持工具。工具选择是多选，不得生成团队级唯一 `activeTool`；当前成员的检测结果只写入被忽略的 `.agent-memory/zworkflow/local/tool-selections/<nickname>.json`。安装资格只来自当前运行时 `active` 身份或用户本次明确指定；`available`、`repository-supported` 和项目标记只用于报告，不安装。

随后执行 [AGENT_WORKFLOW_COEXISTENCE.md](AGENT_WORKFLOW_COEXISTENCE.md)。

对工具目录中的完整正文执行内容保全接入：

1. 以 capability 为单位清点根 `CLAUDE.md`、`.claude/skills|commands|agents`、`.codex/skills` 及注册表声明的其他工具路径，并记录文件 hash、引用和消费者。
2. 项目专属 Skill 迁入 `.agents/skills/<id>/`；项目代码强制流程迁入 `project-context` 或命中的领域 Skill reference；Agent 角色迁入 `.agents/agent-roles/`；命令只保留触发语义并转到对应共享 Skill。
3. 共享目标已存在时做语义比较：等价则复用，互补则保全式合并，冲突则停止该 capability 的迁移。不得覆盖任一正文或静默选边。
4. 更新迁入内容中的 `.claude/skills`、`.codex/skills`、工具私有 memory 和机器绝对路径，全部指向 `.agents/`、`.agent-memory/` 或项目相对路径。
5. 只有逐文件内容清单、必需 reference/script、入口触发和引用校验全部通过，才把 Claude skills/commands/agents 替换成 wrapper，并把 Codex 完整 skill 副本移出 `.codex/skills`。优先保留可恢复的本地迁移备份，且必须由 `.gitignore` 排除。
6. 迁移完成后根/工具入口仍可存在，但只能是薄入口；“文件仍存在”不等于迁移失败，“仍保存完整正文”才是失败。

按 `registry.json` 的模型需求与 adapter `modelRouting` 为当前 active / 明确指定的平台解析子 Agent 模型：先读取运行时提供的 Agent/模型选项，再应用已验证映射或唯一匹配；发生歧义时只向当前成员确认一次，并将偏好与决定写入本地 tool selection。不得读取其他成员全局配置、把 Codex 模型名机械转换到其他平台、静默替换不可用模型，或在无法验证逐 Agent 选模时声称已经节省费用。

在同一次 setup 中，只对当前运行时自动识别为 active 或用户本次明确指定的工具执行注册表 `install`：`shared-direct` 不落盘任何工具副本；`copy-thin-if-missing` 按 `artifacts` 清单创建所有未占用的薄接口。运行时身份无法唯一判断时才询问当前工具；其他成员应在其工具中首次运行 setup，从而增量补装。目录型 artifact 递归处理，但仍逐文件跳过已有目标。不得要求用户再手工复制入口，也不得把完整 Skills、项目事实或工作台状态写入工具专属目录。

输出能力映射，并据此优化 zWorkFlow：

- 等价能力标记为复用，zWorkFlow 跳过对应重复步骤。
- 部分重叠只记录 zWorkFlow 需要补充的缺口。
- 冲突只告警，不修改任何一方。
- 把精简路由和来源指纹写入 `.agent-memory/zworkflow/integration/`。
- 后续任务在指纹未变化时只读精简摘要，不重复加载完整工作流。
- 对直接支持 `.agents/skills/` 的工具复用共享源；只有注册表声明需要时才在未占用路径创建薄入口或 wrapper。
- 团队中不同成员使用不同工具时，并行保留所有已确认适配器，不因本次执行工具删除或改写其他工具入口。

## Phase 3：发现项目系统资料并生成 System Spec

执行 [ARCHITECTURE_SPEC_DISCOVERY.md](ARCHITECTURE_SPEC_DISCOVERY.md)。

- 自动识别项目内部具有全局约束性质的架构 Markdown 或同等文档。
- 用代码、依赖配置和模块边界简单交叉核验描述是否仍然成立。
- 只把进入 Player 构建、与具体游戏玩法耦合且已确认的稳定运行时边界生成分类为 `system` 的正式 Spec；开发工具与工作流不得进入，legacy `architecture` 仅兼容读取。
- capability 已存在时不覆盖：相同内容跳过，有变化时走 OpenSpec Change。
- 增量维护依赖图；System Spec 只依赖 System Spec。真 Architecture 与 Plugin 写入 `project-tooling`，不进入玩法 Spec 分类。
- Gap 只表示缺失的依赖节点或契约。

本阶段与 Agent 工作流共存分析互相独立：架构资料形成 Spec；工作流资料只形成能力映射和冲突报告。

## Phase 4：建立 zWorkFlow 自有项目上下文

根据 Phase 2 的能力映射决定是否需要 zWorkFlow 自己的能力：

- setup 完成时必须存在且只存在一个可达的项目路由入口：已有等价项目速查时记录其具体入口并标记 `reuse-existing`；否则生成 `.agents/skills/project-context/SKILL.md` 与 `references/PROJECT-INDEX.md`。不得交付“没有等价入口且没有 Project Index”的中间状态。
- 已有等价项目速查时不创建重复 `project-context`。若外部同名 `project-context` 已占用但缺少可用索引或等价路由，不覆盖其正文；将该能力标记为 `conflict` 并停止依赖项目路由的后续薄化，等待用户决定迁移或补全。
- 已有等价重构队列、文档同步或成员偏好流程时标记复用，不创建重复能力。
- 没有等价能力时，从 [PROJECT_CONTENT_TEMPLATE.md](PROJECT_CONTENT_TEMPLATE.md) 成对创建最小 `project-context` 与 `PROJECT-INDEX.md`，再按需创建 `project-architecture` 与命中的 `project-domain-*`；通用核心已经提供空的 `project-refactor-queue` 与可配置的 `project-doc-sync`。只写 Phase 1 已确认事实，不复制安装源所在项目的内容。
- 通用 `project-tooling` 安装后，把 Phase 1 已确认的 Plugin/System 候选写入其 `references/tooling-catalog.json`。Plugin 的 `decisionBasis` 保持空白，除非用户本次明确给出依据；Architecture 只有经用户确认才能创建，且必须 `usagePolicy=required`、`locked=true`。
- 若目标项目有明确代码分层，在目录顶层声明项目自定义 `layers`，并用条目 `layerIds` 标记一个能力横跨的零到多个实现层。`kind` 仍只表示 Plugin/Architecture/System 的归属与复用边界，不把 Data/Adapter/View 或 MVC 等项目分层编码成 System 子类型；没有分层约束的项目省略这些可选字段。
- 写入目录前先按稳定模块边界拆分：一个条目必须拥有可单独路由的职责、证据、约束和依赖；禁止用“整个引擎/整个框架”条目代替 README 与代码已明确区分的核心模块。生命周期、启动编排或状态管线在剥离玩法/UI/内容数据后仍可独立复用时归为 Architecture；具体项目接入另归 System。文档存在但实现缺失的条目只能以明确 partial 约束写入，不得宣称已实现。
- 为工作台目录条目生成语义对齐的中英文字段：`displayName`/`displayNameEn`、`description`/`descriptionEn`、`capabilities`/`capabilitiesEn`、`constraints`/`constraintsEn`。稳定 ID、路径、依赖与版本不翻译；缺少英文内容不得声称工程能力页已完整支持英文。
- Architecture 首次建档或从其他类型重分类时，若 Phase 1 证明它是具有直接消费入口的工具类 Architecture，则同次自动生成非空且语义对齐的 `usageNotes` / `usageNotesEn`。策略必须覆盖适用与非适用场景、业务层首选入口、必要的所有权/生命周期规则和应避免的平行实现或误用；只使用代码、接口、调用点与文档已确认的事实。纯边界、阶段顺序或概念模型不强制生成。
- 自动策略只初始化一次：任一语言已有非空 `usageNotes` 时，重复 setup、来源指纹变化或增量发现均不得覆盖；新证据与旧策略冲突时只报告差异，只有用户明确要求重写策略才更新正文。`usagePolicy=required` 与 `locked=true` 仍是独立机器门禁。
- 把依赖清单、插件目录、DLL/asmdef 和命名空间证据的来源指纹写入 Git 忽略的 `.agent-memory/zworkflow/local/tooling-discovery.json`；指纹未变化时后续 setup 复用目录，不重新全仓扫描。
- 新建 zWorkFlow 自有能力时，完整内容和共享状态必须写入对应 `.agents/skills/<功能>/`；保护清单与增量维护队列分别写入 `project-refactor-queue/references/PROTECTED_FILES.md`、`REFACTOR_QUEUE.md`。工具目录只生成入口、命令、配置或薄 wrapper。
- 同名路径已存在时跳过，不做三向合并。
- 领域信息按实际项目命名，不从分发包携带任何预置领域内容。

## Phase 5：安装平台能力并按需补充可选能力

- 只有 Phase 0.25 已将 OpenSpec CLI 标记为 `available` 或 `installed-and-verified` 且检测到 Unity 项目时，才处理 [UNITY_WORKBENCH_INTEGRATION.md](UNITY_WORKBENCH_INTEGRATION.md) 中的完整模板集。首次安装要求没有同类工作台且全部目标路径未占用；升级时读取 `PACKAGE_MANIFEST.json.projectInstall` 与 `.agent-memory/zworkflow/install-state.json`，只有目标文件仍匹配上次安装 hash，或用户明确要求本次用移植包更新全部 zWorkFlow 内容时，才成套替换已有 Workbench。检测到未登记的项目自定义修改时整套停止并报告冲突，不做部分覆盖。优先复用已登记的 Editor tooling 根；首次无法唯一判断时使用 `Assets/Editor/zWorkFlow/`，不要求 `Assets/Scripts`。CLI 未通过验证时记录 `blocked-openspec-cli`，不得安装部分工作台模板。
- 非 Unity 项目不安装工作台源码，但保留 OpenSpec、设计导入、路由、关系数据和命令工作流。
- 已有但无法证明由 zWorkFlow 管理的同类工作台只记录能力映射；已由安装清单管理的 Workbench 按版本和 hash 安全升级。

只补用户当前目标需要且不存在等价能力的部分：

- 设计文档导入、schema-v5 完整 Draft Change、Review Issues 与审批门禁流程。
- 安装设计导入能力时只幂等创建 `openspec/drafts/changes/`；导入批次只保存审计与 `{capability, changeId, status}` 引用。`openspec/drafts/specs/` 已废止，索引不得复制正文。
- Spec 分类和依赖 metadata 的缺失空结构。
- 成员映射或维护队列的空模板。
- 文档包、中间桥接层及额外运行时均为显式可选项。
- 安装 `document-project-bridge` 时只把 `inspect-implemented-design-changes` 安装到项目侧。Workbench 允许选择任意候选目录，只要可扫描到 Markdown 就保存文档根路径并点亮桥接灯；项目不创建全局 Ledger，只读扫描设计 Markdown，并从校验有效的实现路由摘要投影进度。不得向设计包注入 `projectRoot` 或工程状态，不得因文档变化自动调用设计导入或创建 proposal。
- 发现旧 `.agent-bridge/project-sync.json` 时把它报告为 deprecated，不读取其中项目路径、不继续执行 `document-change-to-openspec`，也不为了迁移而改写外部设计包；由设计包 setup 幂等创建新账本。

除 OpenSpec CLI 官方要求的 Node.js 外，Python 或其他额外运行时不得成为前置条件。已有有效配置保持不变；缺失配置只在当前能力确实需要时创建。

## Phase 6：质量与成本审计

使用 [QUALITY_AUDIT_CHECKLIST.md](QUALITY_AUDIT_CHECKLIST.md) 检查会影响 zWorkFlow 正确性的问题，但只报告，不替用户修改项目设计或已有工作流。

同时检查：

- 是否仍有可由已有能力完成的重复步骤。
- 后续任务是否可以只读路由摘要而非完整工作流。
- 来源指纹是否足以判断何时需要重新扫描。
- zWorkFlow 新文件是否全部位于未占用路径。
- System Spec 是否有代码或文档证据、依赖方向是否合法。
- 按 [FEATURE_COVERAGE.md](FEATURE_COVERAGE.md) 核对本次分发包是否包含最新工作流、schema、工作台源码、三份人类文档和平台兼容分支；缺项时 setup 失败，不得静默降级。
- 校验 AI 工具适配器注册表可解析、ID 唯一、安装策略为 `active-or-explicit-only`、每项都有安装模式、安装源存在，并验证多工具团队不会共享或覆盖唯一 active tool。

## 后导入检测

架构资料或 Agent 工作流在 setup 后导入时，不重新初始化：

1. 用户要求检测或导入。
2. 比较来源指纹，只读取新增或变化文件。
3. 项目耦合的系统资料走 System Spec 发现流程；Plugin/Architecture 走 `project-tooling` 增量发现。
4. Agent 工作流资料走共存分析流程。
5. 更新 zWorkFlow 自有缓存；不修改新导入的文件。

## 交付

按 [SETUP_OUTPUT_CONTRACT.md](SETUP_OUTPUT_CONTRACT.md) 报告：

- 只读扫描了什么。
- 哪些已有能力被复用，因此 zWorkFlow 会跳过哪些步骤。
- 哪些能力仅在需要时补充。
- 发现了哪些冲突，等待谁决定。
- 当前成员 active/available 的工具、仓库已支持工具，以及每项能力的原生/薄 wrapper/降级状态。
- 当前成员的模型路由偏好、各角色 profile 的 resolved / native-auto / inherited / unresolved 状态，以及需要确认的歧义；不要把本地具体模型决定写入团队共享报告缓存。
- Node.js 与 OpenSpec CLI 的检测版本、安装动作、最终验证状态，以及 CLI 未通过时工作台被阻止的原因。
- 新增或复用的 System Spec 及代码核验证据。
- 写入了哪些 zWorkFlow 自有文件。
- 通用核心、项目内容层和平台工作台分别安装、复用或跳过了什么。
- 明确确认哪些已有工作流文件没有被修改。
