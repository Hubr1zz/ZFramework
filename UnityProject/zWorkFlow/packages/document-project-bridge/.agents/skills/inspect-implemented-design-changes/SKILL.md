---
name: inspect-implemented-design-changes
description: 从项目配置的全部 Markdown 设计文档路径检查“已经实现但随后又发生实质修改”的文档。用于用户要求检查实现后设计变更、评估是否需要重新设计导入，或项目 Workbench 显示项目账本时；不得自动创建 OpenSpec proposal、执行设计导入或修改文档包。
---

# Inspect Implemented Design Changes

这是项目包侧的可选桥接。设计文档路径只保存在项目侧本机配置；实现账本由项目包维护，不得向文档包注入项目路径或工程状态。

## 检查

1. 从项目侧 `openspec/design-source.json` 取得用户配置的全部设计文档路径；未配置时要求用户添加路径，不猜测位置。至少一个目录有效即可建立桥接，不要求特殊工作流文件。
2. 读取并由项目包维护 `<project-root>/openspec/implementation-ledger.json`；字段契约见 [implementation-ledger-schema.md](references/implementation-ledger-schema.md)。账本只保存稳定 `sourceId` 和相对于该来源的 `documentPath`，不保存绝对路径。
3. 递归扫描全部来源中的 Markdown，每个来源作为独立顶层节点，按相对路径重建可折叠目录结构；刷新同时发现新增/删除文件并重算指纹，没有账本条目的新文件显示未实现 0%。
4. 按 `(sourceId, documentPath)` 合并同一文档的实现记录，展示实现状态/百分比、实现后是否修改、变更时间和摘要；当前指纹偏离账本 `currentFingerprint` 且没有新摘要时显示“手动修改”。路径存在时允许打开原文。
5. 需要评估项目影响时，只读取对应文档、其关联正式 Spec 和最少项目事实，输出建议的 `设计导入：<范围>` 命令；由用户明确执行后才能进入 `openspec-derive-design-specs`。

## 边界

- 不自动调用设计导入、propose、apply、sync 或 archive。
- 不修改文档包，不把项目绝对路径写入文档包。
- 至少一个已配置来源目录有效时建立桥接；空目录仍可显示为来源节点，新建 Markdown 后由刷新发现。
- 项目账本缺失时由项目包创建空账本；为空时显示独立空状态，不把它视为文档包错误或要求安装文档工作流。
- 账本是项目审计索引；设计正文和项目正式 Spec 仍分别是各自事实源。
