# Agent 薄入口

本项目已接入 zWorkFlow。工作流制作、setup 与人类说明统一保存在 `zWorkFlow/`；项目根只保留工具发现所需入口和安装后的项目数据。

处理任何非平凡代码、文档、配置、架构或 Agent 工作流修改前，依次读取：

1. `zWorkFlow/AGENT_WORKFLOW_README.md`
2. `zWorkFlow/AGENTS.md`
3. `.agents/README.md`
4. `.agents/skills/project-refactor-queue/references/PROTECTED_FILES.md`（仅在任务可能修改项目文件时）
5. `.agents/skills/team-member-preferences/SKILL.md`；只读取当前成员对应的 `.agent-memory/team/members/<nickname>.md`

项目事实、领域规则、OpenSpec、重构队列和团队规范以项目根 `.agents/`、`.agent-memory/` 与 `openspec/` 为准。这些是当前项目数据；完整通用工作流与分发资产以 `zWorkFlow/` 为准。任务涉及序列化、动画、资源、启动、依赖注入、异步或编辑器扩展时，按 `.agents/skills/project-tooling/SKILL.md` 读取命中的工程能力条目。

## ZFramework 代码任务

生成代码实现设计、修复代码、重构代码或执行任何项目 C# 修改时，必须先读取
`.agents/skills/zframework-dev/references/CODE-WORKFLOW.md`，完成 L1-L4 分级并按主题加载
`.agents/skills/zframework-dev/` 的共享参考。工具专属 `.claude/`、`.codex/` 只作为薄入口，不是项目规范权威源。
