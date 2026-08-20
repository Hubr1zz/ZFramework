---
schemaVersion: 2
category: architecture
title: "ActionQueue 全生命周期环境"
---

# Action Environment Lifecycle Specification

## Purpose

统一战役、营地、狩猎与战斗的权威流程执行边界，使后续装备、状态、事件和规则覆盖能够注册一次并安全投影到对应阶段，同时保持 GameCore、TEngine 事件与世界空间表现之间的单向依赖。

## Requirements

### Requirement: Each major gameplay lifetime owns one execution environment
The campaign SHALL retain one Campaign ActionEnvironment for the campaign lifetime, while Settlement, Hunt, and Combat SHALL each own a separate environment that is created on entry and disposed on exit.

#### Scenario: The campaign moves from Settlement to Hunt
- **WHEN** the Campaign ActionQueue commits the phase transition
- **THEN** the Settlement environment is detached and disposed, the Hunt environment is attached, and the Campaign environment remains active

#### Scenario: Multiple commands target one feature
- **WHEN** more than one root command is submitted to the same environment
- **THEN** the environment executes one complete causal chain at a time in root FIFO order

### Requirement: Campaign effects install across current and future environments
A campaign-owned Installer Registry SHALL allow an Adapter to register reactors, reaction gates, guards, or related leases once, filter installation by environment kind, and apply it to both matching active environments and matching environments created later.

#### Scenario: An effect is acquired during Settlement
- **WHEN** its Installer supports Settlement and Hunt
- **THEN** it is installed into the current Settlement environment and automatically installed into the next Hunt environment without phase constructors knowing the concrete effect type

#### Scenario: A phase environment exits
- **WHEN** its ActionEnvironment is disposed
- **THEN** every installation lease for that environment is released in reverse order without unregistering the Installer from other or future environments

#### Scenario: Installation fails partway
- **WHEN** an Installer throws after creating one or more leases
- **THEN** the incomplete installation and all earlier installations from that registration attempt are rolled back, and the registry remains usable

### Requirement: Cross-environment handoff uses committed facts
An ActionEnvironment SHALL stage TEngine EventBus facts in its Outbox and publish them only after the owning root reaches an allowed commit boundary; phase environments SHALL NOT synchronously execute roots inside another environment.

#### Scenario: A Hunt event requests an encounter
- **WHEN** the Hunt root commits the encounter request fact
- **THEN** the Campaign event handler may enqueue a separate Campaign root after the Hunt chain has released its internal execution state

#### Scenario: A root fails or is cancelled
- **WHEN** the owning root does not commit
- **THEN** uncommitted facts are discarded and no cross-environment transition is requested

### Requirement: Domain state remains independent of execution infrastructure
GameCore SHALL remain free of UnityEngine, TEngine EventBus, and the installed ActionQueue package; Unity Adapters SHALL map persistent data or table-defined effects into Installers and GameActions, while Views SHALL only submit intent and present results.

#### Scenario: A table-defined persistent effect is added later
- **WHEN** content loading creates its runtime domain state
- **THEN** an Adapter may register an Installer without adding ActionQueue or EventBus dependencies to GameCore
