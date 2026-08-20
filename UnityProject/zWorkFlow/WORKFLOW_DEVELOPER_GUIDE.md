# Agent 工作流二次开发者说明

本文面向维护 zWorkFlow、Workbench、OpenSpec 适配器或其他 AI 工具接入的开发者。普通使用者请阅读 [Agent 工作流介绍](WORKFLOW_OVERVIEW.md) 和 [快速上手](WORKFLOW_QUICKSTART.md)。

## 命令行安装边界

仓库根 `package.json` 与 `bin/zworkflow.mjs` 提供 GitHub `npx` bootstrap。它只负责运行时检查、OpenSpec 兼容安装和无覆盖复制；项目事实发现、已有工作流共存分析、内容 skill 生成与 Workbench 条件安装继续由 `setup/SETUP_NEW_PROJECT.md` 驱动 Agent 完成。不要在 CLI 中复制一套会与 setup 文档漂移的架构推断逻辑。

## 产品边界

工作流由四部分组成：

- `.agents/`：跨工具共享的 Skills、Agent 角色和项目事实。
- `openspec/`：正式 Spec、活动 Change、设计导入和审计 metadata。
- `Assets/Scripts/Editor/AgentWorkbench*`：Unity 内的查看、编辑和审批界面。
- `zWorkFlow/`：可复制到其他项目的分发包、模板和适配说明。

工具专属目录只能保存入口、命令、配置和薄 wrapper，不得复制完整共享能力。

### C# 代码结构查询

`codebase-query` 是条件安装的共享 skill。Unity 目标在 `Assets` 或显式项目内 source roots 存在 C#，且可执行 PowerShell 7+（`pwsh`）时，setup 从分发包安装完整 skill，不要求 `Assets/Scripts`。索引 v3 在 Windows/macOS 统一使用 UTF-8 与 `/` 相对路径；缺少 `pwsh` 时只有获得用户许可才尝试安装，否则跳过并保留 Agent 原生 `rg`/源码读取路径。

查询脚本默认扫描 `Assets` 下全部 C# 文件，建立命名空间、完整类型、继承、方法、变量类型、receiver 调用和词法回退关系，再一次性写入 `.agent-memory/zworkflow/local/code-query-index.json`。类型引用以单次标识符扫描和哈希绑定完成，using alias 解析带循环保护。查询时先校验文件数量、最新修改时间和总长度组成的来源签名；签名不变时直接读取缓存，变化时自动重建。索引是可再生成的本地派生物，不进入项目事实、ADR 或 OpenSpec。Unity 工作台“代码索引”页可手动异步构建，并显示进度、文件覆盖率、缺失项与调用统计。

类型绑定只在 receiver 类型唯一且目标类型或项目内基类声明方法时进入 `resolvedCallers`；其他同名命中保留在 `lexicalFallbackFiles`。这能减少误报导致的后续工具调用，但不替代 Roslyn、Unity 序列化资源核验或源码确认。

### 子 Agent 模型路由契约

`setup/adapters/registry.json` 同时保存平台无关的角色需求和平台 adapter 路由能力。角色通过 `economy`、`coding`、`advanced-reasoning` profile 表达能力、推理强度和写权限；只有 adapter 可以声明厂商 selector、原生 Auto、主/次模型或继承降级。

新增或升级 adapter 时必须同步 schema，并验证：当前运行时如何公开模型选项、是否支持逐 Agent 选模/推理档位、何时允许唯一匹配自动选择、歧义如何请求成员确认、无能力时如何降级。不得把某平台模型名按名称或参数规模机械映射到另一平台。

成员的 `cost-first`、`balanced`、`quality-first`、`platform-auto` 偏好及已解析 selector 只写入 `.agent-memory/zworkflow/local/tool-selections/<nickname>.json`。共享 registry 保存能力契约和已验证候选，不保存个人账号模型列表或团队唯一默认模型。

### Workbench 源码路由

- `AgentWorkbenchWindow.cs`：窗口生命周期、主导航、正式 Spec、配置与公共绘制逻辑。
- `AgentWorkbenchWindow.ImportReports.cs`：设计导入批次、Draft Change 审阅与 Markdown 展示。
- `AgentWorkbenchWindow.Changes.cs`：正式 Change 列表、详情与任务进度。
- `AgentWorkbenchWindow.Data.cs`：配置、索引、OpenSpec/Draft 的加载、解析和持久化。
- `AgentWorkbenchWindow.Models.cs`：工作台内部枚举、DTO 与序列化模型。
- `AgentWorkbenchWindow.Translations.cs`：权威语言配置、翻译 manifest、hash 校验、缺失/失效提示与只读门禁。
- `AgentWorkbenchWindow.EditorGuidance.cs`：Feature/Architecture 的可选 Unity 配置指引展示。
- `AgentWorkbenchSupport.cs`：本地化、主题和工作台配置支持。

