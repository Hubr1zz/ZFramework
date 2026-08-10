# Hunting in Darkness 数据迁移

迁移时间：2026-08-06。

## 活动数据

- 游戏领域资料：`.agents/skills/project-hunting-in-darkness/`、
  `.agents/skills/project-gamecore/`、`.agents/skills/project-combat/`。
- 重构队列：`.agents/skills/project-refactor-queue/references/REFACTOR_QUEUE.md`。
- OpenSpec：`openspec/`。源项目的 Change、Draft、正式 Spec、翻译和元数据已合入；
  `tengine-startup-lifecycle` 保持为目标项目已有正式 Spec。
- 当前成员映射与个人规范：`.agent-memory/team/`。

## 原样档案

本目录保存源项目数据的原样副本：

- `.agent-memory/`：安装状态、团队数据和可重建的代码索引。
- `openspec/`：包含本机设计源、Workbench 布局、旧 GUID/路径证据的完整副本。
- `source-project-skills/`：原 project-* 项目内容层资料和工程能力目录。

原样档案不是目标项目的权威运行数据；它用于审计、恢复和核对迁移遗漏。
旧 GUID、绝对路径和代码索引允许失效，活动数据以目标工程路径为准。

游戏脚本的架构判断、逐文件调整内容、验证结果和仍需手工连接的资源见
`SCRIPT_MIGRATION.md`。
