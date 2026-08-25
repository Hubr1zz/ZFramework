---
schemaVersion: 2
category: architecture
title: GameManager管理器
---

## MODIFIED Requirements

### Requirement: ZFramework Module owns global campaign runtime

项目 SHALL 使用一个 ZFramework Campaign Runtime Module 独占持有 Settlement、Hunt 与 BossFight 的阶段 FSM、跨阶段 ActionEnvironment installer registry 和 Campaign ActionSession。`GameManager` SHALL 作为当前运行世代的场景宿主，通过 lease 请求阶段切换并根据结果激活目标阶段根，不得自行创建平行阶段 FSM 或共享 registry。

#### Scenario: GameManager 获取阶段运行世代

- **WHEN** 场景中的权威 GameManager 完成基础表现装配
- **THEN** 它从 Campaign Runtime Module 获取唯一活动 phase lease
- **AND** 第二个并发 lease 请求被拒绝

#### Scenario: 阶段 Runner 使用共享效果环境

- **WHEN** Settlement、Hunt 或 Combat Session 创建自己的 ActionEnvironment
- **THEN** 它 attach 到当前 runtime 的唯一 installer registry
- **AND** 阶段 Session 只能注册或使用效果，不得销毁整个 registry

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

### Requirement: Campaign runtime owns settlement event restore gate

项目 SHALL 由当前 Campaign Runtime Generation 持有唯一已发布的营地事件恢复投影。读档、活动狩猎恢复或回营年度事件 MUST 先创建不可见候选，再显式发布；`GameManager` 不得保留平行投影字段。Reset、Dispose 与框架 Shutdown MUST 清除已发布投影。

#### Scenario: 恢复候选尚未发布

- **WHEN** 系统为候选营地数据创建事件恢复投影
- **THEN** 当前出猎门禁仍读取旧的已发布投影
- **AND** 候选只有显式发布后才成为当前权威

#### Scenario: 当前运行世代重置

- **WHEN** 启动或恢复失败并重置当前 Campaign Runtime Generation
- **THEN** 已发布的营地事件恢复投影被清除
- **AND** 后续启动不会继承旧存档的恢复门禁

### Requirement: Player campaign commands always use ActionQueue

战役启动完成后，玩家请求的阶段切换和遭遇开始 MUST 通过 runtime 内的 Campaign ActionSession 执行。若该 Session 不可用，命令 MUST 失败，不得直接调用阶段 Host 绕过 Reactor、Gate 或串行执行。

#### Scenario: Campaign Runner 不可用

- **WHEN** 玩家在战役标记为活动但 Campaign ActionSession 不可用时请求阶段切换
- **THEN** 请求返回失败
- **AND** 当前阶段不发生变化
