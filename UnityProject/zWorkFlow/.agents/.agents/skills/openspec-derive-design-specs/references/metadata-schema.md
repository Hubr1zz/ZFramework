# Design Spec Metadata

## Per-Spec review

每个 Draft Change capability 必须在 `specs/<capability>/` 同目录维护 `spec-review.json`。工作台以它为核验与关联索引；Change 的 `gaps.json` 和 `dependencies.json` 是权威明细。

```json
{
  "schemaVersion": 5,
  "capability": "action-cards",
  "title": "行动卡",
  "category": "game-rule",
  "readiness": "blocked-by-integration",
  "verification": {
    "status": "partial",
    "summary": "已有卡牌状态，缺少统一战斗编排接口",
    "codeEvidence": [
      {
        "guid": "0123456789abcdef0123456789abcdef",
        "displayPath": "Assets/Scripts/Runtime/Abilities/AbilityState.cs",
        "fileHash": "脚本全文 sha256",
        "line": 24,
        "feature": "行动卡运行管线",
        "status": "verified",
        "checkedAt": "2026-07-18T12:00:00+08:00"
      }
    ],
    "evidence": [],
    "differences": ["当前恢复流程与设计时点不一致"],
    "verifiedAt": "2026-07-18T12:00:00+08:00"
  },
  "gapIds": ["GAP-ACTION-CARD-INTEGRATION-001"],
  "dependencyIds": ["action-cards--requires--combat-orchestration"],
  "sourceReferences": ["primary::行动卡.md:24"],
  "pairedFeatureCapability": "action-cards-implementation",
  "editorGuidance": {
    "summary": "把战斗配置集中接入现有战斗组合根。",
    "inspectorReferences": ["将 RulesConfigSO 拖到 GameplayCoordinator 的 rules 字段。"],
    "tunableParameters": ["在 CombatRulesSO 中调整行动点与时点参数。"],
    "sceneSetup": [],
    "usage": ["由现有 GameplayCoordinator 在流程开始时创建规则对象，无需新增组件。"]
  },
  "reviewIssues": []
}
```

`reviewIssues` 与对应 Draft Change 的同名字段保持一致，用于把 Verification 差异、设计冲突和依赖缺失放在一个审核区域显示。它不替代底层 `verification`、`gaps.json` 或 `dependencies.json`：Gap 仍然只表示依赖树缺失，界面层只隐藏技术 ID 和状态枚举。

配对 Game Rule review 写 `pairedFeatureCapability`，配对 Feature review 写 `pairedRuleCapability`。Game Rule 的 `verification.codeEvidence`、`verification.differences`、`gapIds`、外部 `dependencyIds`、`reviewIssues` 和 `implementationOutline` 保持为空；这些交付与门禁数据只在 Feature review 中保存。

`implementationOutline` 只用于 `feature` / `system`；读取 legacy `architecture` 时按 `system` 处理。它以少量类似伪代码的短句记录核心数据类、关键判断和主要调用方向。设计阶段记录计划方案；apply 完成后按实际实现更新，再由显式 sync 带入正式 Spec review。

`editorGuidance` 仅供 `feature` / `system` 使用，读取 legacy `architecture` 时按 `system` 处理，且是可选字段。只有开发者需要在 Unity 中拖拽引用、调整集中配置、创建/挂载场景对象或执行首次接入动作时才生成；纯 C#、没有暴露字段、非组件型且无需人工接入的 capability 必须省略。四个动作数组可分别为空，但整个对象至少包含一条非空动作；`summary` 可选。工作台只在该字段有效时显示“引擎配置”按钮。

`codeEvidence` 是代码证据缓存。每项保存 Unity `.meta` GUID、项目相对路径、最近一次语义核验时的脚本全文 SHA-256 `fileHash`、入口行、大功能、`status` 与 `checkedAt`。证据数量不限，但同一脚本/大功能只保留一项，不按小函数或相邻代码行拆分。`feature` 必须具体说明代码做什么或定位处属于脚本中的哪个大功能，优先使用“动作 + 对象/结果”，例如“加载数据并刷新界面”“管理棋盘实体位置与朝向”“定义行动卡 SO 配置”；不得填写脚本名、capability 标题或“<脚本名>脚本主要职责”。工作台加载/刷新证据或尝试批准时按 GUID 重算 hash：一致显示“有效”，不同显示“修改过”，找不到显示“文件缺失”；持久化 `invalid` 显示“已失效”。修改/重新导入或批准前，Agent 只重读状态异常的脚本；仍支持原功能才更新 hash 与 `status=verified`，否则写 `status=invalid` 并同步实现差异。工作台显示 `<path>:<line> -- <feature> [状态]`；GUID 用于跳转，路径用于展示。

