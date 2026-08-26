---
name: project-context
description: ZFramework Unity 项目的低成本事实入口与 skill 路由。用于开始非平凡项目任务、选择领域资料、代码流程和工程能力；不替代源码、项目清单或正式 Spec。
---

# ZFramework Project Context

本 skill 由根 `AGENTS.md` 的最小项目读取规则调用；普通开发不需要先加载完整 zWorkFlow 说明。先读取 [PROJECT-INDEX.md](references/PROJECT-INDEX.md)，只选择当前任务命中的行；每个命中的 `project-*` skill 再按自身“必读参考”打开最少来源。未命中的领域资料、Agent 角色文件和实现进度摘要不读取。

## 已确认概况

- Unity：Unity 6；`ProjectSettings/ProjectVersion.txt` 当前为 `6000.5.9f1`，版本敏感任务必须重新读取该文件。
- 框架：ZFramework 基于 TEngine 扩展，项目内使用 YooAsset、UniTask 与 Luban 工具链；具体版本和安装位置以项目清单、asmdef 与源码为准。
- 正式启动：`GameEntry.Awake` 启动 Procedure 状态链，`ProcedureStartGame` 调用 `GameApp.Entrance`，再由 `PlayableGameBootstrap.EnsureInstalled` 在配置的入口场景装配可玩内容与 `GameManager`。
- 代码修改：必须进入 `.agents/skills/tengine-dev/references/CODE-WORKFLOW.md`。

这些概况只用于路由，不作为长期权威副本。无法由索引确认或可能随版本变化的事实必须回到源码、asmdef、`ProjectVersion.txt`、Package 清单或项目 Wiki 核验。
