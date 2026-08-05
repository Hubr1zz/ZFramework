# Reusable Workflow Structure

zWorkFlow 提供一组可选能力，而不是要求目标项目采用固定工作流结构。setup 先分析已有能力，再决定哪些步骤应复用、跳过、按需补充或等待冲突处理。

## 能力层次

### 请求与方案能力

- 需求完整性检查。
- OpenSpec proposal、apply、sync、archive。
- 显式设计文档导入与 Draft Change。
- 架构审查、重构和复盘。

### 项目上下文能力

- 项目事实速查。
- C# 项目可选的本地结构索引、类型绑定、调用者和影响范围查询。
- 架构边界与依赖规则。
- 领域说明。
- 维护队列、文档索引和成员偏好。

### 工具适配能力

- 不同 Agent 工具的入口或 wrapper。
- 由 `setup/adapters/registry.json` 描述的多工具能力发现、版本降级与团队并存策略。
- 平台无关的角色模型需求 profile，以及基于当前运行时选项的逐平台模型路由、用户一次性确认和安全降级。
- 可选工作台、脚本和外部文档桥接。

每项能力都必须先与已有工作流比较。已有等价能力时复用，不安装重复项；路径碰撞时跳过，不覆盖。

工具适配器不得成为第二套业务规则。Codex、Claude Code、Cursor、GitHub Copilot、Gemini CLI、Windsurf 与 Kimi Code CLI 都应优先读取共享 `.agents/skills/`；只有工具不直接支持共享格式时才生成薄 wrapper。仓库支持多个工具是常态，不存在团队级唯一 active tool。

角色只声明 `economy`、`coding` 或 `advanced-reasoning` 需求；厂商模型 selector 属于 adapter，并且必须由当前运行时验证。个人的成本/质量偏好和解析结果属于本地状态，不能进入共享角色、团队能力映射或其他成员配置。

完整功能内容和可变共享状态必须位于其能力专属目录。zWorkFlow 自有保护清单与增量维护队列固定使用 `.agents/skills/project-refactor-queue/references/{PROTECTED_FILES,REFACTOR_QUEUE}.md`，并按需分别读取；不得把队列、项目事实或完整规范放入 `.claude/`、`.codex/`、`.gemini/` 等工具目录。

## 动态路由

setup 将能力关系写入 `.agent-memory/zworkflow/integration/workflow-map.json`，并生成精简 `routing-summary.md`。后续任务：

1. 先检查来源指纹是否变化。
2. 未变化时只读取路由摘要和当前任务必要文件。
3. 等价能力直接使用已有实现，跳过 zWorkFlow 重复流程。
4. 部分覆盖仅在任务触及缺口时加载补充能力。
5. 冲突能力在用户决定前不自动调用任何一方完成有争议写入。

## 设计 Spec 数据

- 暂存导入：`openspec/design-imports/<run-id>/`
- 唯一 Draft 内容：`openspec/drafts/changes/<change-id>/`（可包含配对规则与实现 capability）
- 导入批次通过 `draft-refs.json` 引用 Change；同 capability 在 Draft 中原位更新，正式工件有差异时报告冲突，不创建同义副本。
- 正式行为：`openspec/specs/`
- 依赖与来源：`openspec/spec-metadata/`
- 分类：`system`、`feature`、`game-rule`；读取 legacy `architecture` 时按 `system` 处理
- 依赖方向：System 到 System、Feature 到 System 或 Feature、规则到 Feature
- Gap：仅表示依赖树中缺失的节点或契约

项目内部的架构资料在用户要求检测或导入时走架构发现流程；外部设计文档仍只在用户显式指定时导入。

## 质量反馈

setup 可报告以下风险，但不得替用户修改已有工作流或项目设计：

- 同类 Agent 能力重复执行。
- 入口或触发规则冲突。
- 文档与代码不一致。
- 缺少验证入口或稳定模块边界。
- 每次任务重复读取大体积上下文。
- 来源缺少指纹，无法判断是否需要重扫。

报告必须包含对 zWorkFlow 自身路由的优化建议，以及需要用户决定的项目侧建议。
