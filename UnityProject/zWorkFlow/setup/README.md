# zWorkFlow Setup

本目录用于把 zWorkFlow 适配到任意项目。setup 先只读分析已有 Agent 工作流，再把可验证的项目规则与完整 Skills 内容保全迁入共享 `.agents/` 并薄化工具入口；冲突或无法证明等价的内容保持原样。zWorkFlow 同时复用已有能力、跳过重复流程，并仅在未占用路径补充缺失能力。

## 使用流程

1. 在项目根运行 `npx --yes github:Hubr1zz/zWorkFlow setup`；也可手工把完整移植包以 `zWorkFlow/` 文件夹名放入项目。两种方式都保持独立安装源，不摊平覆盖项目文件。
2. 明确要求 AI：“读取 `zWorkFlow/setup/SETUP_NEW_PROJECT.md` 并执行 setup”。
3. AI 先校验 `PACKAGE_MANIFEST.json` 与 OpenSpec CLI；缺失或过旧时自动安装兼容版本并复验，再逐文件安装未占用的通用核心。
4. AI 扫描新项目事实并生成项目专属内容 skills；只有 OpenSpec CLI 验证成功，且检测到 Unity、没有同类界面时才成套安装工作台。
5. AI 生成能力映射和精简路由摘要；冲突路径跳过，可无损迁入的工具专属正文经校验后替换为薄入口。

架构资料或 Agent 工作流后导入时，用户再次要求检测即可；setup 只处理新增或变化来源，不重新初始化。

## 文件说明

- [SETUP_NEW_PROJECT.md](SETUP_NEW_PROJECT.md)：setup 主流程与不干扰边界。
- [CLI_BOOTSTRAP.md](CLI_BOOTSTRAP.md)：GitHub `npx` 下载、OpenSpec 验证与 Agent setup 的两阶段边界。
- [ARCHITECTURE_SPEC_DISCOVERY.md](ARCHITECTURE_SPEC_DISCOVERY.md)：项目系统资料识别、代码核验与 System Spec 生成（文件名为旧版兼容保留）。
- [AGENT_WORKFLOW_COEXISTENCE.md](AGENT_WORKFLOW_COEXISTENCE.md)：已有 Agent 工作流能力映射、冲突提示和轻量路由缓存。
- [AI_TOOL_ADAPTERS.md](AI_TOOL_ADAPTERS.md)：Codex、Claude Code、Cursor、GitHub Copilot、Gemini CLI、Windsurf、Kimi Code CLI 的声明式适配与多工具团队策略。
- [REUSABLE_WORKFLOW_STRUCTURE.md](REUSABLE_WORKFLOW_STRUCTURE.md)：可复用能力结构。
- [PROJECT_CONTENT_TEMPLATE.md](PROJECT_CONTENT_TEMPLATE.md)：无等价能力时才使用的通用项目上下文模板。
- [QUALITY_AUDIT_CHECKLIST.md](QUALITY_AUDIT_CHECKLIST.md)：只读质量与成本审计。
- [SETUP_OUTPUT_CONTRACT.md](SETUP_OUTPUT_CONTRACT.md)：setup 交付格式。
- [UNITY_WORKBENCH_INTEGRATION.md](UNITY_WORKBENCH_INTEGRATION.md)：Unity 项目完整 setup 的标准工作台；已有同类界面、路径冲突或非 Unity 项目时跳过。
- [FEATURE_COVERAGE.md](FEATURE_COVERAGE.md)：当前分发包功能、资产与平台兼容覆盖清单；发布和 setup 验收时逐项核对。
- [PACKAGE_MANIFEST.json](PACKAGE_MANIFEST.json)：分发包必需资产、通用 skill 白名单和禁入内容契约。

## 设计边界

- 完整 setup、人类文档和分发资产始终留在 `zWorkFlow/`；项目根只安装工具发现薄入口、项目共享 `.agents/`/OpenSpec/团队状态，以及平台运行必需文件。
- 通用核心不预设项目领域；Unity 工作台只在检测到 Unity 项目时安装。
- OpenSpec CLI 要求 Node.js `>=20.19.0` 和 OpenSpec `>=1.6.0 <2.0.0`。完整 setup 会自动安装缺失或过旧的兼容 1.x；Node 缺失、CLI 安装失败或版本不兼容时阻止工作台安装。
- `codebase-query` 在 Unity 项目的 `Assets` 或显式项目内 source roots 含 C# 且 PowerShell 7+（`pwsh`）可用时安装；不依赖个人的 `Assets/Scripts` 目录结构，并在 Windows/macOS 使用同一份 UTF-8 可移植索引。缺少运行时时，setup 仅在用户许可后尝试安装，否则安全回退。
- setup 会安装“权威生成语言 + 翻译现有Spec”能力：原 OpenSpec 路径始终唯一权威，Git 共享的中英文副本只供 Workbench 显示并按块 hash 增量同步；Spec 条目名称按 capability ID 分别保存中英文显示值。
- 不覆盖冲突入口、skills、commands、agents、自动化或历史数据；只有已完成内容保全和验证的工具专属正文可以薄化。
- “融合”包括把可验证的项目规则与完整 Skills 迁入共享源并薄化工具入口；不做无证据的文本拼接或静默选边。
- 同类能力优先复用，zWorkFlow 跳过重复步骤。
- 架构资料与 Agent 工作流资料走两条独立检测流程。
- 除 OpenSpec CLI 官方要求的 Node.js 外，不要求额外运行时；非 CLI 的 Agent 原生流程仍可用。
- 同一仓库可同时保留多种 AI 工具适配；成员的当前工具与版本只保存在本地忽略文件中。
