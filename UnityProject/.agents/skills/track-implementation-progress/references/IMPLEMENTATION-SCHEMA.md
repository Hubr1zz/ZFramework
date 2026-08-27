# Implementation Audit 与 Routing Index Schema

## 权威层级

1. 正式 OpenSpec Spec/Change 描述需求与验收标准。
2. `openspec/specs/<capability>/implementation.json` 保存实现状态、验证和代码证据事实。
3. `openspec/implementation-audit.json` 保存 discovery revision 与候选排除项。
4. `openspec/implementation-summary.json` 是可验证的派生路由索引，不是事实来源。
5. `.agent-memory/zworkflow/local/implementation-discovery.json` 只保存本机候选，不进入 Git。

## implementation.json

每个正式能力在 `openspec/specs/<capability>/implementation.json` 中维护一条事实记录。推荐字段：

```json
{
  "schemaVersion": 1,
  "artifactRole": "formal-implementation-assertion",
  "capability": "hunt-map-generation",
  "specHash": "<normalized current spec.md SHA-256>",
  "title": "狩猎地图生成",
  "codeReadiness": "implemented",
  "progress": 100,
  "verification": {
    "status": "verified",
    "validatedAgainstSpecHash": "<same current spec.md SHA-256>",
    "evidence": [{
      "displayPath": "Assets/GameScripts/GameLogic/HuntingInDarkness/GameCore/Hunt/HexMapGenerator.cs",
      "fileHash": "<normalized UTF-8 SHA-256>",
      "feature": "确定性地图生成规则"
    }]
  },
  "summary": "已完成并通过定向验证。",
  "sourceReferences": [
    { "sourceId": "design-doc", "path": "Design/hunt-map-generation.md" }
  ]
}
```

实现状态支持 `planned | partial | implemented | verified | stale | blocked | unknown`。正式断言必须具有正确的 `artifactRole`、capability 身份，并让 `specHash` 与 `verification.validatedAgainstSpecHash` 同时绑定当前 Spec。实现事实可以使用 `implementationStatus/status/readiness`，也兼容 `codeReadiness`；当 `codeReadiness=implemented` 且 `verification.status=verified` 时派生为 `verified`。绑定错误、证据缺失、缺 hash 或 hash 不一致时派生状态为 `stale`。

`sourceReferences`/`designSources` 只保留项目内相对路径和可选 `sourceId`。外部绝对设计路径会被忽略，不进入 Git 管理的摘要或输入清单。

## implementation-audit.json

```json
{
  "schemaVersion": 1,
  "discoveryRevision": "<git commit>",
  "discoveryExclusions": [
    { "pathPrefix": "Packages/", "reason": "第三方代码" }
  ]
}
```

该文件不声明功能完成，只控制 C# 候选审计基线和有理由的排除项。`discover` 只写本地候选；`checkpoint` 才更新 `discoveryRevision`。

## implementation-summary.json

摘要固定为：

```json
{
  "schemaVersion": 2,
  "role": "derived-routing-index",
  "inputDigest": "<content/state digest>",
  "inputManifestDigest": "<manifest digest>",
  "outputDigest": "<derived routing content digest>",
  "inputManifest": [
    { "path": "openspec/specs/hunt-map-generation/implementation.json", "sha256": "...", "state": "current" }
  ],
  "counts": {},
  "attentionRequired": [],
  "verificationRequired": [],
  "staleEvidence": [],
  "requirements": [{
    "id": "hunt-map-generation",
    "designSources": [{ "sourceId": "design-doc", "path": "Design/hunt-map-generation.md" }]
  }]
}
```

`inputManifest` 按项目相对路径的 ordinal 顺序排序，包含摘要读取的 Spec、implementation fact、依赖元数据和代码证据；缺失输入也保留为 `state=missing`。`inputManifestDigest` 是规范化 manifest JSON 的 SHA-256，`inputDigest` 是按同一顺序组合 `path/hash/state` 的 SHA-256，`outputDigest` 绑定 Workbench 实际消费的派生 requirement、设计来源与证据状态。`validate` 还会重新构建完整摘要并作深比较，任何输入或派生内容被修改都 fail-closed。

`requirements` 按 capability ID 排序，保存派生状态、来源、摘要和证据状态。摘要不复制 Spec 正文，不包含本机绝对路径或生成时间。

## query 切片

- `query -Attention`：返回所有 `effectiveStatus` 不是 `verified/implemented` 的能力。
- `query -Slice capability -Capability <id[,id]>`：按 capability ID 筛选。
- `query -Slice path -Path <project-relative-prefix>`：按证据路径前缀筛选。

所有 query 先执行同样的输入 digest 校验；过期摘要不能查询，必须先显式 `refresh`。
