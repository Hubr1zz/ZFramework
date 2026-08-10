# 项目结构索引

先用本页定位；只在任务命中对应区域时读取右侧参考，不要顺序读取全部项目文档。

| 需要了解的内容 | 代码入口 | 再读取 |
| --- | --- | --- |
| 顶层目录、命名空间、当前限制 | `Assets/Scripts/` | `PROJECT-QUICKSTART.md` 对应小节；需要范围/限制时读 `PROJECT-README.md` |
| 跨阶段组合、场景、存档、EventBus | `Assets/Scripts/Core/`、`GameplayBase/` | `ARCHITECTURE.md` 对应小节 |
| GameCore / Adapter / ViewLayer 依赖 | `Assets/Scripts/GameCore/`、`Adapters/Unity/`、`ViewLayer/` | `../../project-gamecore/references/GAMECORE.md` |
| 战斗、攻击、部位、时点、Boss 卡桌 | `GameCore/Combat/`、`Adapters/Unity/Combat/`、`ViewLayer/Combat/`、`UI/BossCardTable.cs` | `../../project-combat/references/COMBAT.md` 对应小节 |
| 营地与狩猎 | `Adapters/Unity/{Settlement,Hunt}/`、`ViewLayer/{Settlement,Hunt}/` | `SETTLEMENT_HUNT.md` |
| Plugin、Architecture、System 工程能力 | `Packages/`、`Assets/Plugins/` 与命中程序集 | `../../project-tooling/SKILL.md`；只读取任务命中的目录条目 |
| Agent/OpenSpec 工作流 | `zWorkFlow/`、`.agents/skills/`、`openspec/`、`Assets/Scripts/Editor/AgentWorkbench*` | `zWorkFlow/AGENT_WORKFLOW_README.md` 和命中的 skill |
| 跨模块符号、候选调用者、改动影响快速定位 | `Assets/**/*.cs` 与配置的项目内源码根 | `../../codebase-query/SKILL.md`；可用时必须先查索引，命中后再读目标领域参考与源码 |

检索顺序：索引 → 目标参考的小节 → `rg --files`/`rg` 找脚本 → 只读命中脚本。只有跨系统决策才升级到完整 `ARCHITECTURE.md`。
