# Agent Workflow Coexistence

本流程负责让 zWorkFlow 与项目已有 Agent 工作流共存。核心原则是：已有工作流只读、不可被 setup 修改；zWorkFlow 通过分析结果优化自己的路由，跳过重复步骤并减少后续上下文读取。

## 触发条件

仅在以下情况执行：

- 用户要求 setup、检测或导入 Agent 工作流。
- 用户先安装 zWorkFlow，之后又导入其他 Agent 工作流并要求检测。
- 项目先存在其他 Agent 工作流，之后安装 zWorkFlow 并要求 setup。

不得通过后台监控修改外部工作流。只有用户明确触发时才重新扫描。

## 只读发现

先读取 `setup/adapters/registry.json`，再扫描入口文档、skills、commands、agent 配置、自动化脚本、持久化目录和验证命令。扫描范围不得只硬编码 `.claude/` 与 `.codex/`；必须包含注册表中各工具声明的项目标记。对每项能力记录：

- 能力 ID 与职责。
- 触发条件。
- 必读文件与大致上下文成本。
- 写入的文件或外部状态。
- 验证方式。
- 与 zWorkFlow 能力的关系。
- 哪些工具和成员可以直接发现该能力，哪些需要薄 wrapper 或降级调用。

只读发现阶段不得修改已有入口、wrapper、skill、命令、配置、历史记录或持久化数据。发现完成后，完整 setup 对已验证可无损迁入共享 `.agents/` 的工具专属正文执行“先迁入、后薄化”；工具设置、凭据、历史记录、冲突内容和无法归类的数据仍不得修改。

## 内容保全迁移与薄化

1. 为每个 capability 建立源文件、hash、依赖 reference/script、触发入口和写入状态清单。
2. 完整 Skill 与项目规则迁入 `.agents/skills/`，角色迁入 `.agents/agent-roles/`；根项目指令中的代码流程进入 `project-context` 或命中领域 Skill 的 reference。
3. 替换所有工具私有正文路径、私有 memory 路径和机器绝对路径。
4. 对迁入结果做文件数量、hash/语义、链接和消费者验证；任一失败则保留原路径，不执行薄化。
5. Claude 原路径保留 wrapper；Codex 等可直接读取共享 Skills 的工具移除完整 skill 副本，只保留注册表要求的 agent/说明壳层。
6. 迁移前内容可放入 Git 忽略的本地恢复备份；备份不是权威源，验证后可由用户清理。

## 同类功能与冲突判断

| 关系 | zWorkFlow 的处理 |
| --- | --- |
| 同类且等价 | 标记 `reuse-existing`；zWorkFlow 跳过自己的重复步骤，只调用或遵守已有能力 |
| 同类但部分覆盖 | 标记 `supplement-only`；仅在用户任务确实需要缺失部分时运行补充步骤 |
| 同类且冲突 | 标记 `conflict`；列出双方规则、影响与建议，在用户决定前不自动选择或融合 |
| 无同类能力 | 标记 `zworkflow-only`；保留 zWorkFlow 原流程 |

“融合”只表示更新 zWorkFlow 自己的能力映射和路由摘要，不表示合并、改写或接管已有工作流。

## 多工具团队

- 能力映射以 capability 为主键、以 tool id 记录可达方式；不得为每个工具复制一份完整 capability。
- 同一仓库可同时处于 Codex、Claude Code、Cursor、GitHub Copilot、Gemini CLI、Windsurf 和 Kimi Code CLI 支持状态。
- 共享映射不得保存“最后执行 setup 的工具”为团队默认值。
- 当前成员的工具、版本和本地探测结果只写 `.agent-memory/zworkflow/local/tool-selections/<nickname>.json`；团队共享摘要只记录仓库支持状态。
- 当前成员的模型偏好与已解析模型同样只写本地 tool selection；共享能力映射只记录 profile 和 adapter 路由能力，不记录个人账号可用模型或选择结果。
- 一个工具入口冲突只阻塞该适配器，不得阻塞其他工具继续使用共享 `.agents/` 能力。

## 轻量缓存

将分析结果写入 zWorkFlow 自有目录：

- `.agent-memory/zworkflow/integration/workflow-map.json`：结构化能力映射、冲突和来源指纹。
- `.agent-memory/zworkflow/integration/routing-summary.md`：后续任务优先读取的精简路由摘要。

每个来源记录路径、大小、修改时间或可用哈希。后续任务先检查指纹：未变化时直接使用摘要，不重复读取完整工作流；发生变化或用户要求重新检测时才重建。这些缓存不得写入或引用为已有工作流的权威来源。

`workflow-map.json` 中每条 capability 至少记录 `id`、`existingSources`、`zworkflowCapability`、`relation`、`zworkflowAction`、`conflicts` 和 `requiredReads`。模板位于 `setup/templates/.agent-memory/zworkflow/integration/`。

## 输出

报告至少列出：发现的工作流、当前成员工具、仓库支持工具、各工具的原生/薄 wrapper/降级能力、子 Agent 模型路由的 resolved / native-auto / inherited / unresolved 状态、复用能力、zWorkFlow 跳过的步骤、只在需要时补充的步骤、冲突警告、预计减少的重复文件读取，以及已迁入/薄化、保持只读和因冲突未迁移的已有工作流文件。
