# Setup Document Workflow

把本目录内容复制到文档仓库根目录，然后让 Agent 执行：

1. 扫描现有文档、入口、目录、术语、灵感、案例、待办和历史。
2. 将项目现有文档规则融合进 `.agents/skills/document-maintenance/SKILL.md`，不覆盖已有规则。
3. 创建 Claude wrapper；Codex 直接读取 `.agents/skills/`。
4. 保留已有文档、历史和个人规范。
5. 不创建实现账本，也不读取项目路径；正式实现状态、审计基线与路由摘要均由项目包维护。遗留的 `.design-workflow/implementation-ledger.json` 和 `openspec/implementation-ledger.json` 均不再是桥接条件或权威来源。

该包不要求项目代码、OpenSpec、Unity 或 Python，可以独立维护任何 Markdown/Obsidian 文档库。它不登记工程实现基线，也不承担工程实现后的变更审计。
