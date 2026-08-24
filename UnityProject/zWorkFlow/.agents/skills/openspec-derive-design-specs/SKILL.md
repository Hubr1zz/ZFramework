---
name: openspec-derive-design-specs
description: "仅在用户输入‘设计导入’、‘设计导入：指定范围’、‘修改指定 ID’、/opsx:derive-specs，或明确要求从设计文档生成/发布 Spec 时使用。生成或原位更新模块化、可审批的 Draft Change，并核验代码；普通问答与一般改动不得触发。"
---

# Design Documents to OpenSpec

## 不可变规则

- 导入的唯一内容实体是 `openspec/drafts/changes/<change-id>/`。禁止生成 `openspec/drafts/specs/`；`design-imports/<run-id>/` 只保存批次审计和引用。
- 批准只把整个 Draft Change 移到 `openspec/changes/`。apply 只修改实现与 Change 内审计；正式 Spec 只在用户之后显式执行 sync 时写入 `openspec/specs/`，规则也不例外。
- 每个玩法规则与其实现 Feature 默认放进同一个配对 Change；共享公共契约确有变化时才追加 System capability。
- Delta、正式 Spec 与关系图谱节点必须先通过 [Formal Spec Scope](references/formal-spec-scope.md)：它们只描述游戏玩法与 Player 运行时契约；开发工具、编辑器工具、zWorkFlow 自身能力和行为保持型工程维护即使已经实现或被显式要求生成 Spec，也不得进入。
- scope、文档目录或系统名称只是扫描边界，不是 capability 边界。大型范围必须先拆成模块化 capability，再分别生成配对 Change；禁止为了减少工件数量创建覆盖多个独立状态 owner、生命周期或结算管线的“大系统实现” Feature。
- 同一 capability/语义只保留一份。导入前按顺序检查正式 Spec、正式 Change、Draft Change；完全一致则引用现有工件，Draft 中已有同能力时原位更新，不新建冲突副本。
- 来源 hash 只说明文档文件是否变化，不能替代语义比较。

详细目录与字段见 [change-schema.md](references/change-schema.md)、[metadata-schema.md](references/metadata-schema.md)；多来源规则见 [design-source-schema.md](references/design-source-schema.md)。使用类型参数时再读 [type-filters.md](references/type-filters.md)。

## 入口

- `设计导入`：全部已配置来源和类型。
- `设计导入：<范围>`：先跨全部来源收敛范围。
- 可追加 `--规则`、`--内容`、`--美术`，多项取并集；类型只过滤候选语义，不过滤来源路径。
- `修改<id>: <内容>`：只原位修改对应未发布 Draft Change。

显式 source 优先，否则读取 `openspec/design-source.json` 的全部等价来源。只有没有有效来源时才询问路径；来源只读。

## Capability 拆分（Generate 前强制执行）

1. 先从玩法需求提取状态 owner、生命周期、输入/输出、核心不变量、结算管线和验收场景，再划分 capability；不要按文档文件、目录或总标题直接生成一个 Feature。
2. 满足以下任一条件时必须拆成独立模块：
   - 具有可独立命名的运行时状态 owner 或配置边界；
   - 具有独立生命周期、结算算法或输入输出契约；
   - 能独立实现、测试、验收或在不改变其他模块内部规则的情况下替换；
   - 代码证据与实现差异自然形成互不重叠的脚本/任务簇。
3. 每个模块默认生成一个 `game-rule` 与一个配对 `feature`，并放入该模块自己的 paired Change。模块间通过 `feature -> feature | system` 依赖连接，不靠合并为一个 Feature 隐藏依赖。
4. 只有当两组规则共享同一权威状态、必须原子交付且拆开后任一方都无法形成可验证行为时，才允许合并；在 `proposal.md` 明确记录合并理由。
5. 多个模块共用且进入 Player 构建的游戏运行契约，仅在确有公共契约变化时提取为 System；共享组合根、Manager 或 Presenter 本身不构成合并 capability 的理由。可脱离具体游戏独立复用的 Architecture 进入 `project-tooling`；编辑器、索引、构建、测试、调试和 zWorkFlow 工作流能力进入对应 Skill/工程文档，均不作为玩法 Spec 分类。
6. 生成前做反向检查：若 Feature 名称仍是泛化的“<大系统>代码实现”，而 Tasks、代码证据或验收场景包含多个可独立交付的主题，停止写工件并重新拆分。
7. 对 EventBus、状态机基类、注册表等共享工具先判断“类实现”还是“项目跨模块契约”：只有本次会改变其派发模型/订阅生命周期/重入异常线程等稳定语义，或至少两个 capability 的正确实现依赖这些语义时，才生成以契约命名的 System capability、图谱节点及真实依赖边；普通复用工具只写入 Feature 的 `implementationOutline`/代码证据，不为类本身创建 Spec 或节点。

