# Setup Document ↔ Project Bridge

只有同时使用文档包和项目包时才安装本包。

1. 把 `.agents/skills/inspect-implemented-design-changes/` 复制到项目仓库的 `.agents/skills/`。
2. 为需要 wrapper 的项目工具创建指向共享 skill 的薄入口。
3. 项目包在 `openspec/implementation-ledger.json` 幂等创建并维护账本；它属于项目审核索引，不保存文档库绝对路径。
4. 项目层使用 Agent 工作台添加任意数量的设计文档路径；这些路径及稳定 `sourceId` 只保存为项目侧本机偏好。文档库无需安装本包或持有特殊文件，也不再另设“设计文档目录”。
5. Workbench 读取项目账本并递归扫描所有来源，每个来源重建为独立、可折叠的顶层结构。刷新同时发现新增/删除 Markdown 并重算指纹，新文件默认 0%；无法由账本摘要解释的新指纹变化显示“手动修改”。右上角灯表示至少一个来源目录有效。

不安装本包时：

- 文档包继续独立维护文档；任意 Markdown 仓库均可作为桥接来源。
- 项目包继续独立使用 OpenSpec 和实现工作流。

整个桥接使用 Agent 原生文件工具，不要求 Python、Node 或额外 CLI；不得自动调用“设计导入”、创建 proposal 或 apply。只有用户明确下达“检查文档及时性”或“检查需求变化”，且确认没有语义变化时，项目侧 Agent 才能更新项目账本的当前指纹。
