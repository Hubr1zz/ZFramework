---
name: inspect-implemented-design-changes
description: 从已选择的独立设计文档包只读检查“已经实现但随后又发生实质修改”的文档。用于用户要求检查实现后设计变更、评估是否需要重新设计导入，或项目 Workbench 显示文档变更账本时；不得自动创建 OpenSpec proposal、执行设计导入或修改设计包。
---

# Inspect Implemented Design Changes

这是项目包侧的可选只读桥接。设计包路径只保存在项目侧本机配置；不得向设计包注入项目路径。

## 检查

1. 从项目侧配置取得用户已选择的设计包根目录；未配置时要求用户选择一次，不猜测路径。
2. 读取 `<document-root>/.design-workflow/implementation-ledger.json`，不写入该文件。
3. 扫描已配置设计来源中的 Markdown，并按相对路径重建简化目录结构；没有账本条目的文件显示未实现 0%。
4. 按 `documentPath` 合并同一文档的实现记录，展示实现状态/百分比、实现后是否修改、变更时间和摘要；当前指纹偏离账本 `currentFingerprint` 且没有新摘要时显示“手动修改”。路径存在时允许打开原文。
5. 需要评估项目影响时，只读取对应文档、其关联正式 Spec 和最少项目事实，输出建议的 `设计导入：<范围>` 命令；由用户明确执行后才能进入 `openspec-derive-design-specs`。

## 边界

- 不自动调用设计导入、propose、apply、sync 或 archive。
- 不修改设计包账本，不把项目绝对路径写入设计包。
- 只有从用户所选路径定位到 `.design-workflow/implementation-ledger.json` 才建立桥接并保存文档包根路径；未找到时保持原绑定并显示失败。
- 账本缺失或为空时显示独立空状态，不把它视为错误或要求安装项目包。
- 账本是审计索引；设计正文和项目正式 Spec 仍分别是各自事实源。