Agent 拆分与 capability 拆分互不等价：同一负责人可以并且应当在一次导入中产出多个模块化 Feature；“单一负责人”只用于复用上下文，不得作为合并功能边界的依据。

## Generate

0. 读取 `openspec/localization.json.generationLanguage` 决定本次权威工件语言：`zh-CN` / `en-US` 明确指定；`source` 跟随本次命中设计文档的主语言。原 OpenSpec 路径只生成一份权威文件，不在导入流程自动生成翻译副本。
1. 只列出 scope 命中的 Markdown，并跟随其直接 Wiki 链接。跨来源同名且无法唯一解析时报告歧义。
2. 可选运行 `scripts/design_spec_workflow.py prepare` 生成批次清单、来源 hash 和工件索引；脚本不可用时做等价的精确扫描，不要求安装依赖。
3. 读取 `duplicate-precheck.json.artifacts`，按 capability、Requirement/Scenario 与完整行为语义去重：
   - 正式 Spec 一致：记录复用，不创建 Change。
   - 正式 Change 一致：引用该 Change，不创建 Draft。
   - Draft Change 同能力：在原目录更新 spec、review、proposal/design/tasks 与引用；保持 change ID。
   - 正式工件行为不同：记录 revision/conflict，等待用户决定；不得用第二份同义数据掩盖冲突。
4. 在 `openspec/design-imports/<run-id>/` 写 `run.json`、`sources.json`、`duplicate-precheck.json`、`gaps.json`、`dependencies.json`、`draft-refs.json`。批次状态、文档计数、不确定语句、跨来源链接歧义和类型过滤审计统一结构化写入 `run.json`，供 Workbench 简要显示；不生成重复这些数据的 Markdown 报告。`draft-refs.json` 每项只含 `capability`、`changeId`、`status`，可有多项指向同一配对 Change。导入记录只作为仍存在 Draft Change 的导航与审计入口；如果生成后没有 Draft 引用，或最后一个 Draft 被批准/删除，立即删除整个该批次记录，不保留空目录。
5. 按 Capability 拆分结果，在 `openspec/drafts/changes/<change-id>/` 分别写完整 Change。每个配对 Change 默认只承载一个玩法模块的 Game Rule 与 Feature；配对 Feature 是该 Change 的交付主 capability。共享 `proposal.md` 必须详细描述该模块的边界、依赖与交付内容，`design.md` 必须描述实现边界；其中“约束”只记录该 Change 独有的限制或用户决定，不复制全局代码规范，没有专属约束时保留空标题供用户编辑。空白不豁免 Agent workflow、项目架构与全局代码规范。`tasks.md` 只来自该模块的实现差异；禁止共享 Design 写“本 Change 不承载代码差异或实现任务”等空洞内容。Feature spec 用 `实现“<规则标题>设计”` 总括并承载详细实现契约。
6. 配对 Game Rule 只保留玩家可观察 Requirement/Scenario、来源引用和少量规则专属说明；其 `spec-review.json` 写 `pairedFeatureCapability`，不重复保存代码证据、实现差异、Gap、外部依赖、Review Issues 或 Tasks。Feature review 写 `pairedRuleCapability`，并集中承载依赖缺失、审核问题和实现差异。依赖方向仍为 `game-rule -> feature -> system`，但内部配对边为 resolved 关联，不构成独立审批门禁。
7. Gap 只表示缺失依赖；代码差异写 `implementation-delta`，设计矛盾写 `design-conflict`。所有交付门禁数据归 Feature/System；Change 层只做汇总。
8. `dependencies.json` 的 capability ID 是依赖关系和工作台跳转的权威来源。`design.md` 若需要解释依赖，只使用“需要接口”“作为基类”“必要参数”等短语，不重复“通过显式端口提供所需契约”一类技术表述。
9. Change 内 `specs/<capability>/spec.md` 必须使用 delta 标题，禁止裸 `## Requirements`：正式目标不存在时把需求写入 `## ADDED Requirements`；目标存在时按 Requirement 语义分别使用 `ADDED`、`MODIFIED`、`REMOVED`、`RENAMED`，同一文件可包含多类。`MODIFIED` 必须写修改后的完整 Requirement 与全部 Scenario，`RENAMED` 使用 `FROM:` / `TO:`。sync 后的正式 Spec 才使用普通 `## Requirements`。
10. 校验 delta 标题、Requirement/Scenario、唯一 ID、依赖方向、配对字段、review 字段和引用路径，并运行 `openspec validate <change-id> --strict`；失败时不得批准。全部 delta 写完后优先用 `sync_baseline.py capture` 记录 capability、delta/目标路径、目标存在性、SHA-256 和已有目标快照；没有 Python 时直接生成相同字段与快照。基线只供 sync 三方判断，apply 不刷新；批次摘要由 Workbench 聚合，不另存正文。