以上窗口脚本组成同一个 `partial` 类型。维护单一功能时应先读取对应脚本；只有改动跨区状态或导航时才需要读取主窗口和其他分区。

Workbench 中正式 Spec、正式 Change 和 Draft Change 条目都可通过右键或双击修改显示名称。重命名不改变 capability ID、Change ID 或目录名：正式 Change 同步 `change-review.json` 与 Proposal 标题；Draft 条目同步对应 Spec、review、Draft 索引、内容 hash 和依赖节点显示名，主 capability 同时更新所属 Change 标题。

### 查看面板布局契约

- 正式 Spec、正式 Change 与 Draft Change 的列表和详情滚动区只允许纵向滚动，不显示水平滚动条。
- 标题、字段、审核信息、依赖、Markdown、代码块和编辑文本必须按当前可用宽度自动换行；不要用固定内容宽度撑大 ScrollView。
- 导入报告进入批次后，左侧返回按钮右侧提供“导入记录信息”入口；右侧只能显示批次信息与导入提示，或当前 Spec 条目详情之一。批次信息不得作为每个 Spec 顶部的重复折叠区。
- 配对 Change 以 Feature 为详细信息与审批主入口。Game Rule 详情只显示来源、配对 Feature 跳转和规则专属内容；Feature 展示共享 Proposal/Design/Tasks、审核、依赖、实现 Spec，并附带配对规则。
- 关系图谱节点正文只显示名称与 readiness，类型由节点颜色和图例表达。下方信息区使用彩色标题、紧凑统计和正文层级；阻塞依赖按钮定位目标节点，待合入按钮打开所属 Change。
- `.agents/skills/project-tooling/references/tooling-catalog.json` 是工程能力权威源，Workbench 与 Agent 共用；`.agent-memory/zworkflow/local/tooling-discovery.json` 仅保存可删除的来源指纹。`kind` 记录归属与复用边界，可选的项目级 `layers` 和条目 `layerIds` 记录实现分层，Workbench 可独立筛选两者。Plugin 的 `decisionBasis` 可由 Workbench 编辑，Architecture 必须 `required + locked` 且只能在用户确认后由 Agent 改动。具有公共消费入口的工具类 Architecture 首次建档时由 Agent 基于代码与调用证据生成一次中英文 `usageNotes`，后续 setup 不覆盖用户编辑。
- OpenSpec 的项目契约分类使用 `system`；读取 legacy `architecture` 时只在内存中归一化。真正可独立复用的 Architecture 只进入工程能力目录，不与玩法 Spec 混为一类。
- 深色主题的控件 tint 必须足以让按钮和输入框线框与面板背景区分；运行源码和 setup 模板使用同一主题常量。

## OpenSpec 生命周期

```text
Draft Change
  ↓ 人工批准
Change
  ↓ apply：实现并核验
sync specs：生成或更新正式 Spec
  ↓
archive：保留历史审计
```

- 所有类别使用相同生命周期；游戏规则也不得从 Draft 直接写入正式 Spec。
- Draft 审批只移动完整 Change 目录，不重新生成 Proposal、Design、Tasks、Spec 或 Review。
- apply 只实现并验证 Change，不触碰正式 Spec；用户显式触发的 sync 是设计导入首次生成或修改正式 Spec 的唯一入口。
- Change 内 Spec 必须使用 `ADDED/MODIFIED/REMOVED/RENAMED` delta 标题表达合并意图，禁止裸 `Requirements`；新正式目标默认 `ADDED`，已有目标按 Requirement 语义分类。正式 Spec 才使用普通 `Requirements`，Draft 批准前必须通过 OpenSpec strict validation。
- Draft 审批依赖可由正式 Spec 或已批准的活动 Change 满足。依赖方 apply 时再检查后者 Tasks 全完成、Verification 已验证、`codeReadiness=implemented`、`readiness=ready|implemented` 且无阻塞；否则拒绝。未 sync 不影响已完成 Change 作为实现依赖，但仍不能归档。
- `syncTargets` 对已存在目标保存 SHA-256 和 Change 内 `baseSnapshotPath`。sync 先比较 base/current/Delta：目标变化只产生 `merge-safe` 或 `review-required`，不能仅凭 hash 变化宣告冲突。Requirement 不重叠时保留双方；目标删除、同一 Requirement 重叠或旧 Change 无快照时由 Agent 审查，只有确认会覆盖功能或混杂语义才写 `blocked-by-conflict`。
- 未同步全部 capability 的配对 Change 不允许 archive。Workbench 的归档按钮仅在 Tasks 全部完成且 `specSyncStatus=synced` 时启用，使用纯 C# 把完整目录移动到 `changes/archive/YYYY-MM-DD-<id>/`；不会触发 sync。Delete 仍是永久删除，两者不可混用。

