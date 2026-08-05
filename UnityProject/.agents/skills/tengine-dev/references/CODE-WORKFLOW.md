# TEngine Code Workflow

本文件由原项目 `CLAUDE.md` 的有效代码流程迁入，是所有 AI 工具共享的项目代码操作规范。

## 适用范围

生成代码实现设计、修复代码、重构代码或执行任何会修改本项目 C# 的操作时必须读取本文件。
纯问答、只读审查或不涉及框架 API、UI 节点、事件定义和资源路径的单行 L1 修改可以跳过模块参考，但仍须遵守编码红线。

## 第一步：判断任务等级

| 等级 | 判断标准 | 知识查询策略 |
| --- | --- | --- |
| L1 简单 | typo、注释、日志、无框架语义的单行改名 | 直接处理，不加载模块参考 |
| L2 调用 | 调用已知 API、单一模块局部修改 | 读取 `tengine-dev` 命中主题 |
| L3 功能 | 新功能、跨文件修改、新增 UI/资源/事件逻辑 | 读取全部相关主题并核验源码 |
| L4 架构 | 模块设计、系统重构、多模块协作、架构决策 | 读取相关主题、`project-tooling` 条目和正式 System Spec |

不确定时上调一级。同一会话已读取且来源指纹未变化的主题可以复用摘要。

## 第二步：按主题读取共享资料

所有权威路径都位于 `.agents/skills/`，不得读取工具目录中的正文副本。

| 场景 | 必读来源 |
| --- | --- |
| UI 开发 | `tengine-dev/references/ui-lifecycle.md`；复杂 Widget 再读 `ui-patterns.md` |
| 资源加载 | `resource-api.md`；生命周期问题再读 `resource-patterns.md` |
| 事件系统 | `event-system.md`；排错再读 `event-antipatterns.md` |
| 模块使用 | `modules.md` |
| 程序集、启动、DLC/Mod | `assembly-content-workflow.md` 与 `code-map.md` |
| Luban 配置 | `.agents/skills/luban-dev/SKILL.md` 与 `luban-config.md` |
| 命名和编码风格 | `naming-rules.md` |
| Unity/MCP 操作 | `mcp-tools.md`；美术与动画再读 `mcp-visual.md` |

涉及资源、启动、异步、编辑器扩展或其他工程能力时，同时读取
`.agents/skills/project-tooling/references/tooling-catalog.json` 中命中的条目。

## 第三步：源码核验后实现

1. C# 类型、方法、调用者或影响范围查询优先运行 `codebase-query`；不可用或超时时再用 `rg` 与定向源码读取。
2. 参考资料与源码冲突时，以当前代码和程序集事实为准，并在交付中标出差异。
3. 只修改任务需要的文件；不把无关重构混入功能实现。

## 编码红线

1. IO 操作优先使用 `UniTask`；若现有生命周期仍使用 Coroutine，先核验兼容边界，不机械混用。
2. 业务代码通过 `GameModule.XXX` 访问模块；框架组合根和启动注册代码可以直接使用 `ModuleSystem`。
3. `LoadAssetAsync` 必须配对释放；GameObject 优先使用 `LoadGameObjectAsync` 的自动引用管理。
4. `GameLogic`、`GameProto` 是普通 Player 程序集；`HotFix` 只是兼容目录名，C# 变化需要重新构建 Player。
5. 模块间使用 `GameEvent` 解耦；UI 内部事件遵循 `AddUIEvent` 生命周期。

## 规范与代码冲突记录

发现资料过时时，不写工具私有 memory。把可复用的问题记录在
`.agents/skills/tengine-dev/references/problems/problem_YYYY-MM-DD.md`，包含问题现象、来源位置、代码证据和建议修正；具体项目事实同步到相应 reference 或 `project-tooling`。