## Unity 实现设计与引擎指引

- 玩法状态与规则默认放入纯 C# 高内聚对象；`MonoBehaviour` 只承担 Unity 生命周期、场景/Prefab 引用、输入表现适配和组合根装配。优先复用一个系统级 owner / presenter / composition root，不为每个小功能新增并散挂组件。
- 模块化 capability 不等于增加 MonoBehaviour：每个 Feature 应有清晰的纯 C# 状态/服务边界，但可由同一个现有 composition root 统一创建和连接。以依赖注入或显式端口协作，避免模块互相读取内部状态。
- 同一系统的策划参数集中到少量 ScriptableObject 或明确的配置对象，再映射为运行时定义；不要把相关参数拆散在多个场景物体的 `[SerializeField]` 上。运行时权威状态不得存入 ScriptableObject。
- 只有具有场景身份、布局、生命周期或必须由开发者连接的对象才做场景/Prefab 挂载与 Inspector 引用；可由纯 C# 构造、注册表或现有组合根创建的对象不要求手工挂载。
- 对每个 `feature` / `system` capability 检查四类人工动作：Inspector 拖拽引用、策划可调参数、场景/Prefab 创建或挂载、首次使用入口。确有动作时才在该 capability 的 `spec-review.json.editorGuidance` 写简短可执行指引；纯 C#、无暴露字段、非组件型且无需人工接入时省略该字段。`game-rule` 禁止包含它。
- 每个 `feature` / `system` capability 的 `spec-review.json` 写 `implementationOutline` 字符串数组，用少量类似伪代码的句子描述核心数据与函数流程，例如“A 根据 B 判断 C，满足 D 时调用 E，否则调用 F”或“定义 A/B/C 数据类分别记录……”。它用于关系图谱快速理解实现，不复制 Spec Requirement，不写逐行代码。设计阶段写计划方案；apply 后按实际代码更新。`game-rule` 不保存该字段。
- `editorGuidance` 是工作台的简短交接数据，不复制 `design.md`。每条只说明开发者实际要做的动作；分别写入 `inspectorReferences`、`tunableParameters`、`sceneSetup`、`usage`，数量不硬限制，但删除背景说明和非必要步骤。

## 代码核验与缓存