## 设计导入与去重

唯一 Draft 内容实体是：

```text
openspec/drafts/changes/<change-id>/
```

禁止重新创建 `openspec/drafts/specs/`。规则和配套 Feature 默认位于同一个配对 Change。

每次导入按以下顺序建立候选索引：

1. `openspec/specs/` 中的正式 Spec。
2. `openspec/changes/` 中的活动正式 Change。
3. `openspec/drafts/changes/` 中的 Draft Change。

处理规则：

- 正式 Spec 内容和行为一致：复用，不创建 Change。
- 正式 Change 一致：引用原 Change，不创建 Draft。
- Draft 中已有同一 capability：保持 Change ID 并原位更新。
- 正式工件行为不同：报告 revision/conflict，不用第二份同义数据掩盖冲突。
- 来源路径、scope 和来源 hash 只用于定位候选；最终判断比较 capability、Requirement、Scenario、约束和完整行为语义。

共享工具默认是实现细节。EventBus、状态机基类、注册表等只有在本次改变派发模型、订阅生命周期、重入/异常/线程等项目稳定公共语义，或至少两个 capability 的正确实现依赖这些语义时，才提升为以契约命名的 System capability 和关系图节点；否则只进入 Feature 的 `implementationOutline` 或代码证据。真 Architecture 由工程能力目录管理。

validator 必须拒绝同一 capability 被多个 Draft Change 持有，以及同一 capability 的完全相同正文出现在多个存储位置。

## Verification 的两层数据

每个 capability 的 `spec-review.json` 保存该能力自己的核验结果；`change-review.json` 保存整个交付包的审批状态和汇总结论。

- `spec-review.json`：随 capability 生命周期存在，记录分类、来源、代码证据、差异、依赖和审核问题。
- `change-review.json`：服务整个 Change 的批准与 apply 门禁，汇总 readiness、codeReadiness、阻塞问题和审批记录。

配对 Change 目前会把主要 Feature 的 Verification 摘要复制到 Change 层，方便 Workbench 和 apply 不遍历全部 capability 就能显示总体状态。这是有消费者的反规范化缓存，不是第二次人工审批。修改代码证据时必须同步两层；后续若改为运行时聚合，可删除这份摘要复制。

## 审批与问题接受

实际只有一个 Change 批准动作：

- capability 的 Review 用于判断局部内容是否清楚、是否与代码一致，不单独把 capability 发布出去。
- 点击“批准 Change”后，Workbench 检查整个 Change 的阻塞问题和硬依赖，并把 Draft 目录移入 `openspec/changes/`。
- 接受 warning/info 只是记录风险已被用户知晓，不等于批准 Change。
- Draft 与正式 Change 共用审核问题表格：级别使用迷你图标和颜色区分；warning/info 可逐行勾选并填写 `acceptanceNote`，blocking 的接受控件保持禁用。接受状态必须同步到 Change 汇总和同 ID 的 capability review，供 apply 读取。
- `apply` 是执行已批准计划，不是第二次内容审批。

## 代码证据缓存

代码证据以脚本和大功能为单位，不按小函数拆分，数量不限。每项保存：

- Unity `.meta` GUID
- 项目相对显示路径
- 脚本全文 SHA-256 `fileHash`
- 可选入口行
- 具体大功能描述：说明代码做什么，使用“动作 + 对象/结果”，不得只写脚本名或“脚本主要职责”
- 最近一次 Agent 语义核验的 `status` 与 `checkedAt`

Workbench 加载/刷新证据或尝试批准时按 GUID 找到当前脚本并计算 hash：一致显示“有效”，不同显示“修改过”，找不到显示“文件缺失”；“修改过”只代表字符内容变化，不阻止批准。apply 开始前 Agent 自动重读修改过的脚本：仍支持原功能则更新 hash 并直接继续，确认不再支持则先停止实现、调整 Change 的 Verification/Issues/Tasks 与受影响规划，再重新校验。文件缺失、已失效或未核验证据仍阻止审批。审批区用 HelpBox 明确显示门禁通过、修改证据数量或具体阻塞原因。Git 状态、修改时间和设计文档 hash 不能替代代码内容 hash。

## 产物可见性与消费者

