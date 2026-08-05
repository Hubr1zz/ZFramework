# Unified Draft / OpenSpec Change Schema

设计导入中的 Draft Change 与 `openspec/changes/<change-id>/` 使用完全相同的目录结构：

```text
<change-root>/
├── proposal.md
├── design.md
├── tasks.md
├── change-review.json
├── dependencies.json
├── gaps.json
├── sources.json
└── specs/<capability>/
    ├── spec.md
    └── spec-review.json
```

设计导入统一写入 `openspec/drafts/changes/<change-id>/`，导入批次仅通过 `draft-refs.json` 引用它。Draft 是完整的一等工件；探索、拆分、重新设计和依赖修复都发生在 Draft 阶段。人工批准实现类 Draft 后，只允许把整个目录移动到 `openspec/changes/<change-id>/`；不得重新生成 proposal、design、tasks、delta Spec 或审计数据。导入批次不是永久历史：当 `draft-refs.json` 不再指向任何现存 Draft Change 时，必须删除整个 `design-imports/<run-id>/` 记录并清除中央索引中的悬空 runId。

`change-review.json` 最低结构：

```json
{
  "schemaVersion": 5,
  "changeId": "design-import-20260722-fishing-rules",
  "title": "钓鱼规则",
  "category": "paired",
  "sourceKind": "design-import",
  "sourceRunId": "20260722-fishing",
  "readiness": "blocked-by-integration",
  "codeReadiness": "not-applicable",
  "approvalStatus": "draft",
  "implementationNotes": "实现时，第二个 Task 只保留函数接口。",
  "verification": {
    "status": "partial",
    "summary": "规则明确，但缺少钓鱼 Feature Spec",
    "codeEvidence": [
      {
        "guid": "0123456789abcdef0123456789abcdef",
        "displayPath": "Assets/Scripts/Gameplay/FishingSystem.cs",
        "fileHash": "脚本全文 sha256",
        "line": 42,
        "feature": "钓鱼结算管线",
        "status": "verified",
        "checkedAt": "2026-07-22T12:00:00+08:00"
      }
    ],
    "evidence": [],
    "differences": ["未发现可承载规则的实现契约"],
    "verifiedAt": "2026-07-22T12:00:00+08:00"
  },
  "reviewIssues": [
    {
      "id": "ISSUE-GAP-FISHING-FEATURE-001",
      "type": "dependency-missing",
      "severity": "blocking",
      "status": "open",
      "blocksApproval": true,
      "summary": "缺少钓鱼 Feature Spec",
      "details": "批准时依赖必须由正式 Spec 或已批准的活动 Change 提供；apply 时再检查依赖实现就绪",
      "sourceId": "GAP-FISHING-FEATURE-001",
      "acceptedBy": "",
      "acceptedAt": "",
      "acceptanceNote": ""
    }
  ],
  "gapIds": ["GAP-FISHING-FEATURE-001"],
  "dependencyIds": ["fishing-rules--requires--fishing-feature"],
  "capabilities": ["fishing-rules", "fishing-rules-implementation"],
  "syncTargets": [
    {
      "capability": "fishing-rules",
      "deltaSpecPath": "openspec/drafts/changes/design-import-20260722-fishing-rules/specs/fishing-rules/spec.md",
      "targetSpecPath": "openspec/specs/fishing-rules/spec.md",
      "targetExisted": false,
      "baseFileHash": "",
      "baseSnapshotPath": ""
    }
  ],
  "specSyncStatus": "pending",
  "syncValidation": {
    "status": "baseline-captured",
    "summary": "已记录正式 Spec 基线；sync 前将进行并发修改校验。",
    "changes": [],
    "conflicts": [],
    "validatedAt": "2026-07-22T12:00:00+08:00"
  },
  "createdAt": "2026-07-22T12:00:00+08:00"
}
```

