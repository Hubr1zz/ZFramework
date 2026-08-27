# Project Index

只列已确认的事实来源。Agent 根据任务选择命中行，不把同一大领域下的所有 skill 一次性加载。

| 任务 / 领域 | 首选来源 | 何时读取 |
| --- | --- | --- |
| 项目概况与正式启动 | `../SKILL.md`、`../../../../Assets/GameScripts/GameEntry.cs`、`../../../../Assets/GameScripts/GameLogic/GameApp.cs`、`../../../../Assets/GameScripts/Procedure/ProcedureStartGame.cs` | 需要技术栈、运行入口或组合根时 |
| Unity 与包版本 | `../../../../ProjectSettings/ProjectVersion.txt`、`../../../../Packages/manifest.json` | 版本、包兼容或 Editor 行为敏感任务 |
| 代码实现或修改 | `../../zframework-dev/references/CODE-WORKFLOW.md` | 所有会修改项目 C# 的任务 |
| C# 类型、调用者与影响范围 | `../../codebase-query/SKILL.md` | C# 方案设计、结构理解或修改影响分析 |
| ZFramework API | `../../zframework-dev/SKILL.md` | UI、资源、事件、模块、启动与排障 |
| Luban 配置 | `../../luban-dev/SKILL.md` | 修改表结构、数据或生成流程 |
| 工程模块图 | `../../project-tooling/references/tooling-catalog.json` | 资源、启动、异步、编辑器扩展或模块依赖 |
| 项目 Wiki | `../../../../repowiki/zh/content/index.md` | 需要系统说明或文档同步时 |
| 正式 System Spec | `../../../../openspec/specs/` | 架构设计或公共运行契约任务 |
| Hunting in Darkness 通用玩法与迁移映射 | `../../project-hunting-in-darkness/SKILL.md` | 营地、狩猎、事件、角色、物品或旧路径映射 |
| GameCore / Adapter / View 边界 | `../../project-gamecore/SKILL.md` | 领域规则落点、三层依赖或跨层重构 |
| 战斗领域 | `../../project-combat/SKILL.md` | 战斗流程、Boss、行动卡、攻击管线或战斗事件 |

同一任务可以命中多行，但只读取真实需要的 skill；例如纯营地规则不读取 `project-combat`，普通战斗修复才同时读取 GameCore 边界与 Combat 资料。