Workbench 展示业务内容，但不逐个展示机器文件。所有持久化产物必须属于权威内容、索引或审计之一，并至少有一个明确消费者。

| 产物 | 可见性 | 消费者 |
| --- | --- | --- |
| `drafts/changes/<id>/` | Workbench 直接展示 | Draft 审阅、批准、apply |
| `change-review.json.implementationNotes` | Workbench 的“备注”按钮按需展示 | Workbench 编辑、apply 编码前读取 |
| `change-review.json.syncTargets`、`.sync-baseline/` | Change 详情聚合显示同步状态，不直接展示快照正文 | sync 的三方合并预检、冲突审计 |
| `spec-review.json.editorGuidance` | 仅在有人工 Unity 接入动作时显示“引擎配置”按钮 | Workbench、实现交接、validator |
| `run.json` | Workbench 聚合展示批次摘要与必要提示 | 状态、文档计数、不确定项、链接歧义、类型过滤审计、恢复 |
| `sources.json`、`duplicate-precheck.json`、`draft-refs.json` | 不逐文件展示 | 去重、来源追踪、validator |
| `drafts/index.json` | 不展示原始 JSON | Workbench 分组和批次引用；不得保存正文 |
| `spec-metadata/*.json` | 通过依赖和 Gap 面板展示 | 正式 Spec 关系图与审核 |
| `changes/archive/` | 不在活动列表展示 | 历史审计 |
| `translations/<language>/` | 仅在 hash 同步时作为 Workbench 显示正文 | Workbench 显示；Agent 翻译命令写入，不参与 apply/sync/validator |
| `translations/manifest.json` | 不展示原始 JSON | Workbench 新鲜度门禁、Agent 块级增量翻译 |

不再生成重复结构化批次数据的 `report.md`；代码核验只读取 `spec-review.json`。没有 Workbench、CLI、validator、apply/sync 或人工审计消费者的产物不应生成。索引和审计只能保存引用、hash 与必要摘要，不得复制 Spec/Review 正文。

导入记录的生命周期与 Draft 引用绑定：批准、删除或外部移动 Draft 后，工作台重写 `draft-refs.json` 并清理中央索引；一个批次不再引用任何实际存在的 Draft Change 时，删除整个 `design-imports/<run-id>/`，不得保留空记录。

`editorGuidance` 是 capability 级唯一结构化交接，不再生成第二份引擎说明文件。它只允许出现在 Feature/Architecture，并按 Inspector 引用、可调参数、场景安装和使用入口组织简短动作；没有人工操作的纯 C# 或非组件型实现省略字段，工作台也不显示按钮。

## 安全边界

- 非平凡功能或公共契约变化必须先通过 Intake Gate；重构、工具和行为保持型调整不触发。
- blocking 审核问题必须 resolved，不能通过 accepted 绕过。
- 被接受的硬依赖仍阻塞实现，只有补齐依赖并设为 resolved 才解除。
- Gap 只表示缺失依赖节点或契约；代码差异和设计冲突不能伪装成 Gap。
- `tasks.md` 只包含实现差异，不承载设计澄清或等待依赖。
- 外部设计来源只读；项目包独立维护 `openspec/implementation-ledger.json`。项目 bridge 以 `openspec/design-source.json` 中全部来源为唯一文档输入，每个来源按稳定 `sourceId` 建立独立顶层节点；刷新时递归重扫 Markdown、同步新增/删除文件并重算指纹。账本以 `(sourceId, documentPath, implementationId)` 定位实现，新文档默认 0%，未被账本解释的指纹变化显示“手动修改”。它不向设计包注入项目路径或工程状态，也不自动创建 proposal。
- 修改 `.agents/` 后同步 `zWorkFlow/.agents/` 权威分发副本；修改 Workbench 后同步 `zWorkFlow/setup/assets/*.template`；修改普通用户行为后只更新 `zWorkFlow/` 顶层三份人类文档，并重建干净移植包与 ZIP。人类文档不再维护 setup 模板或项目根副本，工具专属目录仍保持薄壳。
- 新持久化产物必须先声明角色、消费者和可见性。

## Workbench 文档入口

Workbench“介绍”页固定展示两份普通用户文档：

- `WORKFLOW_OVERVIEW.md`：面向无开发经验的使用者。
- `WORKFLOW_QUICKSTART.md`：用最短步骤带用户完成第一次使用。

`WORKFLOW_DEVELOPER_GUIDE.md` 作为二次开发附带文件保存在 `zWorkFlow/` 安装源，但不在工作台介绍页展示。setup 必须校验三份文档完整，不得把它们复制到项目根。
