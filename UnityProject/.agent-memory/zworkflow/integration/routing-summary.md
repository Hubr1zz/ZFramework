# zWorkFlow Routing Summary

> 更新于 2026-07-29。来源指纹未变化时，后续任务优先读取本摘要。

## 共享权威源

- 项目入口：AGENTS.md。
- 项目速查：.agents/skills/project-context/references/PROJECT-INDEX.md。
- ZFramework 代码任务：.agents/skills/zframework-dev/references/CODE-WORKFLOW.md。
- 工程模块：.agents/skills/project-tooling/references/tooling-catalog.json。
- 所有完整 Skills 与角色：.agents/skills/、.agents/agent-roles/。

## 工具入口状态

- Codex：直接读取 AGENTS.md 与 .agents/skills/；.codex/skills 不再保存正文。
- Claude Code：根/.claude/CLAUDE.md、skills、commands、agents 均为薄入口。
- Claude 的 settings.local.json 保持原样。
- 原工具正文保存在 Git 忽略的本地恢复备份，不参与能力路由。

## 代码修改强制流程

生成实现、修复、重构或修改项目 C# 时，先读取 CODE-WORKFLOW.md，判断 L1-L4，再按主题读取 zframework-dev references。涉及工程模块时同时读取 tooling catalog；参考与代码冲突时以源码为准。

## 工程能力拆分

- Plugin：UniTask、YooAsset、MCP for Unity。
- Architecture：ModuleSystem、ResourceModule、GameEvent、MemoryPool、ObjectPoolModule、UIModule、ConfigSystem、ProcedureModule、ZFramework 启动生命周期。
- System：ZFramework 项目启动接入（Launcher UI、更新/内容包流程与 GameApp 交接）。
- ConfigSystem 当前为 partial：文档和 LubanLib 存在，但生成的 ConfigSystem/GameConfig 代码未落盘。

## 冲突

- 当前无未决工作流冲突。旧 HybridCLR 规则已由代码与新版 Claude 资料核验后淘汰。
- 现有 Coroutine 与异步优先规范的差异已改为“核验现有生命周期边界”，不再形成互相矛盾的双重规则。

## 来源指纹

- 结构化指纹和迁移清单见 workflow-map.json。
- 本机发现缓存与可恢复备份位于 .agent-memory/zworkflow/local/。
