# Claude Code 薄入口

请使用中文写提案和回答。

执行任务前读取项目根 [AGENTS.md](./AGENTS.md)。完整项目 Skills、代码规范、工程能力和持久化状态只维护在 `.agents/`、`.agent-memory/` 与 `openspec/`；本文件不保存第二份正文。

生成或修改 ZFramework C# 时，必须按 `AGENTS.md` 路由到：

- `.agents/skills/zframework-dev/references/CODE-WORKFLOW.md`
- `.agents/skills/zframework-dev/SKILL.md`
- `.agents/skills/project-tooling/references/tooling-catalog.json` 中命中的模块

Claude Code 的 skills、commands 和 agents 只允许作为薄 wrapper 指向共享源。
