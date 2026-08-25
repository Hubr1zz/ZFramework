# AGENTS.md

请使用中文写提案和回答。

本文件是跨工具共享入口，供 Codex、Cursor、GitHub Copilot、Windsurf、Kimi Code CLI 及其他支持 `AGENTS.md` 的工具读取。执行任何代码、文档、配置、架构或工作流修改前，必须先阅读并遵守 [AGENT_WORKFLOW_README.md](./AGENT_WORKFLOW_README.md)。

## OpenSpec Intake Gate

会新增或改变功能、外部可观察行为或公共运行契约的非平凡产品功能或运行框架改动，必须先通过 `openspec-intake-gate` 审阅。

不自动进入 zWorkFlow：单纯引擎适配、编辑器/开发工具、Agent 工作流、行为保持型重构、性能优化、代码清理、实现方案调整、问答与边界清晰的小型改动。即使改动位于框架代码，只要功能、外部行为和公共契约保持不变，也走普通流程。用户显式调用 OpenSpec/zWorkFlow 可以触发审阅，但不能让开发工具越过正式 Spec 的玩法范围门禁。

通过条件：用户请求必须能明确回答目标、背景/动机、修改范围、约束/不做什么、验收标准。缺失关键项时，停止执行并按 `openspec-intake-gate` 的警告模板要求用户重写需求。

## 工作流入口

- 完整 skill 内容只维护在 `.agents/skills/`。
- 项目事实、维护队列和共享工作台状态归属于对应 `.agents/skills/<功能>/references/`；工具目录只能保存入口、命令、配置和薄 wrapper。
- 支持 `.agents/skills/` 的工具直接扫描共享源；不支持的工具只通过薄 wrapper 转入。
- `.codex/` 只保留 Codex 专用 agent 壳层和说明，不维护 `.codex/skills/` 完整副本；发现旧完整副本时先迁入 `.agents/skills/` 再移出可发现路径。
- Claude Code 通过根/`.claude/CLAUDE.md` 与 `.claude/skills/*` wrapper 进入同一套 `.agents/skills/` 内容。已有 Claude 正文先内容保全迁入共享源，再由薄入口替换。
- Gemini CLI 通过 `GEMINI.md` 薄入口进入；Kimi Code CLI、Cursor、GitHub Copilot、Windsurf 优先直接读取 `AGENTS.md` 与 `.agents/skills/`。
- 团队可同时使用不同工具；不得在共享配置中保存唯一 active tool，成员当前工具与版本只保存在 `.agent-memory/zworkflow/local/`。
- 任务可能修改项目文件且已安装 `project-refactor-queue` 时，只读取其 `references/PROTECTED_FILES.md`；普通任务不要加载 `REFACTOR_QUEUE.md`。
- 任务开始时使用 `team-member-preferences` 解析当前成员昵称；只读取 `.agent-memory/zworkflow/team/MAINTAINERS.md` 和当前成员对应的 `.agent-memory/zworkflow/team/members/<nickname>.md`，不要读取全员规范。
- 项目读取先走 setup 生成或复用的项目速查；zWorkFlow 自有内容存在 `project-context/references/PROJECT-INDEX.md` 时，以该索引作为稳定入口，只在命中“项目概况”行时读取 `project-context/SKILL.md` 正文。任务命中序列化、动画、资源、启动、依赖注入、异步或编辑器扩展时再读取 `project-tooling` 中的相关条目。若拆分会让事实检索、Spec 设计或代码核验重复读取同一来源/脚本，合并为单一负责人；只有上下文独立时才使用专门 Agent。
- 涉及 C# 项目结构、类型/方法定位、候选调用者或改动影响时，若 `codebase-query` 已安装且 PowerShell 7 可用，必须先执行其索引命令收敛候选，再读取命中源码核验。只有工具不可用、执行失败或查询超出契约时才回退 `rg`/原生检索，并说明回退原因。
- 普通开发任务不得以 Change Review、Summary 或 Workbench 状态作为启动前置；全局实现 Ledger 已废止。这些工件只在显式 OpenSpec/zWorkFlow 生命周期或实现验证完成后的进度投影阶段读写。代码索引例外，它必须在 C# 方案设计前用于形成紧凑事实包。
- 相关职责组成一个功能簇时，先连续实现，再统一执行一次后置审查和定向验证；不要按文件或小函数反复审查、重复测试。全量测试仅用于跨系统/L4 里程碑、公共运行契约变化、发布门禁或用户明确要求。
- `openspec-derive-design-specs` 只能在用户显式要求生成或发布时触发；默认读取 `openspec/design-source.json` 中的全部等价来源路径，可重复的显式 `source` 仅临时覆盖本次扫描。
- 统一用户入口为 `设计导入`、`设计导入：<范围>`、`修改<id>: <修改内容>` 和 `检查文档及时性`；导入可追加 `--规则`、`--内容`、`--美术` 且多个参数取并集。`检查文档及时性` 路由到 `inspect-implemented-design-changes`，只重算实现后设计变更状态和摘要，不自动设计导入或创建 Proposal。Codex 必须路由到共享 skill，不向用户暴露工具专属语法。
- `翻译现有Spec`、`翻译现有Spec：中文|英文 [范围]` 与 `同步Spec翻译：中文|英文 [范围]` 路由到 `openspec-translate`。原 OpenSpec 路径是唯一权威内容，`openspec/translations/` 只供显示。
- 用户以非权威语言要求修改已有 Spec/Change 时，先改权威文件并在同一任务增量同步该语言受影响块；直接编辑权威文件或执行生命周期命令不会后台自动翻译，Workbench 通过 hash 要求显式同步。

## Skill 分层

- 架构设计层：`architecture-review`、`workflow-refactor`、`workflow-reflection`、`team-member-preferences`、`openspec-*`、`openspec-intake-gate`、`openspec-derive-design-specs`。
- 项目内容层：`project-context`、`project-architecture`、`project-tooling` 工程能力目录、按需生成的 `project-domain-*`、`project-refactor-queue`、`project-doc-sync`。

在其他项目执行 setup 时，先读取 `setup/adapters/registry.json`，只读分析已有 Agent 工作流并生成多工具能力映射；随后只对已完成共享内容保全和验证的工具专属正文执行薄化，冲突项保持原样。只有进入 Player 构建且直接支撑游戏玩法的项目运行时系统资料，经代码核验后才可增量生成 `system` 正式 Spec；开发工具、真 Architecture 与 Plugin 进入对应 Skill、工作流文档或 `project-tooling`，并按稳定模块边界拆分。