- 先读项目结构索引，再只检索能力涉及的脚本。定位单位是脚本；一个证据描述该脚本承载的一项大功能，不拆成相邻小函数/代码行证据。证据数量不限。`feature` 必须直接说明代码做什么，例如“加载存档并刷新界面”“管理棋盘实体位置与朝向”“定义行动卡 SO 配置”；禁止写“<脚本名>脚本主要职责”、只写脚本名或照抄 capability 标题。
- 每项 `codeEvidence` 保存 Unity GUID、项目相对 `displayPath`、脚本全文 SHA-256 `fileHash`、可选入口行 `line`、大功能 `feature`。
- 新建或重新核验的证据写 `status=verified` 与 `checkedAt`。工作台加载/刷新证据或尝试批准时按 GUID 定位脚本并重算 SHA-256：一致显示“有效”，不同显示“修改过”，无法定位显示“文件缺失”；`invalid` 表示 Agent 已确认原功能描述不再成立。`modified/missing` 是完整性状态，不等同语义失效。
- 下一次导入或 `修改<id>` 先扫描目标 Change 的全部证据：GUID 与 hash 都相同则直接复用；显示“修改过”、文件缺失或已标记失效时，只重读受影响的整份脚本。仍支持原功能时更新路径、行号、`fileHash`、`status=verified`、`checkedAt`；不再支持时写 `status=invalid`，并同步修正 Verification differences、Review Issues 与 Tasks，禁止只替换成新 hash。
- 不以 Git 状态、修改时间或来源文档 hash 判断代码缓存；这些都不能证明脚本内容相同。
- 规则与 Feature 不重复记录同一实现信息：代码证据、差异、Gap、外部依赖、Review Issues、Tasks、详细 Proposal 和实现 Design 归配对 Feature；规则只保留玩家可观察规则、来源和配对 Feature 引用。

## Revise

收到 `修改<id>` 后只读目标 Change、对应批次、sourceReferences 指向的来源及直接依赖，并优先重新核验所有完整性状态为“修改过/文件缺失/已失效”的代码证据。保持 change/capability ID，原位重算受影响的 Spec、review、Tasks、Gap、依赖与报告；除非用户明确要求重新划分能力。不得修改正式 Change、正式 Spec、代码或来源文档。

## Review 与批准

- blocking 问题必须 resolved；warning/info 可显式 accepted，并在 Change 和对应 spec-review 中同步人员、时间、理由。
- 批准门禁按 GUID 重算全部代码证据 hash，但“修改过”只作为 apply 前复核提示，不阻止 Draft 转为 Change；“文件缺失/已失效/未核验”仍阻止批准。Workbench 在审批区用提示框明确显示通过或阻塞原因，并在存在修改证据时说明其数量。apply 开始前由 Agent 重读全部“修改过”的脚本，不能用自动刷新 hash 代替语义核验。
- 配对 Feature 是唯一人工批准入口。批准前按整个 Draft Change 汇总检查所有阻塞问题和硬依赖；Game Rule 不独立批准，也不能绕过 Feature 门禁。Feature 通过后规则随整个目录自动进入正式 Change。
- 依赖可由同一 Draft Change、正式 Spec 或活动 `implementation-change` 满足批准门禁。批准只冻结设计；apply 必须重新检查外部 Change 的 Tasks 全完成、`verification.status=verified|implemented`、`codeReadiness=implemented`、`readiness=ready|implemented` 且无阻塞，否则拒绝。已 apply 但未 sync 的 Change 可作为实现依赖，但仍是待合入 delta。
- 所有类别统一写 `approvalStatus=implementation-change` 后整目录移动到 `openspec/changes/<change-id>/`；不重新生成工件，不直接写正式 Spec。
- apply 只完成实现与验证，不写正式 Spec。用户显式请求 sync 后，才把同一 Change 内 Feature 与 Game Rule 全部 delta 一起合并到正式 Spec；Game Rule 不单独 sync，archive 只归档已同步的 Change。
- Draft 批准后从所有关联批次的 `draft-refs.json` 移除引用；某批次不再引用任何现存 Draft Change 时删除 `openspec/design-imports/<run-id>/`，中央 Draft 索引也不得保留该批次或已移动目录的悬空引用。

## 读取与职责

- 使用项目结构索引做路由，只读取 scope 来源、目标工件、直接依赖和相关脚本。
- 一次导入默认由单一负责人完成检索、语义收敛、Spec/Change 设计与核验，以复用已读上下文。只有子任务无需重复读取同一来源/代码时才拆分 Agent；否则合并职责。
- 不一次加载完整设计库、代码库、全部项目参考或全员偏好。
