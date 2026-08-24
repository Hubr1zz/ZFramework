---
name: track-implementation-progress
description: 在实现与验证完成后维护 zWorkFlow 的人类可视化进度投影，或在用户显式要求检查实现覆盖时发现绕过 OpenSpec 的代码。不得作为普通开发任务的启动前置；只生成候选，不把未验证代码自动标记为完成。
---

# Track Implementation Progress

把已经完成的代码与验证结果投影为 Workbench 可视化状态；不参与代码方案决策，也不要求下一次普通开发先读取这些产物。

## 稳定入口

在项目根目录运行：

```powershell
& .agents/skills/track-implementation-progress/scripts/run.ps1 refresh
& .agents/skills/track-implementation-progress/scripts/run.ps1 discover
```

- `refresh` 从正式 Spec/Change 与设计实现账本生成 Git 管理的 `openspec/implementation-summary.json`。
- `discover` 额外比较 Git 变化、C# 派生索引和已有代码证据，把结果写入 Git 忽略的 `.agent-memory/zworkflow/local/implementation-discovery.json`。
- `validate` 只验证账本、摘要与路径，不写文件。
- `checkpoint` 只能在全部候选已映射、验证或明确排除后执行；它记录本次已审计 Git revision。

详细字段与状态规则见 [LEDGER-SCHEMA.md](references/LEDGER-SCHEMA.md)。

## 触发边界

- 普通代码任务启动时不运行本 skill，不读取 `implementation-ledger.json` 或 `implementation-summary.json`。
- 只在功能簇实现与验证完成后、用户显式刷新/检查 Workbench 进度时，或显式 OpenSpec apply/sync 生命周期要求更新证据时运行。
- 需要理解代码时使用 C# 派生索引和命中源码；Review、Ledger、Summary 不能替代代码事实，也不能反向扩大实现范围。

## 实现完成后的记录

- OpenSpec Change 内实现：更新 Change Tasks、`change-review.json` / `spec-review.json` 的 `codeReadiness`、Verification 和代码证据，再运行 `refresh`。
- 已有正式 Spec 的直接实现：更新该 Spec 的 `spec-review.json`，记录具体代码职责、项目相对路径、规范化 SHA-256 与验证结果，再运行 `refresh`。
- 只有设计文档、没有正式 Spec 的功能：在 `implementation-ledger.json` 中以稳定 `implementationId` 记录；一个功能一条记录，同一文档允许多条。
- 没有可识别需求来源的代码只能留在 discovery 候选中。先映射到现有能力，或按项目门禁建立合适的 Spec/工程能力条目，不能直接宣称完成。

## 绕过工作流的实现发现

`discover` 覆盖两类变化：工作区未提交 C#，以及账本 `discoveryRevision` 之后已提交的 C#。首次启用且没有基线时，它扫描全部 Git 管理的 C# 作为一次性回填候选，避免遗漏早期手写或直接 Agent 实现。它优先用 `.agents/codebase-query/code-query-index.json` 返回每个候选文件的类型和方法摘要，并检查已有证据哈希是否过期。

发现候选后，Agent 必须：

1. 用 capability ID、标题和索引类型/方法做候选匹配。
2. 读取少量命中源码与对应需求，确认行为而非只看文件名。
3. 执行与风险相称的编译、数据或流程验证。
4. 更新正式 Review 或设计实现账本。
5. 重新 `refresh`；全部候选处理完后才 `checkpoint`。

生成代码、第三方代码或确认不承载需求的文件，可在账本 `discoveryExclusions` 中按项目相对 `path` 或 `pathPrefix` 排除，并必须填写 `reason`。排除只减少审计候选，不产生完成状态。

静态索引、Git 路径或文件存在只能证明“可能实现”，不能证明功能完成。Unity 序列化、Scene/Prefab、反射和事件动态绑定仍需定向核验。

## 持久化边界

- OpenSpec Spec/Change/Review：权威需求与验收事实。
- `openspec/spec-metadata/dependencies.json`：复用工作台项目树的能力级 `readiness`；`implemented` 是实现声明，不等同于已验证。
- `openspec/implementation-ledger.json`：Git 管理的设计来源实现审计账本。
- `openspec/implementation-summary.json`：Git 管理、可再生成的 Workbench 展示索引，不复制 Spec 正文，Agent 普通开发不依赖它启动。
- `.agent-memory/zworkflow/local/implementation-discovery.json`：本机候选快照，不进入 Git。
- C# 派生索引：定位证据，不是完成状态权威。
