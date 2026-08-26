# 项目结构索引

先用本页定位；只在任务命中对应区域时读取右侧参考，不要顺序读取全部项目文档。

## 当前权威约束（2026-08-26）

以下约束覆盖旧设计文档、旧 Spec 或现有代码中与之冲突的描述，直到后续经用户明确修改：

- 战役日历配置化：默认日历包含两个季节，每次成功出猎并完成权威回营提交只推进一个季节；完成配置中的全部季节后才进入下一年。未来增减季节只改配置，不改流程代码。
- 新年度事件只在进入新一年时触发一次。出发失败、取消和读档恢复本身不推进季节。
- 日历推荐以 `CampaignCalendarConfig` 和有序 `SeasonDefinition` 列表表达；首期季节只需稳定 ID 与显示名。存档保存 `CalendarId`、`CurrentYear`、`CurrentSeasonIndex`，战役开始后冻结所选日历；`HuntsCompletedThisYear`、`HuntsPerYear` 仅用于兼容迁移。
- `GameManager` 的目标职责仅为战役顶层 FSM、跨阶段事务和启动/关闭，并统一持有 Settlement、Hunt、Showdown 三个阶段管理者。阶段管理者优先由 ZFramework 生命周期管理，不增加平行 MonoBehaviour 权威；Showdown 当前只建立生命周期与接口，不推进玩法。
- 角色、事件、物品、装备、配方和路线只实现每个机制一个可验证的代表案例。未明确差异的内容只保留稳定 ID、入口和空配置，不自行发明数值或效果；美术、音效与演出只留接口。
- 当前实施顺序：完成营地事件到下一次狩猎风险租约，再实现配置化季节/年度循环，最后做有边界的三阶段管理器拆分。

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
