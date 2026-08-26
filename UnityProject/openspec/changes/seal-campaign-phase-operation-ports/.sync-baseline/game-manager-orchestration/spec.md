---
schemaVersion: 2
category: architecture
title: "GameManager管理器"
---

# GameManager管理器 Specification

## Purpose

定义项目唯一组合根对全局生命周期、共享服务和三个阶段切换的调度边界；阶段内部的玩法规则与具体表现仍由各自子系统负责，避免全局管理器持续膨胀。

## Requirements

### Requirement: GameManager owns global phase orchestration
The project SHALL use one GameManager composition root to coordinate Settlement, Hunt, and BossFight lifecycle transitions.

#### Scenario: Transitioning between game phases
- **WHEN** a phase transition is requested
- **THEN** GameManager delegates state transition to PhaseManager and activates only the destination phase roots

### Requirement: GameManager delegates domain behavior
GameManager SHALL coordinate phase managers and shared services without implementing their internal gameplay rules.

#### Scenario: Entering a phase
- **WHEN** a destination phase becomes active
- **THEN** GameManager invokes that phase's entry boundary and leaves phase-specific behavior to the corresponding subsystem

### Requirement: One campaign lease owns three phase managers

ZFramework 管理的 CampaignRuntime SHALL 为每个战役世代创建并唯一持有 Settlement、Hunt 与 Showdown 三个 plain phase manager。GameManager SHALL 通过同一 CampaignRuntime lease 持有它们，只保留顶层 FSM host、跨阶段事务、startup/shutdown 与共享场景根；不得建立平行 MonoBehaviour 或全局阶段单例。

#### Scenario: The campaign runtime shuts down

- **WHEN** GameManager lease 被释放或 ZFramework Campaign Module 关闭
- **THEN** Showdown、Hunt、Settlement phase manager SHALL 按依赖逆序释放
- **AND** 每个阶段的 runtime generation、ActionSession、表现绑定与回调 SHALL 全部失效

### Requirement: Phase managers own runtime generations

SettlementPhaseManager 与 HuntPhaseManager SHALL 分别拥有其配置、候选 generation 集合、当前权威 generation、generation counter 以及 prepare/swap/release/reset 生命周期。ShowdownPhaseManager SHALL 唯一拥有当前 PlayableCombatSession 的 prepare/start/update/dispose 生命周期，但本阶段 SHALL NOT 增加或修改战斗规则。

#### Scenario: A candidate replaces the current generation

- **WHEN** phase manager 接受与 expected current 身份匹配的候选 generation
- **THEN** 候选 SHALL 成为唯一 current
- **AND** 不属于该战役、已发布或过期的候选 SHALL 被拒绝

### Requirement: Stale phase callbacks cannot control the campaign

阶段表现与 session callback SHALL 验证当前 campaign/runtime generation、manager identity 和 active session。旧 generation 在 swap、reset 或 dispose 后晚到的 Hunt completion、encounter 或 checkpoint callback SHALL 被忽略，不得保存、结算或切换全局阶段。

#### Scenario: An old Hunt callback arrives after replacement

- **WHEN** 新 Hunt generation 已成为 current，旧 manager 随后报告完成或检查点
- **THEN** Campaign SHALL 保持新 generation 与当前阶段不变
- **AND** SHALL NOT 写入旧 HuntRecord 或活动狩猎检查点
