# Setup Document ↔ Project Bridge

只有同时使用文档包和项目包时才安装本包。

1. 把 `.agents/skills/inspect-implemented-design-changes/` 复制到项目仓库的 `.agents/skills/`。
2. 为需要 wrapper 的项目工具创建指向共享 skill 的薄入口。
3. 项目包不创建全局实现 Ledger。正式实现事实位于 capability 的 `implementation.json`，审计基线位于 `implementation-audit.json`，本机候选位于 Git 忽略的 `.agent-memory`。
4. 项目层使用 Agent 工作台添加任意数量的设计文档路径；这些路径及稳定 `sourceId` 只保存为项目侧本机偏好。文档库无需安装本包或持有特殊文件，也不再另设“设计文档目录”。
5. Workbench 递归扫描所有来源，并只在 `implementation-summary.json` 的输入摘要校验有效时投影关联 capability 的实现状态；索引缺失或过期时显示待刷新，不猜测完成度。右上角灯表示至少一个来源目录有效。

不安装本包时：

- 文档包继续独立维护文档；任意 Markdown 仓库均可作为桥接来源。
- 项目包继续独立使用 OpenSpec 和实现工作流。

整个桥接使用 Agent 原生文件工具，不要求 Python；不得自动调用“设计导入”、创建 proposal 或 apply。只有用户明确要求检查进度时才刷新派生摘要；实现后的语义变化必须进入 Change 或直接实现收养流程。
