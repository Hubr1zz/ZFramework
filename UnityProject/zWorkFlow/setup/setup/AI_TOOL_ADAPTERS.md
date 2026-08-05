# AI Tool Adapters

本流程让同一仓库中的不同成员同时使用不同 AI 编程工具。适配器只负责工具入口、命令与 Agent 壳层；完整规则始终来自 `AGENTS.md`、`.agents/skills/` 和 `.agents/agent-roles/`。

## 权威注册表

`setup/adapters/registry.json` 是工具适配能力、检测信号、入口格式和降级规则的结构化来源，结构由同目录 `registry.schema.json` 约束。setup 不得在主流程中继续硬编码新的 `if Codex` / `if Claude` 分支；新增工具时先扩展注册表，再补必要的薄模板和验收项。

当前正式支持：

- OpenAI Codex
- Claude Code
- Cursor
- GitHub Copilot
- Gemini CLI
- Windsurf
- Kimi Code CLI（兼容旧称 Kimi CLI）

## 团队多工具原则

- 仓库不保存唯一 `activeTool`，也不因最后一次 setup 改写团队默认工具。
- 多个适配器可同时存在；每个成员的工具只读取自己的专属入口和共享源。
- 团队共享：适配器注册表、未冲突的薄入口、`.agents/`、OpenSpec 和项目级工作流数据。
- 个人本地：当前工具、CLI 版本、检测时间和个人启用偏好，按昵称写入 `.agent-memory/zworkflow/local/tool-selections/<nickname>.json`，必须被 Git 忽略。即使多人共享同一工作目录也不得互相覆盖。
- 不读取或修改其他成员 Home 目录下的全局 AI 配置；只有当前成员明确授权时才检查当前机器的 CLI 版本。

## 检测优先级

setup 对每个成员独立执行检测，按以下顺序累计证据，不做互斥选择：

1. 当前执行 setup 的运行时身份。
2. 用户本次显式指定的工具列表。
3. 项目中已存在的工具专属文件或目录。
4. 当前机器可验证的 CLI 可执行文件与版本。

`AGENTS.md` 和 `.agents/skills/` 是跨工具共享信号，不能单独用于判断某个具体工具正在使用。项目标记只表示仓库可能支持该工具，不代表当前成员选用了它。

安装资格固定为 `active-or-explicit-only`：只安装当前执行 setup 的 AI 运行时，或用户本次明确追加的平台。`available`、`repository-supported`、历史成员的本地选择和单纯存在的项目标记都不触发安装，更不能据此一次性安装所有平台。

- 当前对话能可靠暴露 Codex、Claude Code、Gemini CLI 等运行时身份时自动识别，不询问工具。
- 运行时身份缺失或无法唯一映射注册表时，才询问当前成员正在使用哪个工具。
- 其他成员首次使用时，由其在自己的工具中运行一次 setup；也可以由用户明确列出需要预装的平台。不要要求某一位成员枚举全团队工具。
- 同一成员之后更换或增加工具时，只补装新工具缺失的 artifact；已有适配器和其他成员入口保持不变。

检测结果分为：

- `active`：当前运行时或用户明确指定。
- `available`：本机存在且版本可验证，但当前未使用。
- `repository-supported`：仓库已有适配文件，但无法确认本机安装。
- `unknown-version`：识别到工具但无法确认版本；只启用稳定入口。

## 子 Agent 模型需求与自动路由

`registry.json` 的 `modelRequirementProfiles` 与 `roleModelRequirements` 是平台无关的需求源；adapter 的 `modelRouting` 只描述当前平台怎样满足这些需求。共享层不得保存厂商模型名，平台层不得反向改变角色职责。

setup 按以下顺序处理当前 active 或用户明确指定的平台：

1. 读取角色的 profile、推理强度与权限要求。
2. 只从当前运行时公开的 Agent 选项、模型列表或已配置主/次模型获取候选；不得读取其他成员 Home 配置，也不得根据营销名称猜测能力。
3. 先应用 adapter 中已验证且当前运行时确认可用的映射；没有静态映射时，根据成员偏好 `cost-first`、`balanced`、`quality-first` 或 `platform-auto` 筛选候选。
4. 只有映射已验证，或筛选后恰好剩一个满足需求的候选时才自动选择。模型不可用、候选不唯一、账号/组织策略不可见或逐 Agent 选模能力仍需验证时，向当前成员展示建议和降级影响，并只确认一次。
5. 把成员偏好、已确认的 tool/profile → model 决定、验证时间和运行时版本写入 `.agent-memory/zworkflow/local/tool-selections/<nickname>.json`。这些记录不进入共享 registry、能力指纹或团队默认值。
6. 平台支持原生 Auto 但不公开稳定逐 Agent 映射时，只在成员选择 `platform-auto` 或 adapter fallback 为 `native-auto` 时使用 Auto，并报告无法保证固定成本层级。
7. 平台只支持主/次模型时，仅在当前模式确认该能力已启用后路由；否则继承主模型。
8. 平台不支持或无法验证逐 Agent 选模时，继承当前模型或由单一主 Agent 完成，不生成虚假的模型配置。

模型路由优化的是模型费用、延迟和上下文隔离。setup 不把“使用便宜模型”表述为必然减少 token；token 审计仍检查重复读取、最大轮次和无价值拆分。

