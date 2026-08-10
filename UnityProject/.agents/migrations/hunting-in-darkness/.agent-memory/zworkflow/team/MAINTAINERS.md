# Maintainers

本文件用于把 AI 工具 / 系统账号 / 团队成员名映射到可审计的维护人身份。

`project-refactor-queue` 和 Unity「增量维护工作台」记录 `维护人` 时，优先按本表解析。

如果 Agent 识别到账号 / 环境用户名但本表没有映射，并且当前任务可以向用户确认，应先询问用户希望使用的维护昵称；用户给出昵称后，把映射追加到本表。本次及后续维护记录都使用该昵称。

如果无法向用户确认，才临时退回当前系统用户名、适配器注册表中的稳定 tool id 或 `未注明`。

| 工具 / 账号 / 环境用户名 | 维护人显示名 | 个人规范文件 | 备注 |
| --- | --- | --- | --- |
| codex | codex | - | 默认 Codex Agent |
| claude | claude | - | 默认 Claude Agent |
| cursor | cursor | - | 默认 Cursor Agent |
| copilot | copilot | - | 默认 GitHub Copilot Agent |
| gemini | gemini | - | 默认 Gemini CLI Agent |
| windsurf | windsurf | - | 默认 Windsurf Agent |
| kimi | kimi | - | 默认 Kimi Code CLI Agent |
| leonz | leonz | `.agent-memory/zworkflow/team/members/leonz.md` | 本机用户，可按团队习惯改成真实姓名或昵称 |

## 维护规则

- 不要把推测的真实姓名写入维护人字段。
- 如果团队成员共用机器，第一次遇到未知身份时应询问用户昵称，并更新本表。
- 如果成员提出只适用于自己的规范，写入其 `个人规范文件`，不要写入口文档。
- 执行任务时只读取当前成员对应的个人规范文件，不读取全员规范。
- 如果 CI / 自动化脚本执行维护，建议单独登记为 `ci-bot`、`build-agent` 等身份。
