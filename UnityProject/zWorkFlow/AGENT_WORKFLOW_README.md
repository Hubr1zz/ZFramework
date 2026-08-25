# Agent Workflow README

这是可搬运 Agent 工作流包的维护入口。修改 `.agents/`、任一 AI 工具适配目录、`AGENTS.md`、memory 约定或 OpenSpec 流程前，必须先读本文件。

## 设计原则

- `zWorkFlow/` 是工作流制作、setup 和人类说明的统一管理边界；目标项目根不复制完整说明，只保留 AI 工具发现所需薄入口、项目 `.agents/`、OpenSpec/团队状态和 Unity 编译所需工作台脚本。
- setup 发现工具专属目录中存在项目规则或完整 skill 时，必须先把内容按能力迁入共享 `.agents/`、校验无遗漏，再把原路径改为薄入口；不能把“只读保护”误解为永久保留重复正文。冲突或无法证明等价时停止该项迁移并保留原文件。
- `zWorkFlow/zWorkFlow Pack/` 是不携带案例项目事实、个人资料或运行历史的干净发布仓；将其作为 `zWorkFlow/` 放入新项目后，可直接执行 `setup/SETUP_NEW_PROJECT.md` 获得完整效果。
- `.agents/` 是唯一完整内容源。
- 各工具专属目录只保留入口、命令和 agent wrapper；不得复制完整共享能力。
- 同一仓库允许不同成员同时使用不同 AI 工具，不保存团队级唯一 active tool。
- 不使用 symlink / junction，保证把完整 `zWorkFlow/` 安装源目录放入新项目后即可 setup。
- OpenSpec Intake Gate 只约束会改变功能、外部可观察行为或公共运行契约的非平凡产品功能或运行框架改动；行为保持型实现调整不触发。

## 目录映射

| 能力 | 共享源 | 工具适配方式 |
| --- | --- | --- |
| 项目入口 | `AGENTS.md` / 本文件 | 直接读取，或由 `CLAUDE.md`、`GEMINI.md` 等薄入口转入 |
| Skills | `.agents/skills/` | 支持该开放路径的工具直接扫描；其他工具使用薄 wrapper |
| Agent 角色 | `.agents/agent-roles/` | 仅在工具支持原生自定义 Agent 时生成薄壳 |
| OpenSpec 命令 | `.agents/skills/openspec-*` | skill 直接触发，或使用工具专属命令 wrapper |
| C# 结构查询加速 | `.agents/skills/codebase-query/` | 预生成本地派生索引，一次返回类型绑定、调用者和影响候选；源码仍是最终依据 |
| 工程能力目录 | `.agents/skills/project-tooling/` | Git 同步 Plugin/Architecture/System 事实与策略；本机来源指纹仅用于增量发现 |
| OpenSpec 中英文显示翻译 | `.agents/skills/openspec-translate/` + `openspec/localization.json` + `openspec/translations/` | 原 OpenSpec 路径保持唯一权威；共享翻译副本按块 hash 增量更新，Spec 条目名称按 capability ID 保存中英文显示名 |
| 团队身份与集成状态 | `.agent-memory/` | 保存成员映射、个人规范和 zWorkFlow 路由状态；不保存通用决策/踩坑池 |
| 保护清单 / 增量维护队列 | `.agents/skills/project-refactor-queue/references/{PROTECTED_FILES,REFACTOR_QUEUE}.md` | 前者仅写任务读取；后者仅增量重构、技术债维护或查看队列时读取 |
| 成员个人规范 | `.agent-memory/zworkflow/team/members/<nickname>.md` | 只按当前成员读取 |
| 工具适配注册表 | `zWorkFlow/setup/adapters/registry.json` | 支持 Codex、Claude Code、Cursor、Copilot、Gemini、Windsurf、Kimi |

## 修改规则

1. 完整规则只改 `.agents/skills/` 或 `.agents/agent-roles/`。
2. 新增 skill 只先修改 `.agents/skills/`；仅为注册表声明需要 wrapper 的工具同步薄壳。
3. 新增 agent 角色先写 `.agents/agent-roles/`；只更新正式支持且需要原生 Agent 配置的工具壳层。
4. 只有会新增或改变玩法功能、玩家可观察行为或 Player 运行时公共契约的非平凡游戏改动才过 `openspec-intake-gate`。行为保持型重构、实现方案调整和开发工具即使被显式要求使用 OpenSpec/zWorkFlow，也不能进入正式 Spec；显式调用只触发审阅。
5. 成员提出个人规范时，更新 `.agent-memory/zworkflow/team/MAINTAINERS.md` 与对应 `.agent-memory/zworkflow/team/members/<nickname>.md`；不要写入口文档。
6. 修改完成后检查旧绝对路径、工具私有 memory、重复完整 skill 副本和团队级唯一 active tool 是否残留。
   工具专属目录不得保存项目事实、维护队列或完整功能文档；这些内容必须归属到对应 `.agents/skills/<功能>/references/`。
