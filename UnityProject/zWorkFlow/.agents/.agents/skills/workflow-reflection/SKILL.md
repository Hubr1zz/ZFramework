---
name: workflow-reflection
description: 统一踩坑沉淀、用户偏好沉淀和工作流自优化。用于任务收尾、用户纠正、重复踩坑、reference 与实际代码不一致，或需要更新 Agent 工作流规则时。
---

# Workflow Reflection

把一次性上下文和长期规则分开。只保存未来存在明确读取入口的内容；不要创建通用“轻量决策”或“踩坑”记忆池。

## 必读参考

- [Reflection.md](references/Reflection.md)：个人项目反思流程

## 记录位置

- `.agent-memory/team/members/<nickname>.md`：只适用于当前成员的偏好；按 `team-member-preferences` 处理
- `.agents/skills/` 或对应项目 reference：会影响未来同类执行的规则、已核验事实与排障约束
- OpenSpec / ADR：承重架构或功能决策
- Git、issue 或任务记录：一次性实现选择、普通修复过程和可由代码历史推导的事实

新功能设计只读取相关正式 Spec、ADR 和命中的项目 skill/reference，不读取泛化记忆目录。

## 触发条件

- 用户纠正工作方式
- 用户确认某个非显然做法值得保留
- 因规则缺失返工
- 发现 reference 与实际代码不一致
- 多次出现同类问题

## 处理规则

1. 先判断是否可从代码、正式文档或 git 历史直接推导；能推导则不写 memory。
2. 与现有规则不冲突的补充，可写入合适位置。
3. 与现有规则冲突或影响范围大时，先输出候选内容等待用户确认。
4. 找不到明确的未来读取入口时不持久化；不要为了“以后可能有用”另建记忆文件。
