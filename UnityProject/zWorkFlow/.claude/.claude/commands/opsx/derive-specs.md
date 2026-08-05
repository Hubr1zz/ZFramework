---
name: "OPSX: Derive Specs"
description: "从项目已配置的设计文档生成暂存 OpenSpec、缺失项和依赖树"
argument-hint: "[--scope \"<范围>\"] [--filter rules|content|art]... [--source \"[ID=]<临时覆盖路径>\"]..."
---

读取并遵守 `.agents/skills/openspec-derive-design-specs/SKILL.md`，执行 Generate。

默认读取 `openspec/design-source.json` 中的全部等价来源；显式 `--source` 可重复并仅覆盖本次输入。路径不声明设计类型，`--filter` 只对跨路径收敛后的候选文本做语义过滤；多个类型取并集，不传时导入规则、内容与美术全部类型。配置缺失或全部失效时才要求用户在 Agent 工作台设置。不得因普通对话隐式扫描设计仓库。外部设计文档只读。
