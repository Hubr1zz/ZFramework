# Maintainers

本文件用于把 AI 工具 / 系统账号 / 团队成员名映射到可审计的维护人身份。

Agent 记录 `project-refactor-queue` 的 `维护人` 时，按以下顺序解析：

1. 如果当前会话或环境能识别账号，先在下表查找映射。
2. 如果识别到账号 / 环境用户名但下表没有映射，并且当前任务可以向用户确认，先询问用户希望使用的维护昵称。
3. 用户给出昵称后，把账号和昵称追加到下表；本次及后续记录都使用该昵称。
4. 如果能识别工具但不能识别真实成员，使用适配器注册表中的稳定 tool id。
5. 如果无法识别或处于非交互环境，写 `未注明`，并在维护备注说明原因。

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

- 不要把推测的真实姓名写入维护人字段。
- 如果团队成员共用机器，第一次遇到未知身份时应询问用户昵称，并更新本表。
- 如果成员提出只适用于自己的规范，写入其 `个人规范文件`，不要写入口文档。
- 执行任务时只读取当前成员对应的个人规范文件，不读取全员规范。
- 如果 CI / 自动化脚本执行维护，建议单独登记为 `ci-bot`、`build-agent` 等身份。
