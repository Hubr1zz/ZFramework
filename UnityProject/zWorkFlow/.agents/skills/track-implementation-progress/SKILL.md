---
name: track-implementation-progress
description: 在实现与验证完成后维护 zWorkFlow 的可验证进度路由索引，或在用户显式要求检查实现覆盖时生成本地候选。不得作为普通开发任务的启动前置；只生成候选，不把未验证代码自动标记为完成。
---

# Track Implementation Progress

把正式 OpenSpec implementation facts 与代码证据投影为 Workbench 可查询状态；不参与代码方案决策，也不要求下一次普通开发先读取这些产物。

## 稳定入口

在项目根目录运行：

```powershell
& .agents/skills/track-implementation-progress/scripts/run.ps1 refresh
& .agents/skills/track-implementation-progress/scripts/run.ps1 validate
& .agents/skills/track-implementation-progress/scripts/run.ps1 query -Attention
& .agents/skills/track-implementation-progress/scripts/run.ps1 query -Slice capability -Capability hunt-map-generation
& .agents/skills/track-implementation-progress/scripts/run.ps1 query -Slice path -Path Assets/GameScripts/GameLogic
& .agents/skills/track-implementation-progress/scripts/run.ps1 discover
```

- `refresh` 从 `openspec/specs/<capability>/implementation.json`、正式 Spec 标题、依赖元数据和代码证据生成 Git 管理的 `openspec/implementation-summary.json`。
- `validate` 重新计算输入清单与摘要 digest；任一输入缺失或变化都会 fail-closed，不会自动刷新。
- `query` 在通过验证后按 attention、capability 或 evidence path 返回切片。
- `discover` 只写 Git 忽略的 `.agent-memory/zworkflow/local/implementation-discovery.json`，不覆盖摘要。
- `checkpoint` 在候选已处理且摘要有效时，把当前 Git revision 写入 `openspec/implementation-audit.json`。

详细字段与摘要状态规则见 [IMPLEMENTATION-SCHEMA.md](references/IMPLEMENTATION-SCHEMA.md)。

## 触发边界

- 普通代码任务启动时不运行本 skill，也不读取 `implementation-summary.json`。
- 只在功能簇实现与验证完成后、用户显式刷新/检查 Workbench 进度时，或显式 OpenSpec apply/sync 生命周期要求更新证据时运行。
- 需要理解代码时使用 C# 派生索引和命中源码；摘要与本地候选不能替代代码事实，也不能反向扩大实现范围。

## 正式实现事实

- 每个正式能力在 `openspec/specs/<capability>/implementation.json` 中保存实现状态、验证状态、摘要和带规范化 SHA-256 的代码证据。
- `implementation.json` 是实现事实唯一输入；`spec-review.json`、旧 `implementation-ledger.json` 和摘要不会被该 skill 读取或写入。
- 没有 `implementation.json` 的能力显示为 `unknown`，不能因为文件存在、关系树 readiness 或 C# 索引命中而自动标记完成。
- `verified` 必须绑定当前 Spec hash，并至少有一项当前代码证据；绑定错误、证据缺失、缺 hash 或 hash 不一致会以 `stale` 出现在有效摘要的 attention 切片中。`validate` 校验的是摘要完整性与可复现性，不会把“存在待处理项”和“索引损坏”混为一谈。

## 绕过工作流的实现发现

`discover` 覆盖两类变化：审计 revision 之后已提交的 C#，以及当前工作区 C# 变化。首次启用且没有基线时，它扫描全部 Git 管理的 C# 作为一次性候选，避免遗漏早期手写或直接 Agent 实现。它优先使用 `.agents/codebase-query/code-query-index.json` 返回类型和方法摘要。

发现候选后，Agent 必须：

1. 用 capability ID、标题和索引类型/方法做候选匹配。
2. 读取少量命中源码与对应需求，确认行为而非只看文件名。
3. 执行与风险相称的编译、数据或流程验证。
4. 更新正式 `implementation.json` 和必要的 Review/Spec 事实。
5. 重新 `refresh` 与 `validate`；全部候选处理完后才 `checkpoint`。

生成代码、第三方代码或确认不承载需求的文件，可在 `implementation-audit.json` 的 `discoveryExclusions` 中按项目相对 `path` 或 `pathPrefix` 排除，并必须填写 `reason`。排除只减少审计候选，不产生完成状态。

静态索引、Git 路径或文件存在只能证明“可能实现”，不能证明功能完成。Unity 序列化、Scene/Prefab、反射和事件动态绑定仍需定向核验。

## 持久化边界

- OpenSpec Spec/Change/Review：权威需求与验收事实。
- `openspec/spec-metadata/dependencies.json`：项目关系树元数据，仅用于路由与来源展示，不替代 implementation fact。
- `openspec/specs/<capability>/implementation.json`：正式实现、验证和代码证据事实。
- `openspec/implementation-audit.json`：Git 管理的 discoveryRevision 与排除项。
- `openspec/implementation-summary.json`：Git 管理、可再生成的 `derived-routing-index`，含输入 manifest digest；不是普通代码任务的启动索引。
- `.agent-memory/zworkflow/local/implementation-discovery.json`：本机候选快照，不进入 Git。
- C# 派生索引：定位证据，不是完成状态权威。
