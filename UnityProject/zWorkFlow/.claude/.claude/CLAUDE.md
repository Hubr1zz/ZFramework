# CLAUDE.md

请使用中文写提案和回答。

本文件安装到目标项目后作为 Claude Code 入口。执行任何代码、文档、配置、架构或工作流修改前，必须先阅读项目根 `AGENTS.md`，再按其中入口读取 `zWorkFlow/AGENT_WORKFLOW_README.md` 与项目共享 `.agents/`。

## 强制规则

- 会新增或改变功能、外部可观察行为或公共运行契约的非平凡产品功能或运行框架改动先通过 `openspec-intake-gate`；行为保持型重构和实现方案调整即使位于框架代码也不进入。只有用户显式调用 OpenSpec/zWorkFlow 才覆盖此判断。
- 完整 skill 内容只维护在 `.agents/skills/`。
- 项目事实、维护队列和共享工作台状态只维护在对应 `.agents/skills/<功能>/references/`；`.claude/` 不保存正文副本。
- `.claude/skills/` 只保留 wrapper；不要在这里复制完整 skill 正文。
- OPSX slash commands 只是 OpenSpec skills 的 Claude Code 入口；如果用户没有显式要求 CLI artifact，小型实现可只按 OpenSpec 范式工作。
- 任务开始时使用 `team-member-preferences` 解析当前成员昵称；只读取当前成员个人规范文件。
- 纯项目事实问答优先使用只读 `project-query-agent`；架构与 Spec 方案使用 `solution-architect`；代码实现使用 `code-implementer`。
- `openspec-derive-design-specs` 只能在用户显式要求生成或发布时触发；默认读取 `openspec/design-source.json` 中的全部等价来源路径，可重复的显式 `source` 仅临时覆盖本次扫描。
- 统一用户入口为 `设计导入`、`设计导入：<范围>` 和 `修改<id>: <修改内容>`；导入可追加 `--规则`、`--内容`、`--美术` 且多个参数取并集。Claude wrapper 必须路由到共享 skill，不向用户暴露工具专属语法。
- `翻译现有Spec`、`翻译现有Spec：中文|英文 [范围]` 与 `同步Spec翻译：中文|英文 [范围]` 路由到 `.agents/skills/openspec-translate/SKILL.md`；翻译副本只供显示。

## 文档路由

- 架构规范：读取项目实际生成或已有的 `project-architecture` 与 `architecture` 正式 Spec。
- 架构审查：读取 `.agents/skills/architecture-review/SKILL.md`。
- 重构：读取 `.agents/skills/workflow-refactor/SKILL.md`。
- 踩坑沉淀：读取 `.agents/skills/workflow-reflection/SKILL.md`。
- 成员个人规范：读取 `.agents/skills/team-member-preferences/SKILL.md`。
- 设计文档转 Spec：显式读取 `.agents/skills/openspec-derive-design-specs/SKILL.md`。
- 项目内容：按能力映射读取已有等价资料，或按需读取 `project-context`、`project-architecture` 与实际生成的 `project-domain-*`。
- 项目代码修改：先读 `project-context/references/PROJECT-INDEX.md`；若 setup 已迁入领域 `CODE-WORKFLOW.md`，必须先按其分级与资料路由执行。
