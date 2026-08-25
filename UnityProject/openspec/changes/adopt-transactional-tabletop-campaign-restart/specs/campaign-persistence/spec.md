---
schemaVersion: 2
category: feature
title: 战役持久化与恢复
---

## ADDED Requirements

### Requirement: Campaign restart establishes storage before runtime replacement

终局重启 SHALL 先准备 detached 初始 Settlement，等待旧战役所有恢复候选可靠删除，再写入候选稳定快照，最后发布 Settlement/Hunt generation 与 Settlement 阶段。删除未完成时 SHALL NOT 发布候选；任一步失败或取消 SHALL 释放候选并尽力恢复上一份稳定载荷。

#### Scenario: Delete completes after a new snapshot could have been written

- **WHEN** 持久化 Adapter 延迟完成旧战役删除
- **THEN** 重启命令 SHALL 等待删除结果
- **AND** 新战役快照 SHALL 只在删除成功后写入

#### Scenario: Confirmed deletion fails

- **WHEN** Adapter 无法确认主档、备份与临时恢复候选均已删除
- **THEN** 当前运行态 SHALL 保持权威并可再次提交重启
- **AND** detached 候选 SHALL 被释放

#### Scenario: Candidate runtime publication fails after storage replacement

- **WHEN** 新快照已写入但 generation CAS、ActionSession 或阶段发布失败
- **THEN** 新候选 SHALL 被撤销
- **AND** 系统 SHALL 尝试恢复重启前稳定载荷并报告明确失败
