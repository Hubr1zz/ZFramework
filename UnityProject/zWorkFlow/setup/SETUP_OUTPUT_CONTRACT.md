# Setup Output Contract

setup 完成后按本契约交付。核心验收项是：通用核心已安装、项目内容层来自目标项目事实、OpenSpec CLI 已验证或明确阻塞、适用的平台工作台已处理，且已有工作流未被修改。

## 允许写入的内容

只允许以下写入：

- `.agent-memory/zworkflow/integration/workflow-map.json`
- `.agent-memory/zworkflow/integration/routing-summary.md`
- `.agent-memory/zworkflow/local/tool-selections/<nickname>.json`（仅当前成员，本地忽略）
- 目标未占用时安装的根薄入口、skills、roles、团队空模板和 OpenSpec 空结构；人类文档只在 `zWorkFlow/` 内校验并由 Workbench 直接读取
- 未占用路径中的 zWorkFlow 自有能力文件
- Unity 项目中目标全部未占用时成套安装的工作台源码
- 用户明确要求生成的新增 System Spec、review 和依赖 metadata
- 用户明确同意安装的可选组件

任何已有工作流文件、配置、入口、历史和持久化数据都不得出现在“已更新”列表。路径冲突必须跳过并报告。

## 报告格式

```markdown
## Setup 完成报告

### 只读发现范围
- 项目事实来源：
- 架构资料来源：
- Agent 工作流来源：
- 未读取的高成本范围：

### 已有工作流保护
- 基线文件数量：
- 未经验证覆盖已有工作流文件：0
- 内容保全迁移：列出迁入共享源、已薄化、保留冲突和本地恢复备份；已验证薄化不计为覆盖。
- 跳过的路径冲突：
- 未触碰的历史 / 数据目录：

### Agent 工作流能力映射
| 能力 | 已有实现 | zWorkFlow 处理 | 冲突 | 后续读取成本 |
| --- | --- | --- | --- | --- |

### AI 工具适配
- 当前成员 active 工具：
- 本次实际安装工具（仅 active / 用户明确指定）：
- 当前机器 available 工具：
- 仓库 repository-supported 工具：
- 使用原生共享 Skills 的工具：
- 新增薄 wrapper：
- 降级或未知版本：
- 本地选择记录：`.agent-memory/zworkflow/local/tool-selections/<nickname>.json`
- 团队级唯一 active tool：无
- 当前成员模型偏好：cost-first / balanced / quality-first / platform-auto / 未设置
- 子 Agent 模型路由：按角色列出 resolved / native-auto / inherited / unresolved；具体个人 selector 只显示在本次报告，不写入团队缓存
- 需要用户确认的模型歧义：

### zWorkFlow 路由优化
- 项目路由入口：`reuse-existing:<path>` / `generated:.agents/skills/project-context/references/PROJECT-INDEX.md` / `conflict:<path>`
- 后续直接复用的能力：
- 被跳过的重复步骤：
- 仅在缺口出现时运行的步骤：
- 路由摘要：
- 来源指纹：

### System Spec
- 候选架构资料：
- 代码核验结果：
- 新增 capability：
- 复用 / 跳过 capability：
- 冲突或过时描述：
- 缺失依赖 Gap：

### zWorkFlow 自有写入
- 通用核心（installed / reuse-existing / conflict）：
- 项目内容层（generated / reuse-existing / conflict）：
- 平台工作台（installed / skipped-not-applicable / reuse-existing / conflict / blocked-openspec-cli）：
- 新增文件：
- 可选组件：
- 因已有等价能力而未安装的组件：
- 工程模块拆分：列出从总框架拆出的模块条目、证据和 partial 候选：

### 版本与平台兼容
- 功能覆盖清单版本：
- Unity Editor 版本：
- 操作系统：
- Node.js：available / blocked-node-missing；版本：
- OpenSpec CLI：available / installed-and-verified / blocked-node-missing / blocked-incompatible-version / install-failed；检测版本与最终版本：
- OpenSpec CLI 安装命令：未执行 / `npm install -g @fission-ai/openspec@^1.6.0`
- PowerShell 7：available / installed-with-consent / skipped-no-consent / install-failed / not-applicable
- codebase-query：installed / reuse-existing / skipped-condition / fallback-native；回退时注明原因
- 当前平台编译结果：
- Unity 6 宏分支检查：
- 未执行的实机验证：

### 待用户决定
- 工作流冲突：
- 架构冲突：
- 其他阻塞项：

### 下一步建议
1.
2.
```

## 质量要求

- 区分已确认事实、推断与建议。
- 同类功能必须标记 `reuse-existing`、`supplement-only`、`conflict` 或 `zworkflow-only`。
- 冲突只报告，不自动融合或选择一方。
- 架构 Requirement 必须有文档或代码证据，且代码端完成基本核验。
- 指纹未变化时，后续任务应读取精简摘要，不重复加载完整工作流。
- 如果任何已有工作流正文在未完成内容保全迁入、引用校验和可恢复备份时被修改，setup 判定失败并停止交付；已验证的薄化迁移必须单独列出，不视为覆盖。
- `FEATURE_COVERAGE.md` 中任一必需资产缺失、运行源码与 setup 模板哈希不一致或发布压缩包落后于来源目录时，setup 判定失败。
- `setup/adapters/registry.json` 无法解析、adapter id 重复、角色引用不存在的 model profile、adapter 缺少 model routing、安装策略不是 `active-or-explicit-only`、模板缺失或共享配置保存唯一 active tool 时，setup 判定失败。
- OpenSpec CLI 未通过兼容版本验证时，OpenSpec 命令能力必须报告为不可用，平台工作台必须为 `blocked-openspec-cli`，不得声称工作台安装成功。
- 项目路由入口既未复用也未生成，或生成的 `project-context/SKILL.md` 与 `references/PROJECT-INDEX.md` 缺少任一文件时，setup 判定失败。