- `dependencies.json` 和 `gaps.json` 保存该 Change 的完整局部依赖子图与缺失依赖。
- `implementationNotes` 是可选的用户实现备注，属于 Change 的权威实现约束；Workbench 负责编辑，`openspec-apply-change` 在编码前从已必读的 `change-review.json` 中读取。它不得覆盖已批准 Spec 或验收标准。
- `syncTargets` 是 Change 对正式 Spec 的目标与创建时基线；已存在的目标还保存 `baseSnapshotPath` 供 sync 做 base/current/Delta 三方判断。新增、删除或 hash 改变只产生 `changes` 与 `clean | merge-safe | review-required` 判定，不自动等同冲突；仅在 Requirement 重叠审查确认会覆盖功能或混杂语义后，才写 `status=conflict`、设置 `specSyncStatus=blocked-by-conflict`。只有用户明确 rebase 后才可刷新基线。
- `reviewIssues` 是审核视图的统一数据源：`design-conflict`、`dependency-missing`、`implementation-delta`。Gap 只对应依赖缺失；Verification 差异映射为实现差异，但原始 `verification` 仍保留用于审计。
- `severity` 为 `blocking | warning | info`。`blocking` 必须解决且不能接受；warning/info 可由用户显式接受并记录人员、时间和理由。`implementation-delta` 不阻止批准，它必须转成 Task。
- Draft 的 `approvalStatus=draft`。批准为正式实现 Change 后写 `implementation-change`、`approvedBy`、`approvedAt`，并把 `readiness` 设为 `ready`；`codeReadiness` 仅允许 `unimplemented | partial | implemented | not-applicable`。
- `verification.codeEvidence` 的 GUID 是代码跳转依据；`displayPath` 只展示。`fileHash` 是最近一次语义核验时的脚本全文 SHA-256，`status=verified | invalid` 与 `checkedAt` 记录 Agent 核验结论和时间。Workbench 在加载/刷新与批准门禁中比较当前 hash，派生显示“有效/修改过/文件缺失/已失效/未核验”；“修改过”只表示字符内容发生变化，必须重读脚本后才能更新 hash 或判为失效。证据以脚本和大功能为粒度，数量不限；`line` 只用于打开脚本时靠近主要入口，不表示每个小功能都要单独取证。`feature` 使用“动作 + 对象/结果”描述定位处负责的大功能，例如“管理场景实体位置与朝向”或“定义环境组件配置”；脚本名、capability 标题和“脚本主要职责”不构成功能描述。
- `spec-review.json` 保存单条 capability 的分类、Verification、Gap IDs、Dependency IDs 与来源；Feature/System 还保存简短 `implementationOutline`，用类似伪代码的句子概括核心数据与函数流程，供关系图谱展示；如需人工 Unity 接入，可额外保存唯一一份结构化 `editorGuidance`。
- `tasks.md` 继续使用 Markdown checkbox 作为持久化格式；任务只能覆盖 Verification 发现的实现差异，不能承担设计澄清或依赖等待。工作台必须解析为进度条和任务状态列表，不直接把全文当普通 Markdown 显示。
- 玩法规则与配套 Feature 默认位于同一个 `category=paired` Change；一个 paired Change 默认只承载一个可独立实现、测试和验收的玩法模块。大型 scope 必须先按权威状态、生命周期、结算管线和独立验收边界拆成多个 paired Changes，再用依赖图连接。共享 `proposal.md`、`design.md`、`tasks.md` 属于该模块的交付主 Feature：Proposal 详细描述模块边界、依赖与交付，Design 描述实现边界，Tasks 只覆盖实现差异。它们不得使用“规则 Change 不承载代码差异”之类与配对 Change 相冲突的旧措辞。
- Feature delta 至少包含 `### Requirement: 实现“<规则标题>设计”`，并集中保存代码证据、实现差异、Gap、外部依赖和 Review Issues。Game Rule delta 只保存玩家可观察 Requirement/Scenario、来源和 `pairedFeatureCapability`；Feature review 以 `pairedRuleCapability` 反向关联。只有项目共享运行契约变化时才在同一 Change 增加 System capability。
- Change Spec 只允许 `## ADDED/MODIFIED/REMOVED/RENAMED Requirements`，不得使用裸 `## Requirements`。正式目标不存在时全部使用 `ADDED`；目标存在时按每条 Requirement 的语义差异分类。普通 `## Requirements` 只属于 sync 后的正式 Spec。
- 工作台中 Game Rule 不展示共享 Proposal、Tasks、实现审核或依赖面板；Design 只显示来源链接，Spec 区显示配对 Feature 跳转。Feature 展示共享工件、全部交付门禁、实现 Spec，并附带配对玩法规则正文。
- 每个 paired Change 的配对 Feature 是该模块唯一批准入口，且必须按整个 Change 汇总检查阻塞问题与硬依赖。不同模块分别批准；依赖尚未满足时不得借由把模块合并进同一个大 Feature 绕过门禁。批准会整体移动目录，Game Rule 自动随 Feature 进入正式 Change；apply/sync 也必须成对完成。
- 批准依赖可由同一 Draft、正式 Spec 或已批准的活动 Change 满足；活动 Change 只需为 `approvalStatus=implementation-change`。依赖方 apply 时必须再次检查其 Tasks 全完成、Verification 已验证、`codeReadiness=implemented`、`readiness=ready|implemented` 且无阻塞；否则拒绝。未 sync 不影响已完成 Change 作为实现依赖，但不能视为正式 Spec 或归档前提。
- 人工批准对所有类别都只把目录移入 `openspec/changes/`。apply 保持 Change 沙箱独立；正式 Spec 仅由用户显式触发的 sync 生成，Draft 审批与 apply 均不得直接写 `openspec/specs/`。
- 手工创建的 OpenSpec Change 也必须补齐同样的审计文件；无法核验时写明确的 `verification.status=unverified`，不得省略。