## Gap

`gaps.json` 只记录依赖树缺失项，是对象数组。设计歧义、表现待定或代码差异只有在能表述为“缺少一个应被依赖的 Spec 节点或依赖契约”时才生成 Gap；否则只写入 verification differences，不创建 Gap。每项至少包含：

```json
{
  "id": "GAP-ACTION-CARD-UI-001",
  "capability": "action-cards",
  "dependencyId": "action-cards--requires--action-card-ui",
  "missingNodeId": "action-card-ui",
  "expectedCategory": "feature",
  "requirement": "Action feedback",
  "type": "missing-dependency",
  "severity": "warning",
  "status": "open",
  "blocksImplementation": false,
  "blockedScenarios": [],
  "summary": "额外 UI 提示尚未设计",
  "impact": "不影响规则层与基础交互",
  "recommendation": "表现阶段补充反馈规范",
  "sourceReferences": ["primary::...md:24"],
  "userRationale": "",
  "deliveryBoundary": "",
  "implementationImpact": "",
  "acceptedBy": "",
  "acceptedAt": "",
  "resolutionNote": ""
}
```

`type` 固定为 `missing-dependency`。每个 Gap 必须由且只由一条 `status=open` 的依赖边引用；`missingNodeId` 是尚不存在或尚未达到所需契约的目标节点。

硬前置即使 `status=accepted` 仍保持 `blocksImplementation=true`；只有 `resolved` 解除。

## Dependency graph

```json
{
  "nodes": [
    {
      "id": "action-cards",
      "label": "行动卡",
      "category": "game-rule",
      "readiness": "blocked-by-integration",
      "specPath": "openspec/specs/action-cards/spec.md"
    }
  ],
  "edges": [
    {
      "id": "action-cards--requires--combat-orchestration",
      "from": "action-cards",
      "to": "combat-orchestration",
      "type": "requires",
      "status": "open",
      "reason": "战斗编排接口未定义",
      "gapId": "GAP-ACTION-CARD-INTEGRATION-001",
      "blocksImplementation": true
    }
  ]
}
```

边类型：`requires`、`integrates-with`、`extends`、`presents`。

正式 Spec 分类固定为：

- `system`：与具体项目领域、全局服务、Manager、阶段或跨系统调度契约耦合的项目系统。legacy `architecture` 只作为兼容输入，读取后归一化为 `system`。
- `feature`：Gameplay 或特定系统的具体实现，可覆盖数据层、适配层与表现层。
- `game-rule`：玩法规则与玩家可观察行为，不描述代码实现。

依赖方向必须满足：

- `system -> system`
- `feature -> system | feature`
- `game-rule -> feature`

规则 Spec 不得直接依赖 System Spec；它通过 Feature Spec 间接获得系统依赖。若目标节点缺失，仍创建带预期分类的占位节点和 open 边，并由 Gap 引用该边。

设计导入时，先按权威状态 owner、生命周期、结算管线、输入输出和独立验收边界拆分玩法模块；scope 或系统总标题不得直接充当 capability。每个新 `game-rule` 节点默认同时生成一个完整 `feature` 节点，并放入该模块自己的配对 Draft Change。Feature 标题使用“`<规则标题> 代码实现`”，总括 Requirement 使用“`实现“<规则标题>设计”`”。模块间用 `feature -> feature | system` 边表达依赖。只有无法确定最小 Feature 边界时才退化为缺失节点与 Gap；需要被多个模块共享且确有变化的项目运行契约时再追加 `system` 节点。可脱离具体游戏独立复用的 Architecture 进入工程能力目录。

一个 Feature 若同时拥有多个可独立命名的状态 owner、互不重叠的实现任务簇或可分别验收的结算流程，视为边界过大，必须在生成工件前拆分。多个模块由同一 Manager/composition root 创建或共享同一批设计文档，不构成合并理由。

readiness：`ready`、`ready-with-deferred-gaps`、`blocked-by-design`、`blocked-by-integration`、`implemented`。

## Spec Markdown frontmatter

正式、Draft Change 与 OpenSpec Change 中的每个 `spec.md` 都必须以轻量 frontmatter 持久化分类：

```yaml
---
schemaVersion: 2
category: system
title: 全局调度边界
---
```

分类同时写入同目录 `spec-review.json`，避免 Markdown 解析失败时丢失。

## Markdown marker

```text
> [!SPEC-GAP] GAP-MISSING-DEPENDENCY-001 · OPEN
> 缺失依赖：目标 capability 尚未建立正式契约。
> 依赖边：DEP-CAPABILITY-TO-MISSING-001
> 期望分类：system
```
