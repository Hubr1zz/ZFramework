# Setup Document ↔ Project Bridge

只有同时使用文档包和项目包时才安装本包。

1. 确认文档包存在 `.design-workflow/implementation-ledger.json`；缺失时由文档包 setup 创建空账本，项目包不得替它写入。
2. 把 `.agents/skills/inspect-implemented-design-changes/` 复制到项目仓库的 `.agents/skills/`。
3. 为需要 wrapper 的项目工具创建指向共享 skill 的薄入口。
4. 项目层使用 Agent 工作台选择任意候选目录；向上查找并在候选目录下有限深度查找 `.design-workflow/implementation-ledger.json`，只有找到后才把文档包根路径保存到项目侧本机偏好。未找到时显示失败且不覆盖已有绑定。
5. Workbench 与检查 Skill 只读账本和设计 Markdown，重建简化目录结构，显示实现状态/百分比、实现后是否修改和摘要；无法由账本摘要解释的新指纹变化显示“手动修改”。右上角灯只表示桥接有效。

不安装本包时：

- 文档包继续独立维护文档。
- 项目包继续独立使用 OpenSpec 和实现工作流。

整个桥接使用 Agent 原生文件工具，不要求 Python、Node 或额外 CLI；不得自动调用“设计导入”、创建 proposal、apply 或修改设计包账本。
