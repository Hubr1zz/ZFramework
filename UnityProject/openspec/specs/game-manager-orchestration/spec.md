---
schemaVersion: 2
category: architecture
title: "GameManager管理器"
---

# GameManager管理器 Specification

## Purpose

定义 Unity 组合外壳、战役流程协调器、ZFramework CampaignRuntime 与三个阶段管理器之间的最终所有权边界，避免后续功能再次把跨阶段编排堆回 GameManager。

## Requirements

### Requirement: GameManager is a Unity composition shell
GameManager SHALL retain serialized scene references, Unity lifecycle callbacks, presentation assembly, EventBus adapters and public compatibility APIs. It SHALL delegate campaign lease ownership, phase transitions, persistence and cross-phase transactions to one plain C# CampaignFlowCoordinator.

#### Scenario: Transitioning between game phases
- **WHEN** a public command or Unity event requests a phase transition
- **THEN** GameManager forwards it to CampaignFlowCoordinator
- **AND** only the committed phase callback changes scene roots and camera state

### Requirement: GameManager delegates domain behavior
GameManager SHALL NOT implement campaign transaction host interfaces or own CampaignRuntime, phase manager, ActionSession, persistence coordinator, or gameplay transaction fields.

#### Scenario: Entering a phase
- **WHEN** gameplay or persistence behavior is added
- **THEN** it is implemented in GameCore, a phase manager/session, or a dedicated cross-phase transaction
- **AND** GameManager remains a facade over CampaignFlowCoordinator

### Requirement: CampaignFlowCoordinator owns one campaign flow lease

CampaignFlowCoordinator SHALL be the sole owner of the acquired CampaignRuntime lease, phase ports, CampaignPersistenceCoordinator and the Startup, Restart, Departure, Return, Encounter, ShowdownOutcome and ActiveHuntRestore transactions.

#### Scenario: Campaign flow shuts down

- **WHEN** GameManager is destroyed
- **THEN** it unsubscribes Unity event adapters and disposes CampaignFlowCoordinator once
- **AND** the coordinator invalidates in-flight flow/persistence work before releasing the CampaignRuntime lease

### Requirement: Continue and replacement load share one restore algorithm

Continue and developer replacement load SHALL call one snapshot restore boundary. Settlement restore SHALL prepare and validate a candidate before publication; pre-commit failure SHALL restore the previous phase/generation, while post-commit event or pending-return recovery failure SHALL retain the newly published generation behind its recovery gate.

#### Scenario: A settlement replacement fails before session activation

- **WHEN** candidate publication or destination session activation fails
- **THEN** the previous phase, settlement generation and presentation SHALL remain authoritative
- **AND** only the uncommitted candidate SHALL be released

#### Scenario: An active Hunt snapshot is continued

- **WHEN** a compatible active Hunt snapshot is loaded
- **THEN** ActiveHuntRestoreTransaction SHALL atomically restore Settlement, Hunt, route, event occurrences and stable payload through the same coordinator entry

### Requirement: Source phase remains recoverable until destination activation

CampaignFlowCoordinator SHALL NOT release the source Hunt or Showdown generation merely because the FSM accepted Settlement. The destination Settlement ActionSession and minimum 3D presentation SHALL become available before the source generation is retired.

#### Scenario: Settlement activation fails after an accepted transition

- **WHEN** the FSM accepts Settlement but the destination ActionSession cannot activate
- **THEN** the coordinator SHALL restore the source phase while retaining its runtime and session
- **AND** the transition SHALL return failure without publishing a partially playable Settlement

### Requirement: Stale asynchronous restore work cannot outlive the flow lease

Startup, Continue and replacement load operations SHALL carry a coordinator operation generation. Reset or Dispose SHALL invalidate the generation before releasing phase/runtime owners, and late continuations SHALL exit without invoking the disposed host.

#### Scenario: GameManager is destroyed while a load awaits persistence

- **WHEN** the persistence continuation resumes after CampaignFlowCoordinator disposal
- **THEN** it SHALL NOT restore, reset or publish through the released CampaignRuntime

### Requirement: One campaign lease owns three phase managers

ZFramework 管理的 CampaignRuntime SHALL 为每个战役世代创建并唯一持有 Settlement、Hunt 与 Showdown 三个 plain phase manager。CampaignFlowCoordinator SHALL 持有同一 CampaignRuntime lease；不得建立平行 MonoBehaviour 或全局阶段单例。

#### Scenario: The campaign runtime shuts down

- **WHEN** CampaignFlowCoordinator lease 被释放或 ZFramework Campaign Module 关闭
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
