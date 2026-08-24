# Project Workflow Package

`zWorkFlow/` 根目录本身就是可独立迁移的安装包。把它作为 `zWorkFlow/` 放到新项目根目录后，让 Agent 读取 `setup/SETUP_NEW_PROJECT.md` 并执行 setup；安装源保持独立。Agent 只把工具发现、项目数据和 Unity 编译所需内容安装到项目根，完整 setup、人类说明和分发资产继续由 `zWorkFlow/` 统一管理。

- setup：`setup/SETUP_NEW_PROJECT.md`
- 多工具适配：`setup/AI_TOOL_ADAPTERS.md` 与 `setup/adapters/registry.json`
- 通用工作流核心：清单列出的 `.agents/skills/` 与 `.agents/agent-roles/`
- C# 查询加速：Unity 项目的 `Assets` 或配置源码根含 C# 且 PowerShell 7+ 可用时安装 `codebase-query`；Windows/macOS 使用 UTF-8 可移植索引，不满足时安全回退
- 项目事实与架构：setup 生成 `project-context`、`project-architecture` 与 `project-domain-*`，分发包不预置任何具体项目内容
- OpenSpec 与实现门禁：`.agents/skills/openspec-*`
- 工作台：按 `setup/UNITY_WORKBENCH_INTEGRATION.md` 接入
- 功能覆盖与平台验收：`setup/FEATURE_COVERAGE.md`
- 人类文档：`WORKFLOW_OVERVIEW.md`、`WORKFLOW_QUICKSTART.md`、`WORKFLOW_DEVELOPER_GUIDE.md`；setup 校验三份完整并由 Workbench 直接读取，不复制到项目根

不安装 `packages/document-project-bridge/` 时，项目层仍可通过对话手动 propose/apply/sync/archive，并可显式从任意设计文档路径生成基线 Spec。apply 保持 Change 沙箱，sync 使用 base/current/Delta 判断安全合并，完成 Tasks 并同步后可从 Workbench 归档。

安装 bridge 后，项目 Workbench 将全部“设计文档路径”保存为本机绑定。每个来源作为独立顶层节点，递归扫描 Markdown 并重建可折叠结构；实现进度只从校验有效的 `implementation-summary.json` 路由索引投影，索引由正式 `implementation.json` 和 active Change 派生。右上角灯只表示至少一个来源有效，项目端不写回设计包或自动触发设计导入。

同一团队可并行使用 Codex、Claude Code、Cursor、GitHub Copilot、Gemini CLI、Windsurf 与 Kimi Code CLI；完整 Skills 只维护在 `.agents/skills/`，工具专属目录只提供薄适配。
