---
name: workflow-refactor
description: 通用重构与代码简化工作流。用于重构、优化、维护、降低复杂度、补注释、移除重复代码、整理命名与结构；遵守目标项目实际采用的架构约束。
---

# Workflow Refactor

重构只改变内部结构，不改变外在行为。实现新功能与重构要分开做；如果用户把两者混在一起，先澄清目标与范围。只有功能、外部可观察行为或公共运行契约发生变化的部分才使用 `openspec-intake-gate`。

## 必读参考

- [Optimize.md](references/Optimize.md)：个人项目重构原则、坏味道、决策规则、总结格式
- [WORKFLOW.md](references/WORKFLOW.md)：新功能与重构协作流程
- [code-simplifier.codex-agent.toml](references/code-simplifier.codex-agent.toml)：代码简化 agent 角色

## 执行规则

1. 先读入口文档和本 skill。
2. 根据用户范围读取相关代码和项目内容 skill。
3. 对问题分类：低风险可直接改；高风险只记录建议；不确定项先问用户。
4. 小步执行，每步保持行为不变。
5. 修改后运行项目可用验证；无法运行时说明原因。
6. 若踩坑或发现规则缺口，交给 `workflow-reflection` 沉淀。

## 默认优先级

1. 命名修正
2. 提炼函数
3. 移除重复和死代码
4. 搬移与封装
5. 架构级重构

架构级但行为保持的重构优先使用 `architecture-review`，不进入 OpenSpec。只有改动会新增/改变功能、外部可观察行为或公共运行契约，或用户明确调用 OpenSpec/zWorkFlow 时，才进入 OpenSpec。
