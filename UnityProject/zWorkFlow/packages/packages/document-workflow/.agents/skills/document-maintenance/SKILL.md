---
name: document-maintenance
description: 维护独立项目的设计文档、知识库、术语、灵感、案例、待办、修改历史与实现后变更账本。用于读取、创建、整理、审阅或同步 Markdown/Obsidian 文档，以及登记设计已实现基线、检查或标记实现后发生的设计变更；不依赖代码项目或 OpenSpec。
---

# Document Maintenance

本 skill 是独立文档包的完整流程源。先根据项目事实调整目录名称和文档规则，不要假设特定游戏或框架。

## 核心流程

1. 读取文档包入口、目录地图和目标文档。
2. 用 `rg --files` 与 `rg` 搜索相关正式文档、术语、灵感和案例。
3. 区分：
   - 已确认正式设计
   - 草稿 / 灵感
   - 稳定术语
   - 内容案例
   - 未解决问题
4. 实质修改正式设计前，审查设计完整性与实现清晰度；缺少核心行为时先询问用户。
5. 编辑后维护链接、待办和内容修改历史。纯工作流改动不写内容历史。
6. 不覆盖已有文档、个人规则或历史；冲突时列出差异。

## 实现后变更账本

账本位于 `.design-workflow/implementation-ledger.json`，字段契约见 [implementation-ledger-schema.md](references/implementation-ledger-schema.md)。它是设计包拥有的审计索引，不是设计正文，不保存项目绝对路径。

1. 可以在用户或可信实现记录提供进度时，为 `(documentPath, implementationId)` 更新 `implementationStatus` 与 `implementationProgress`；百分比限制为 0-100。没有记录的文档由项目端显示为未实现 0%，不得猜测进度。
2. 仅在用户明确说明某份正式设计已经实现，或提供可信的实现完成记录时登记实现基线。把状态设为 `implemented`、进度设为 100，并按 schema 的文本归一化规则记录内容 SHA-256、Git revision（可用时）和时间；不得仅因文档存在就猜测已经实现。
3. 成功实质修改正式设计后，检查账本中相同 `documentPath` 的所有实现基线。当前 SHA-256 与 `implementedFingerprint` 不同时，更新 `currentFingerprint` / `currentRevision`，设置 `changedAfterImplementation=true`，并记录 `changedAt` 与简短 `changeSummary`。
4. 纯格式、链接修复、目录索引、工作流配置或历史整理不标记为实现后设计变更；无法判断是否影响设计语义时，在 `changeSummary` 中注明待人工确认。绕过文档工作流的指纹变化由项目端显示为“手动修改”。
5. 用户确认新设计已经重新实现后，为同一 `implementationId` 重置实现基线并把 `changedAfterImplementation` 设回 false；保留 `previousImplementedFingerprint` 供审计。
6. 文档包不得根据账本主动读取项目目录、调用“设计导入”、生成 OpenSpec proposal 或修改项目代码。

文档包在没有桥接 skill、项目包或 Unity 时必须能完整独立工作。
