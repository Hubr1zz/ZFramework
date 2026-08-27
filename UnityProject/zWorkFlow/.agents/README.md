# .agents

`.agents/` 是本工作流包的共享规范源。

- `skills/`：完整 skill 内容；Codex、Cursor、GitHub Copilot、Gemini CLI、Windsurf、Kimi Code CLI 等支持该开放路径的工具直接扫描。
- `skills/tengine-rts-development/`：RTS Session、Roslyn 热更新与增量正式化的设计门禁；它与其他 Unity 项目 skills 共用本目录，不在仓库外层维护副本。
- `agent-roles/`：跨 AI 工具共享的 agent 角色说明。
- `skills/<功能>/references/`：该功能的完整规则、项目事实和可变工作台数据；保护清单与待处理重构项分别位于 `project-refactor-queue/references/PROTECTED_FILES.md`、`REFACTOR_QUEUE.md`，按需读取。
- 项目读取先走 `project-context/references/PROJECT-INDEX.md`，再按需打开一个参考小节与目标脚本。
- Agent 拆分是可选项：若事实检索与方案设计会重复读取同一来源/代码，由单一负责人完成；只有上下文不重叠时才拆分。
- `project-query-agent` 可做独立、只读事实检索；`solution-architect` 可处理独立架构/Spec；`code-implementer` 负责实现。
- 未知结构的 C# 任务先由执行 Agent 用 `codebase-query context` 形成一次性事实包，再交给方案 Agent 和实现复用；准确文件/符号已知且无需关系分析时直接定点读取。不要让多个 Agent 重复查询或读取同一批源码。
- Change Review、Summary 和 Workbench 状态是实现后的进度投影，不是普通开发启动依赖；全局实现 Ledger 已废止。相关职责按功能簇完成后统一审查与定向验证；全量测试只用于跨系统/L4 里程碑、公共契约、发布门禁或显式要求。
- `openspec-derive-design-specs` 只允许显式调用。
- `codebase-query` 是 C# 项目的条件能力：未知结构、调用者与影响查询先用本地派生索引缩小候选，再以返回的行范围核验源码；准确目标的单点核验可直接使用 `rg`/源码读取。缓存不是项目事实或决策权威源。
- `project-tooling` 保存 Git 同步的 Plugin/Architecture/System 工程能力目录；实现任务只读取命中条目。Plugin 判断依据为空时按代码风格判断，Architecture 固定 required/locked 且修改前需用户确认。
- 工程能力按稳定模块边界拆分；README/Wiki 与目录、接口或 asmdef 能共同证明 Resource/Event/Config/UI/Procedure 等模块时分别建条目，不能只保留总框架节点。
- 从旧 `CLAUDE.md`、`.claude/skills/` 或 `.codex/skills/` 迁入的项目代码流程和完整 Skills 以本目录为唯一权威源；工具目录只保留 wrapper。
- `openspec-translate` 只维护 `openspec/translations/` 中的中英文显示副本与块级 hash；所有 Agent 和生命周期命令仍读取原 OpenSpec 权威路径。

不要在任何工具专属目录维护第二份完整 skill、项目事实或工作台状态；专属目录只允许入口、命令、配置和薄 wrapper。
