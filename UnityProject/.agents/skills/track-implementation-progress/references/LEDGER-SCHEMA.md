# 功能实现账本与 Workbench 进度摘要

## 权威层级

1. 正式 Spec/Change 描述需求与验收标准。
2. `spec-review.json` / `change-review.json` 保存代码就绪度、验证和证据。
3. `implementation-ledger.json` 只补充设计文档与实现的映射、基线和摘要。
4. `implementation-summary.json` 是前三者的派生索引。

## 设计实现账本

账本 `schemaVersion` 为 `3`。顶层可包含：

- `discoveryRevision`：上一次完成候选审计的 Git commit。
- `entries`：按 `(sourceId, documentPath, implementationId)` 唯一定位。

功能条目的稳定字段：

- `implementationId`：稳定 capability ID；不得使用日期、路径或随机 GUID。
- `implementationLabel`：人类可读名称。
- `implementationStatus`：`planned | partial | implemented | verified | stale | blocked`。
- `implementationProgress`：`0..100`，只作展示；不能替代验证。
- `codeReadiness`：`unimplemented | partial | implemented | not-applicable`。
- `verificationStatus`：`unverified | verified | implemented | failed | stale`。
- `discoveryExclusions`：可审计的非需求代码排除项；每项使用项目相对 `path` 或 `pathPrefix`，并填写 `reason`。
- `discoverySource`：`openspec | design-ledger | manual | direct-agent | imported`。
- `evidence`：项目相对路径、规范化 SHA-256、代码职责、状态和检查时间。

`verified` 必须有与功能行为相关的验证记录；文件存在、类型命中或进度 100% 不足以成立。

## Workbench 进度摘要

`implementation-summary.json`：

- `role` 固定为 `derived-index`。
- `requirements` 按 capability ID 排序，保存状态、来源引用、必要摘要和证据引用。
- `attentionRequired` 仅列出需要继续工作的 ID。
- `counts` 汇总状态，供 Workbench 和人类开发者快速查看项目进度。
- 不保存设计文档绝对路径、完整 Spec/Review 正文、生成时间或本机状态。

摘要额外复用 `openspec/spec-metadata/dependencies.json` 的项目树状态。树节点 `readiness=implemented` 会生成 `effectiveStatus=implemented` 和 `verificationStatus=unverified`；没有 Review 或树状态的正式 Spec 为 `unknown`，不会误判成规划中。

该摘要不是普通代码任务的输入或启动索引；Agent 只在显式进度维护、OpenSpec 生命周期或实现完成后的投影阶段读取。

- `attentionRequired`：未实现、部分实现、阻塞、过期或未知的能力。
- `verificationRequired`：已声明实现但尚无有效验证记录的能力。
- `effectiveStatus=stale`：证据路径缺失或哈希不一致。修复代码并不自动恢复 `verified`；必须重新验证并更新权威 Review/账本。

## 发现快照

本地 discovery 快照包含：

- `baselineMissing`：尚无可信审计起点，需要做一次初始定向对账。
- `changedCSharpFiles`：审计基线之后或当前工作区变化的 C# 文件。
- `unmappedCSharpChanges`：没有被任何现有代码证据覆盖的候选。
- `staleEvidence`：路径缺失或内容哈希变化的历史证据。
- 每个 C# 候选的索引类型和方法摘要。

候选快照不能直接写回“已实现”。
