---
name: openspec-translate
description: 当用户输入“翻译现有Spec”、要求把现有 OpenSpec 内容翻译成中文/英文，或同步已失效翻译时使用。始终以原 OpenSpec 路径为权威，只更新 openspec/translations 下的显示副本与块级哈希 manifest。
---

# Translate Existing OpenSpec

把 OpenSpec 的权威内容增量翻译为团队共享的只读显示副本。翻译不是第二份 Spec，不能作为 Agent 推理、apply、sync、archive、validator 或审批的输入。

## 入口

- `翻译现有Spec`：目标语言取用户当前使用语言；仍无法判断时询问中文或英文。
- `翻译现有Spec：中文|英文 [范围]`：翻译指定语言和可选 Change、Spec、capability 或相对路径。
- `同步Spec翻译：中文|英文 [范围]`：与上面相同，只处理哈希变化或缺失的块。
- `/opsx:translate [zh-CN|en-US] [范围]`：工具 wrapper 入口。

工作台按钮生成的指令 MUST 直接指向当前内容所属的权威文件，而不是标题、段落、JSON 字段或 block id。Change、Spec、capability 等较宽范围可以解析为多个权威文件；对每个命中文件仍分别执行完整性检查。

## 权威与目录

- `openspec/localization.json.generationLanguage` 只决定以后生成权威文件的默认语言：`source` 表示跟随本次设计文档，另支持 `zh-CN`、`en-US`。`specTitles` 以 capability ID 为稳定键保存可选的 `zhCN` / `enUS` 条目显示名；它是 Workbench 引用与列表的显示元数据，不替代 Spec 标题或 ID。
- `openspec/specs/`、`openspec/changes/`、`openspec/drafts/changes/` 下的原路径永远是唯一权威内容。Agent 始终先读并修改这些文件。
- 翻译保存为 `openspec/translations/<target-language>/<source-relative-path>`；共享索引为 `openspec/translations/manifest.json`。两者必须纳入 Git。
- 非权威副本只能翻译人类可读字段。JSON 的 ID、路径、hash、状态、时间、布尔值、依赖边、审批事实和 schema 字段必须与权威文件保持一致。

## 增量流程

1. 读取 `openspec/localization.json`、`openspec/translations/manifest.json` 和用户指定范围内的权威文件。不要读取翻译副本来理解需求或代码。
2. 可选运行：
   `python .agents/skills/openspec-translate/scripts/translation_blocks.py inspect --language <zh-CN|en-US> [--scope <path-or-id>]`
   脚本不可用时，按同一规则比较 SHA-256；不得要求用户安装 Python。
3. 权威文件是翻译与完整性验收的最小公开单位。对每个命中文件检查全部 block，并处理其中所有变化、缺失、新增或译文哈希失效的 block；不得只翻译触发按钮所在的文本块。Block 只用于在同步权威文件时跳过无需处理的部分，以及支持工作台渲染。
4. 对 manifest 中 `sourceHash` 未变化且全部 block 同步的文件不重译。需要同步时，只重译 `changedBlocks`；未变化块必须从现有译文原样保留，避免改写既有译文。
5. Markdown 按 frontmatter、标题、段落、列表/任务、代码块和表格的顺序块处理。保持标题层级、checkbox、代码、链接目标、ID 与路径不变。
6. `change-review.json`、`spec-review.json`、`gaps.json` 等结构化文件只翻译标题、摘要、详情、要求、影响、建议、说明和人工指引等展示文本；结构字段保持不变。
7. 用户要求修改非权威语言内容时，先修改权威文件，再在同一任务中更新目标语言的受影响块；不得只改翻译副本。
8. 写完目标文件后运行：
   `python .agents/skills/openspec-translate/scripts/translation_blocks.py record --language <zh-CN|en-US> --source <authority-relative-path> --target <translation-relative-path>`
   `record` 必须拒绝 block 数量、顺序或类型不完整的部分译文。对每个修改文件刷新 manifest 后再次 `inspect`；任一 block 缺失或失效时文件不得标记为 `current`，目标范围必须没有 stale/missing block。

## 工作台契约

- 工作台按 `openspec/workbench-config.json.currentLanguage` 选择显示语言。
- 当前语言不是权威语言且翻译缺失时，不回退显示权威正文，只提示执行本 skill。
- 翻译/同步按钮只复制“目标语言 + 所属权威文件路径”的文件级指令；block id 不进入公开指令。
- 正式与 Draft Spec 条目按当前语言显示名称并允许分别重命名。权威语言重命名同步原 Spec、review 与依赖 label；非权威语言重命名只更新 `localization.json.specTitles` 中对应语言的显示名。capability ID、路径和依赖键始终不变。
- Spec 列表、依赖树、关系图和可读引用 MUST 统一通过当前语言的条目名称解析；缺少显式名称时，权威语言回退原 Spec 标题，非权威语言优先回退已同步译文标题。
- `sourceHash` 或 `translatedHash` 不匹配时，不渲染旧译文，只提示同步并提供复制命令。
- 非权威 Markdown 在工作台只读；仅 Spec 条目显示名可独立重命名。Agent 修改正文时仍同时维护权威文件和已存在的相关翻译。

## 输出

报告权威语言、目标语言、处理范围、重译块数、复用块数、写入的翻译路径，以及仍缺少翻译的文件。
