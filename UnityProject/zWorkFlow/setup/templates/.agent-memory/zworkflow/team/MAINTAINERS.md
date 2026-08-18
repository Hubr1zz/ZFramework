# Maintainers

本文件把 AI 工具、系统账号或团队成员名映射到可审计的维护人身份。无法确认真实成员时使用稳定工具 ID 或 `未注明`，不得猜测姓名。

## 成员映射

| 工具 / 账号 / 环境用户名 | 维护人显示名 | 个人规范文件 | 备注 |
| --- | --- | --- | --- |
| codex | codex | - | 默认 Codex Agent |
| claude | claude | - | 默认 Claude Agent |
| cursor | cursor | - | 默认 Cursor Agent |
| copilot | copilot | - | 默认 GitHub Copilot Agent |
| gemini | gemini | - | 默认 Gemini CLI Agent |
| windsurf | windsurf | - | 默认 Windsurf Agent |
| kimi | kimi | - | 默认 Kimi Code CLI Agent |

## 维护规则

- 第一次遇到未知成员身份时询问用户昵称，再追加映射。
- 个人规范写入 `.agent-memory/zworkflow/team/members/<nickname>.md`。
- 执行任务时只读取当前成员的个人规范文件。
