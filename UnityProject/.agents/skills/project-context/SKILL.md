---
name: project-context
description: TEngine Unity 项目速查与低成本路由。用于开始项目任务、判断代码入口、选择共享 skill 和定位文档或工程能力。
---

# TEngine Project Context

先读取 [PROJECT-INDEX.md](references/PROJECT-INDEX.md)，只打开与当前任务相关的来源。

## 已确认概况

- Unity：2022.3 LTS 项目，Unity 根目录为本目录所在项目根。
- 框架：TEngine，使用 YooAsset、UniTask、Luban；`GameLogic` 与 `GameProto` 随 Player 编译。
- 启动：`GameEntry` 启动 Procedure 状态链，最终由 `ProcedureStartGame` 调用 `GameApp.Entrance`。
- 代码修改：必须进入 `.agents/skills/tengine-dev/references/CODE-WORKFLOW.md`。

无法由索引确认的事实必须回到项目源码、asmdef、Package 清单或项目 Wiki 核验。
