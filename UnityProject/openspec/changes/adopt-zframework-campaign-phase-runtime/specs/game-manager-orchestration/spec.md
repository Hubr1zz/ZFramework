---
schemaVersion: 2
category: architecture
title: GameManager管理器
---

## MODIFIED Requirements

### Requirement: ZFramework Module owns global phase runtime

项目 SHALL 使用一个 ZFramework Campaign Runtime Module 独占持有 Settlement、Hunt 与 BossFight 的阶段 FSM。`GameManager` SHALL 作为当前运行世代的场景宿主，通过 lease 请求阶段切换并根据结果激活目标阶段根，不得自行创建平行阶段 FSM。

#### Scenario: GameManager 获取阶段运行世代

- **WHEN** 场景中的权威 GameManager 完成基础表现装配
- **THEN** 它从 Campaign Runtime Module 获取唯一活动 phase lease
- **AND** 第二个并发 lease 请求被拒绝

#### Scenario: Transitioning between game phases

- **WHEN** a phase transition is requested
- **THEN** GameManager delegates state transition through the active module lease
- **AND** activates only the destination phase roots

### Requirement: Failed startup resets the current generation

启动或恢复失败 SHALL 重置当前 lease 内的 FSM，但 MUST NOT 释放模块占用或建立第二套权威运行态。场景宿主销毁或框架 Shutdown SHALL 幂等释放当前 lease。

#### Scenario: 玩家在读档失败后重试

- **WHEN** 活动狩猎恢复未能完成且当前场景宿主仍有效
- **THEN** 当前 phase lease 回到未启动的 Settlement 默认状态
- **AND** 同一 GameManager 可再次尝试启动战役

#### Scenario: 场景宿主销毁后重新进入

- **WHEN** 当前 GameManager 销毁并释放 phase lease
- **THEN** 后续 GameManager 可获取具有更大 GenerationId 的新 lease
