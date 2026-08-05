# Setup Document Workflow

把本目录内容复制到文档仓库根目录，然后让 Agent 执行：

1. 扫描现有文档、入口、目录、术语、灵感、案例、待办和历史。
2. 将项目现有文档规则融合进 `.agents/skills/document-maintenance/SKILL.md`，不覆盖已有规则。
3. 创建 Claude wrapper；Codex 直接读取 `.agents/skills/`。
4. 保留已有文档、历史和个人规范。
5. 幂等创建 `.design-workflow/implementation-ledger.json`；已有账本保持不变。旧 `.agent-bridge/project-sync.json` 不再使用，不读取其中项目路径，也不据此触发项目工作流。

该包不要求项目代码、OpenSpec、Unity 或 Python，可以独立维护任何 Markdown/Obsidian 文档库，并可独立登记设计实现基线与实现后变更。
