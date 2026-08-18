# Project Implementation Ledger Schema

`<project-root>/openspec/implementation-ledger.json` 是项目包拥有的 schemaVersion 2 审计索引，用于记录全部已配置 Markdown 来源中的设计实现基线与实现后变更。它不保存文档库绝对路径，也不触发目标工作流。

`sourceId` 来自 `openspec/design-source.json`，`documentPath` 相对于该来源根目录并使用 `/`；以 `(sourceId, documentPath, implementationId)` 为唯一键。schemaVersion 1 的无 `sourceId` 条目仅在相对路径跨来源唯一时兼容。`implemented*` 是最近确认实现完成的基线，`current*` 是项目侧最近核验状态，`changedAfterImplementation` 只在基线后发生实质设计变化时为 true。文本指纹以 UTF-8 读取、CRLF 归一为 LF、去除首尾空白后计算小写 SHA-256。

项目 Workbench 的“刷新文档状态”会重扫文件结构并重算、显示哈希结果；只有用户明确要求“检查需求变化”且确认没有语义变化时，项目侧 Agent 才更新 `current*` 字段。
