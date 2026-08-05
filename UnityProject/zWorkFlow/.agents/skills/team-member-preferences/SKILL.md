---
name: team-member-preferences
description: 根据当前提出需求的团队成员，按昵称读取并应用其个人工作规范。用于用户提出、更新或要求遵守个人偏好、个人规范、团队成员专属约束、沟通风格、验收偏好、代码审查偏好时；也用于任务开始时以低 token 成本微调执行方式。
---

# Team Member Preferences

用最小上下文应用“提出需求者”的个人规范。

## Token 预算原则

只读两类文件：

1. `.agent-memory/team/MAINTAINERS.md`：身份 / 账号 / 环境用户名 → 昵称与规则文件。
2. 当前成员对应的 `.agent-memory/team/members/<nickname>.md`。

不要读取其他成员文件；不要把所有成员规范汇总进入口文档或 skill。

## 任务开始

1. 从 `.agent-memory/team/MAINTAINERS.md` 解析当前用户昵称。
2. 若存在 `.agent-memory/team/members/<nickname>.md`，读取并应用其中规则。
3. 若昵称未知且当前任务需要记录维护人或个人规范，先询问用户希望使用的昵称，并更新 `MAINTAINERS.md`。
4. 若没有个人规则文件，按项目通用 workflow 执行。

## 用户提出个人规范时

当用户说“以后我希望……”“对我来说……”“我的习惯是……”“某成员要求……”等，只记录适用于该成员个人的规则：

- 先确定昵称。
- 将规则追加到 `.agent-memory/team/members/<nickname>.md`。
- 规则保持短句，写清触发场景。
- 不把个人规则升级为项目通用规范，除非用户明确要求全团队适用。

## 个人规范文件格式

```markdown
# <nickname> — Personal Workflow Preferences

## Active Rules

- 场景：规则。

## Deprecated / Superseded

- 旧规则：替代说明。
```

## 冲突处理

优先级：

1. 用户当前明确指令
2. 项目 / 安全 / 架构硬规则
3. OpenSpec Intake Gate（仅当非平凡改动会改变功能、外部行为或公共运行契约时）
4. 当前成员个人规范
5. 通用风格偏好

个人规范不能覆盖安全边界、架构约束或验收标准。冲突时指出冲突并按更高优先级执行。