| 工具 | 逐 Agent 选模 | 自动策略 | 不确定时 |
| --- | --- | --- | --- |
| Codex | 原生支持模型与 reasoning effort | 当前运行时验证 registry 映射 | 警告并建议已声明 fallback，不静默替换 |
| Claude Code | 原生 Agent model；effort 需运行时验证 | 验证 `haiku` / `sonnet` alias 后应用 | 当前成员确认 |
| Cursor | 运行时能力可能变化 | 仅唯一且已验证的 Agent 选项 | 原生 Auto 或继承，不从通用模型 picker 推断 |
| GitHub Copilot | 自定义 Agent 可选模型，受账号/组织策略影响 | 运行时唯一匹配 | 原生 Auto |
| Gemini CLI | 原生 Agent 可指定模型 | 从当前 Agent 选项唯一匹配 | 当前成员确认 |
| Windsurf | 当前 adapter 未验证 | 不自动生成 | 单一主 Agent |
| Kimi Code CLI | 条件式主/次模型 | 已确认启用时 economy → secondary，其余 → primary | 继承当前模型 |

## 安装与冲突

1. 直接支持 `AGENTS.md` 和 `.agents/skills/` 的工具不创建 Skill 副本。
2. 工具需要专属入口时，只按注册表 `install.artifacts` 从分发包创建薄 wrapper；一次 setup 只处理当前 active 与本次明确指定的工具，不要求逐个手工复制。
3. 可选命令、prompt、workflow 和原生 Agent 壳层只在用户需要该能力且目标路径未占用时安装。
4. setup 前已经存在的文件先保持只读；若它是完整工具专属正文，按 `AGENT_WORKFLOW_COEXISTENCE.md` 完成共享迁入和校验后再薄化。目标路径冲突时标记 `reuse-existing`、`supplement-only` 或 `conflict`，不得覆盖。
5. 未知或低版本工具使用 `AGENTS.md + 自然语言/Skill 显式调用` 的最低兼容模式。
6. setup 不安装 AI 工具、不登录账号、不写入密钥，也不修改用户级配置。条件 skill 所需的本机运行时（例如 PowerShell 7）只允许在用户明确许可后按 `SETUP_NEW_PROJECT.md` 尝试安装；未获许可或失败时回退，不得把运行时安装与 AI 工具适配混为一谈。

`install.mode = shared-direct` 表示该工具直接读取共享入口，setup 不创建工具目录；`copy-thin-if-missing` 表示按清单递归复制薄接口。每个目标仍逐文件执行“仅缺失时创建”，因此同一次 setup 可以一键适配多个工具，同时不会覆盖团队成员已有配置。

模型路由决定不改变上述安装边界。setup 只安装 registry 已声明且分发包实际提供的缺失薄壳；没有模板时不得仅为了保存解析出的模型名生成新 wrapper。未完成内容保全迁移或存在冲突的 Agent 文件继续保持只读；已验证迁入共享源的完整 Agent 正文可以替换为薄壳。运行时支持直接传递模型参数时，优先使用本地解析结果调用。

`install.artifacts[].source` 相对于分发包 `zWorkFlow/` 根目录解析，`target` 相对于目标项目根目录解析。目录 artifact 必须声明 `recursive: true`，setup 展开后逐文件安装，不能用目录覆盖操作绕过冲突检查。

## 工具调用映射

| 工具 | 项目入口 | Skills | 显式调用 |
| --- | --- | --- | --- |
| Codex | `AGENTS.md` | `.agents/skills/` | 直接点名 skill |
| Claude Code | `.claude/CLAUDE.md` | `.claude/skills/` 薄 wrapper | `/opsx:*` 或 skill |
| Cursor | `AGENTS.md` | `.agents/skills/` | slash skill；旧版本回退入口指引 |
| GitHub Copilot | `AGENTS.md` | `.agents/skills/` | skill 或可选 prompt |
| Gemini CLI | `GEMINI.md` 薄 wrapper | `.agents/skills/` | skill 或可选 TOML command |
| Windsurf | `AGENTS.md` | `.agents/skills/` | `@skill` 或可选 workflow |
| Kimi Code CLI | `AGENTS.md` | `.agents/skills/` | `/skill:<name>` |

## Kimi CLI 兼容规则

Kimi Code CLI 直接发现项目级 `AGENTS.md` 与 `.agents/skills/`，因此默认不创建 `.kimi-code/skills/` 副本。`kimi --version` 只作为可选版本探测；检测到旧 Kimi CLI 时：

- 保留根 `AGENTS.md` 作为稳定入口。
- 如果该版本无法发现 `.agents/skills/`，报告 Skills 自动发现降级，要求通过提示显式读取对应 `SKILL.md`。
- 不自动迁移 `~/.kimi/`、会话、凭据或模型配置。

## 验证

setup 完成时至少验证：

- `registry.json` 可解析且 adapter id 唯一。
- 每个 adapter 都有安装模式；`copy-thin-if-missing` 的每个 source 与共享来源实际存在。
- 新建 wrapper 只引用共享源，不包含完整 Skill 正文。
- 已有完整 Claude/Codex skill 已迁入共享源或因明确冲突保留；不得同时无说明地维护第二份正文。
- 同一仓库同时存在两个以上工具适配器时，入口之间没有互相覆盖或声明唯一 active tool。
- 本地 tool selection 路径已被 `.gitignore` 排除。
- 每个角色引用的 profile 存在，每个 adapter 都声明 `modelRouting`，静态 selector 只在运行时确认可用后应用。
- 自动选择只发生在已验证映射或唯一运行时候选；歧义决定只保存在当前成员本地文件中。

本地选择文件从 `setup/templates/.agent-memory/zworkflow/local/tool-selection.json` 初始化，并以当前成员昵称命名；它只记录当前成员本机事实，不参与团队能力路由或来源指纹。
