# Agent Memory

这是所有受支持 AI 工具共享的项目级、可审计记忆目录。

本目录只保存有明确消费者的团队身份与 zWorkFlow 集成状态。强制规则、排障约束和项目事实写入对应 `.agents/skills/` 或正式项目文档；承重决策写入 ADR/OpenSpec；其余依赖 Git、issue 或任务记录。

目录：

- `team/`：团队成员、账号、Agent 工具身份映射；用于增量维护工作台记录“谁维护了某项内容”。
- `team/members/`：团队成员个人规范。执行任务时只读取当前成员对应文件，不读取全员规范。
- `zworkflow/integration/`：团队共享的仓库能力映射，不保存唯一 active tool。
- `zworkflow/local/`：当前成员的工具、版本和本地偏好；必须被 Git 忽略。

不创建通用 `decisions/` 或 `problems/`：它们没有稳定读取路由，容易形成只写不读的重复记忆。