7. 设计文档转 Spec 必须显式触发；普通问答不得扫描整个设计仓库。
   已有至少一个有效的 `openspec/design-source.json` 来源时不得要求用户重复输入路径，也不得被 setup 空配置覆盖。多个来源地位相同，类型参数只能用于扫描后的语义过滤。
8. 发布带缺失 Spec 与允许实现分开判断；硬前置 accepted 后仍阻塞实现。
9. 设计 Spec 扫描必须有 Agent 原生路径；可选脚本缺失时不得要求用户安装运行时。
10. 文档包与项目包独立；设计来源路径只保存在项目本机配置。正式实现事实按 capability 保存为 `openspec/specs/<id>/implementation.json`，审计基线与排除规则保存在 `openspec/implementation-audit.json`，不得再建立同时承担映射、状态和证据的全局 Ledger。
11. Review 只属于 active/archive Change。正式 Spec 是已接受的行为权威，不保存 reviewIssues、difference 或 approval；显式 sync 将已核验事实转换为无争议的 `implementation.json`。
12. `implementation-summary.json` 仅是带输入摘要校验的派生路由索引。摘要有效时可用于缩小到少量 capability；摘要过期时必须 fail-closed 并刷新，不能据此宣称功能完成。普通代码理解仍以 C# 派生索引和命中源码为准。
13. Agent 可在用户明确授权时直接按文档编码；完成后必须核验已有正式 Spec，或生成 post-hoc adoption Change 供人类审查。没有正式 Spec 的实现与超出正式行为的实现不得直接成为权威。
14. `.DS_Store`、Python 缓存、`openspec/workbench-config.json`、`openspec/design-source.json`、`.agent-memory/zworkflow/team/MAINTAINERS.md`、`.agent-memory/zworkflow/team/members/` 与 `.agent-memory/zworkflow/local/` 属于系统生成物、个人偏好或机器路径，必须由 `.gitignore` 排除；项目级 Spec、Change、依赖、Gap 与共享适配器仍正常纳入版本管理。
15. 新增持久化产物前必须注明权威内容、索引或审计三种角色，并至少有一个明确消费者（Workbench、CLI、validator、apply/sync 或人工审计）。索引与审计只能保存引用、hash 和必要摘要，不得复制 Spec/Review 正文；没有消费者的产物不生成。
16. `openspec/localization.json` 和 `openspec/translations/` 必须进入 Git。前者保存以后生成权威工件的默认语言，以及以 capability ID 为键的中英文 Spec 条目显示名；后者是 Workbench 的非权威显示副本与块级 hash 索引。显示名与翻译均不得被 apply、sync、validator 或 Agent 事实读取替代原 Spec，依赖和生命周期引用仍使用稳定 ID。
17. 已有工具工作流接入采用“清点 → 迁入共享源 → 路径更新 → 哈希/引用校验 → 原路径薄化”的事务。根 `CLAUDE.md`、`.claude/skills/`、`.codex/skills/` 中有价值的项目规则不得丢弃，也不得继续作为第二份权威正文；工具设置、凭据、用户历史和无法归类的内容不迁移。
18. 工程能力发现按稳定、可独立路由的模块拆分。README 的核心模块表、模块目录、公共接口、asmdef 和代码入口共同证明边界时，Resource、Event、Config、UI、Procedure 等应分别建条目；不得只生成一个覆盖整个框架的笼统能力。

## Agent 模型分层

先判断拆分是否会导致重复读取。一次设计导入、同一系统的诊断+方案或紧密耦合审查默认由一个负责人贯穿；只有子任务上下文独立，或主 Agent 能提供足够的已读摘要而无需重读时才分 Agent。项目存在 `project-context/references/PROJECT-INDEX.md` 时先读索引，否则先读 setup 映射出的等价项目速查。

代码任务采用“索引定位 → 方案设计 → 功能簇实现 → 一次后置审查与定向验证 → 进度投影”的节奏：

1. 只读执行 Agent 先用 C# 派生索引收敛类型、调用者、生命周期入口和影响范围，再读取少量命中源码，形成可复用的事实包。代码索引属于设计阶段的前置输入，不是仅供实现后审计的工具。
2. 方案 Agent 基于事实包完成宏观边界，也必须下沉到可执行的接口契约、状态所有权、失败路径、目标文件和验收接缝；不输出逐行实现。事实不足时只要求定向补查，不重新全量扫描。
3. 执行 Agent 复用同一事实包完成读写、命令输入输出和验证。多个职责紧密相关、共享同一状态机或生命周期的改动作为一个功能簇连续实现，不逐文件切换 Agent、审查或跑全量测试。
4. 每个功能簇完成后统一做一次后置审查和定向测试。全量测试只用于跨系统/L4 里程碑、公共运行契约变更、发布门禁或用户明确要求；普通功能簇使用编译、数据验证和命中测试组。
5. OpenSpec Change、Review、Summary 和 Workbench 进度只在实现与验证完成后投影，或在用户显式操作其生命周期时读取。经过 digest 校验的 Summary 只负责定位，不能替代命中的 Spec、实现状态与源码；除代码索引外，zWorkFlow 产物不得成为普通开发任务的启动依赖。

