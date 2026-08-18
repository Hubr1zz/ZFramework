---
name: zframework-dev
description: 基于当前 ZFramework 源码的 Unity 游戏开发指导。生成或修改本项目 C# 代码时必须使用；也用于 ZFramework 架构、程序集、启动流程、GameModule、UIWindow/UIWidget、GameEvent、YooAsset 资源与 DLC/Mod Package、UniTask、Luban 配置和构建排障任务。
---

# ZFramework 开发指导

ZFramework 是基于 YooAsset + UniTask + Luban 的 Unity 游戏框架。业务代码作为普通 Player 程序集编译。
本 skill 提供 AI 专用的精炼参考文档，确保生成的代码与框架 API 完全一致。

## 代码任务强制入口

任何生成代码实现设计、修复代码或修改项目代码的任务，先读取
[CODE-WORKFLOW.md](references/CODE-WORKFLOW.md)，完成任务等级判断，再按其中路由只读取命中的参考文件。
L1 可以不加载模块参考；L2-L4 不得跳过对应主题资料与源码核验。

## 核心红线

1. **异步优先**：IO 操作用 `UniTask`，禁止同步加载/Coroutine
2. **模块访问**：通过 `GameModule.XXX` 访问，而非 `ModuleSystem.GetModule<T>()`
3. **资源必须释放**：`LoadAssetAsync` 对应 `UnloadAsset`，GameObject 用 `LoadGameObjectAsync`
4. **程序集边界**：`GameLogic`、`GameProto` 是普通 Player 程序集；C# 变化必须重新构建 Player
5. **事件解耦**：模块间用 `GameEvent`，UI 内部用 `AddUIEvent`

## 文档路由

根据任务类型，读取对应的 reference 文档：

| 任务类型 | 必读文档 | 进阶文档 | 优先级 |
|---------|---------|---------|--------|
| UI 开发 | [ui-lifecycle.md](references/ui-lifecycle.md) | [ui-patterns.md](references/ui-patterns.md) | P0 |
| 事件系统 | [event-system.md](references/event-system.md) | [event-antipatterns.md](references/event-antipatterns.md) | P0 |
| 资源加载 | [resource-api.md](references/resource-api.md) | [resource-patterns.md](references/resource-patterns.md) | P0 |
| 模块使用 | [modules.md](references/modules.md) | — | P0 |
| 程序集、启动入口、DLC/Mod | [assembly-content-workflow.md](references/assembly-content-workflow.md) | [code-map.md](references/code-map.md) | P0 |
| 代码规范 | [naming-rules.md](references/naming-rules.md) | — | P1 |
| Luban 配置 | [luban-config.md](references/luban-config.md) | — | P1 |
| 项目结构 | [architecture.md](references/architecture.md) | — | P2 |
| 问题排查 | [troubleshooting.md](references/troubleshooting.md) | — | P2 |
| MCP 场景/GO/UI/脚本/Editor | [mcp-tools.md](references/mcp-tools.md) | — | P1 |
| MCP 材质/Shader/动画/VFX | [mcp-visual.md](references/mcp-visual.md) | — | P2 |

`code-map.md` 由脚本生成。涉及当前类名、程序集、Procedure 或包版本时优先读取它；架构变化后运行：

```bash
python .agents/skills/zframework-dev/scripts/generate_architecture_docs.py
```
