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
4. 只有会新增或改变功能、外部可观察行为或公共运行契约的非平凡产品功能或运行框架改动才过 `openspec-intake-gate`。行为保持型重构和实现方案调整即使发生在框架代码中也不进入；用户显式调用 OpenSpec/zWorkFlow 时除外。
5. 成员提出个人规范时，更新 `.agent-memory/zworkflow/team/MAINTAINERS.md` 与对应 `.agent-memory/zworkflow/team/members/<nickname>.md`；不要写入口文档。
6. 修改完成后检查旧绝对路径、工具私有 memory、重复完整 skill 副本和团队级唯一 active tool 是否残留。
   工具专属目录不得保存项目事实、维护队列或完整功能文档；这些内容必须归属到对应 `.agents/skills/<功能>/references/`。
7. 设计文档转 Spec 必须显式触发；普通问答不得扫描整个设计仓库。
   已有至少一个有效的 `openspec/design-source.json` 来源时不得要求用户重复输入路径，也不得被 setup 空配置覆盖。多个来源地位相同，类型参数只能用于扫描后的语义过滤。
8. 发布带缺失 Spec 与允许实现分开判断；硬前置 accepted 后仍阻塞实现。
9. 设计 Spec 扫描必须有 Agent 原生路径；可选脚本缺失时不得要求用户安装运行时。
10. 文档包与项目包独立；项目包维护 `openspec/implementation-ledger.json`，其中只保存设计来源 ID、相对文档路径、实现基线和必要摘要，不保存文档库绝对路径。项目桥接以全部“设计文档路径”为唯一来源：至少一个有效目录即可点亮桥接灯，并按来源分别重建可折叠文档树；文档包不创建或维护该账本，任何一方都不得因文档变化自动调用“设计导入”或创建 proposal。
11. `.DS_Store`、Python 缓存、`openspec/workbench-config.json`、`openspec/design-source.json`、`.agent-memory/zworkflow/team/MAINTAINERS.md`、`.agent-memory/zworkflow/team/members/` 与 `.agent-memory/zworkflow/local/` 属于系统生成物、个人偏好或机器路径，必须由 `.gitignore` 排除；项目级 Spec、Change、依赖、Gap 与共享适配器仍正常纳入版本管理。
12. 新增持久化产物前必须注明权威内容、索引或审计三种角色，并至少有一个明确消费者（Workbench、CLI、validator、apply/sync 或人工审计）。索引与审计只能保存引用、hash 和必要摘要，不得复制 Spec/Review 正文；没有消费者的产物不生成。
13. `openspec/localization.json` 和 `openspec/translations/` 必须进入 Git。前者保存以后生成权威工件的默认语言，以及以 capability ID 为键的中英文 Spec 条目显示名；后者是 Workbench 的非权威显示副本与块级 hash 索引。显示名与翻译均不得被 apply、sync、validator 或 Agent 事实读取替代原 Spec，依赖和生命周期引用仍使用稳定 ID。
14. 已有工具工作流接入采用“清点 → 迁入共享源 → 路径更新 → 哈希/引用校验 → 原路径薄化”的事务。根 `CLAUDE.md`、`.claude/skills/`、`.codex/skills/` 中有价值的项目规则不得丢弃，也不得继续作为第二份权威正文；工具设置、凭据、用户历史和无法归类的内容不迁移。
15. 工程能力发现按稳定、可独立路由的模块拆分。README 的核心模块表、模块目录、公共接口、asmdef 和代码入口共同证明边界时，Resource、Event、Config、UI、Procedure 等应分别建条目；不得只生成一个覆盖整个框架的笼统能力。

## Agent 模型分层

先判断拆分是否会导致重复读取。一次设计导入、同一系统的诊断+方案或紧密耦合审查默认由一个负责人贯穿；只有子任务上下文独立，或主 Agent 能提供足够的已读摘要而无需重读时才分 Agent。项目存在 `project-context/references/PROJECT-INDEX.md` 时先读索引，否则先读 setup 映射出的等价项目速查。

- 共享需求只使用 `economy`、`coding`、`advanced-reasoning` 三种 profile，并分别表达推理强度、写权限和成本/质量优先级；权威结构位于 `setup/adapters/registry.json`。
- Codex 与 Claude 的模型名只是经过验证的平台映射示例，不作为其他平台的模型名翻译表。
- setup 只在当前运行时确认模型可用且映射唯一时自动选择；候选不唯一、账号策略不可见或价格偏好未确定时，让当前成员确认一次，并把决定写入 Git 忽略的本地 tool selection。
- 平台仅支持原生 Auto、主/次模型或继承时，按 adapter 声明降级；无法验证逐 Agent 选模时不得宣称已经节省模型费用。
- 更换便宜模型主要优化费用和延迟，不等同于减少 token；真正的 token 优化仍依赖避免重复读取、限制轮次和只拆分上下文独立的任务。

当前已验证映射：

- `project-query-agent` / `wiki-query-agent` → `economy`：Codex `gpt-5.5` + `low`（不可用时显式建议 `gpt-5.6-terra + low`）；Claude `haiku`。
- `solution-architect` → `advanced-reasoning`：Codex `gpt-5.6-sol` + `high`；Claude `sonnet`。
- `code-implementer` / `code-simplifier` → `coding`：Codex `gpt-5.6-sol` + `medium`；Claude `sonnet`。

## 迁移到新项目

将完整 `zWorkFlow/` 目录放到新项目根目录，不要手工覆盖项目文件。随后让 Agent 读取 `zWorkFlow/setup/SETUP_NEW_PROJECT.md` 并执行完整 setup；setup 只为当前运行时自动识别或用户明确指定的工具创建缺失薄接口，其他成员首次运行时再增量补装，直接支持共享源的工具采用零复制适配，并从目标项目事实生成项目内容层 skills：

- `project-context`
- `project-architecture`
- `project-tooling`（通用规则 + 从目标项目发现的目录）
- 按目标项目领域生成的 `project-domain-*`
- `project-refactor-queue`
- `project-doc-sync`

架构设计层按目标技术栈选择性复用。ZFramework 等技术栈专用 skill 不属于默认安装项，只有检测到目标项目实际使用时才接入。

目标是 Unity 项目、`Assets` 或配置的项目内源码根存在 C#，且 PowerShell 7（`pwsh`）可用时，setup 条件安装 `codebase-query`；不要求 `Assets/Scripts`。Windows 与 macOS 使用同一份 UTF-8、可移植路径索引。缺少 PowerShell 7 时可在获得用户许可后尝试安装；未获许可或失败则跳过并继续使用 Agent 原生检索，不阻塞迁移。