### 直接代码任务的最小读取顺序

用户直接要求实现、修复或重构代码，并不等于绕过项目规则；它只表示不默认启动 OpenSpec Change。Agent 按以下顺序读取，命中后停止扩张：

1. 工具先自动注入系统 / 开发者规则和根入口（例如 `AGENTS.md`），并按 skill 元数据选择本阶段明确命中的通用 skill；方案分析类 skill 在设计前读取，后置审查类 skill 留到产出完成后读取或执行。
2. 读取根入口为普通开发明确列出的保护清单和当前成员偏好；这些是入口规则，不是领域 skill。完整 zWorkFlow 维护说明只在工作流修改、setup、路由诊断或显式生命周期任务中读取。
3. 读取 setup 记录的等价项目速查；若使用 zWorkFlow 自有项目内容，则先读 `project-context/references/PROJECT-INDEX.md`，只选择任务命中的领域、框架或工程能力行。
4. 任何 C# 修改先读项目代码流程（若存在）并完成风险分级；涉及类型、调用者、生命周期或影响范围时先用 `codebase-query` 形成紧凑事实包，再读取命中源码。
5. 只加载索引命中的领域 skill 和当前阶段确实触发的其他 skill。角色说明只在该角色实际调度时加载；`solution-architect` 不属于所有代码任务的必读项。
6. 行为保持型重构、工具改动和边界清晰的小修改直接实现；会改变非平凡产品行为或公共运行契约时才进入 `openspec-intake-gate` 并读取相关正式 Spec。
7. 功能簇完成后统一执行一次后置审查和风险相称的验证，再提交 Git。实现进度 skill、Summary、Review 与 Workbench 只在完成后投影，不在任务开始时读取。

- 共享需求使用 `economy`、`coding`、`efficient-read`、`efficient-execution`、`advanced-reasoning` profile，并分别表达推理强度、写权限和成本/质量优先级；权威结构位于 `setup/adapters/registry.json`。
- Codex 与 Claude 的模型名只是经过验证的平台映射示例，不作为其他平台的模型名翻译表。
- setup 只在当前运行时确认模型可用且映射唯一时自动选择；候选不唯一、账号策略不可见或价格偏好未确定时，让当前成员确认一次，并把决定写入 Git 忽略的本地 tool selection。
- 平台仅支持原生 Auto、主/次模型或继承时，按 adapter 声明降级；无法验证逐 Agent 选模时不得宣称已经节省模型费用。
- 更换便宜模型主要优化费用和延迟，不等同于减少 token；真正的 token 优化仍依赖避免重复读取、限制轮次和只拆分上下文独立的任务。

当前已验证映射：

- `project-query-agent` / `wiki-query-agent` → `efficient-read`，`code-implementer` / `code-simplifier` → `efficient-execution`：Codex 均为 `gpt-5.6-luna` + `high`；Claude 均为 `sonnet` + `high`。两者分别保持只读与写入权限，负责索引、定向阅读、执行、输入输出和实现，不承担独立宏观架构决策。
- `solution-architect` → `advanced-reasoning`：Codex `gpt-5.6-sol` + `high`；Claude `sonnet`。

## 迁移到新项目

将完整 `zWorkFlow/` 目录放到新项目根目录，不要手工覆盖项目文件。随后让 Agent 读取 `zWorkFlow/setup/SETUP_NEW_PROJECT.md` 并执行完整 setup；setup 只为当前运行时自动识别或用户明确指定的工具创建缺失薄接口，其他成员首次运行时再增量补装，直接支持共享源的工具采用零复制适配，并从目标项目事实生成项目内容层 skills：

- `project-context`
- `project-architecture`
- `project-tooling`（通用规则 + 从目标项目发现的目录）
- 按目标项目领域生成的 `project-domain-*`
- `project-refactor-queue`
- `project-doc-sync`

架构设计层按目标技术栈选择性复用。TEngine 等技术栈专用 skill 不属于默认安装项，只有检测到目标项目实际使用时才接入。

目标是 Unity 项目、`Assets` 或配置的项目内源码根存在 C#，且 PowerShell 7（`pwsh`）可用时，setup 条件安装 `codebase-query`；不要求 `Assets/Scripts`。Windows 与 macOS 使用同一份 UTF-8、可移植路径索引。缺少 PowerShell 7 时可在获得用户许可后尝试安装；未获许可或失败则跳过并继续使用 Agent 原生检索，不阻塞迁移。
