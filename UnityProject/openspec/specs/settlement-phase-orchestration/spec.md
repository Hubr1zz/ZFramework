---
schemaVersion: 2
category: architecture
title: 营地阶段编排
---

# Settlement Phase Orchestration Specification

## Purpose

定义营地阶段在全局循环中的进入、场景与界面根节点激活、待结算记录接收及持久化边界；营地内部的经济与角色成长规则不属于本架构 Spec。

## Requirements

### Requirement: Settlement phase enters through GameManager
The project SHALL enter Settlement through GameManager and activate the Settlement world and UI roots.

#### Scenario: Entering Settlement
- **WHEN** the global phase changes to Settlement
- **THEN** GameManager activates Settlement roots, invokes SettlementManager entry, and refreshes the configured Settlement presentation

### Requirement: Settlement entry owns the save boundary
The project SHALL persist settlement state after the Settlement entry lifecycle has completed.

#### Scenario: Completing Settlement entry
- **WHEN** SettlementManager has received any pending hunt record
- **THEN** GameManager saves the resulting settlement state through the persistence adapter

### Requirement: Loaded event state is projected before departure
Loading a campaign in Settlement SHALL rebuild pending event execution from persisted Timeline references through the active Settlement runner. The load path SHALL remain distinct from normal phase entry and SHALL NOT invoke year advancement or event generation.

#### Scenario: Continue restores an unresolved event chain
- **WHEN** loaded Settlement data contains one or more unresolved event entries
- **THEN** GameManager resolves their configured content in persisted order and submits one chain to the active Settlement ActionQueue
- **AND** every departure entry remains rejected until the chain completes successfully

#### Scenario: Event restoration fails
- **WHEN** content resolution or Settlement ActionQueue execution cannot restore the pending chain
- **THEN** the failure remains observable and the campaign SHALL NOT enter Hunt through another departure entry

### Requirement: Settlement table assembly is scene-authorable and reentrant
GameManager SHALL initialize the same SettlementTable3D command ports and data bindings whether the table is assigned by the scene or created as a runtime fallback. Repeated settlement entry and load SHALL reuse the table without duplicating its generated hierarchy or EventBus subscriptions.

#### Scenario: A scene-authored settlement table enters play
- **WHEN** GameManager already has a serialized SettlementTable3D reference
- **THEN** it binds the active Settlement action session command ports and initializes every table zone
- **AND** it does not create a second settlement table

#### Scenario: A campaign is loaded while the settlement table exists
- **WHEN** the active SettlementManager instance is replaced by loaded campaign state
- **THEN** the existing table unsubscribes from its previous event bindings and rebinds to the loaded manager
- **AND** its runtime or scene-authored hierarchy is retained and refreshed idempotently
- **AND** context panels close and squad slots release references to hunter cards from the previous campaign state

#### Scenario: A scene table has only some zones assigned
- **WHEN** any but not all of the Hunter, Resource, Workshop, and Invention zones are serialized
- **THEN** initialization fails with an actionable scene-assembly error instead of entering play with latent null references
