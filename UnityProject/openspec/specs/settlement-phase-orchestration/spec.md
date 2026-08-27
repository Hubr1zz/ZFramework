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
The project SHALL enter Settlement through GameManager and activate the Settlement world and UI roots. SettlementPhaseManager SHALL own Settlement runtime generations and their ActionSession lifecycle; SettlementPhaseCoordinator SHALL own the current table composition bridge, while CampaignFlowCoordinator SHALL own the pending-return persistence transaction. GameManager SHALL provide scene references and retain only the compatibility facade.

#### Scenario: Entering Settlement
- **WHEN** the global phase changes to Settlement
- **THEN** GameManager activates Settlement roots, asks SettlementPhaseManager to activate the current generation, and refreshes the configured Settlement presentation

### Requirement: Settlement entry owns the save boundary
The project SHALL submit a pending hunt return through the active Settlement ActionQueue before ordinary annual-event projection, then persist the committed Settlement state.

#### Scenario: Completing Settlement entry
- **WHEN** SettlementManager has received any pending hunt record
- **THEN** the Settlement runner commits HuntHistory, the next configured season and any real year-boundary Timeline entries as one ordered root, clears the pending handoff, and GameManager requests a save

### Requirement: Settlement generations are campaign scoped

SettlementPhaseManager SHALL prepare new or restored runtime candidates without publishing them, atomically swap only the expected current generation, and release retired candidates. Reset or Campaign shutdown SHALL dispose every Settlement ActionSession and restore projection before the phase manager is released.

#### Scenario: A restored Settlement candidate is rejected

- **WHEN** 候选内容、日历或持久效果投影无法通过发布前验证
- **THEN** 当前 Settlement generation SHALL 保持权威
- **AND** 候选 session 与恢复投影 SHALL NOT 留下活动绑定

#### Scenario: Return settlement cannot complete
- **WHEN** the Settlement runner rejects, cancels, or throws while applying the pending record
- **THEN** the pending handoff remains persisted and every Hunt departure entry remains gated for a bounded retry

### Requirement: Loaded event state is projected before departure
Loading a campaign in Settlement SHALL first recover a persisted pending hunt return through the active Settlement runner, then rebuild pending event execution once from persisted Timeline references. A load without a pending return SHALL NOT invoke year advancement or event generation.

#### Scenario: Continue restores an interrupted return handoff
- **WHEN** loaded Settlement data contains a pending stable hunt return
- **THEN** CampaignFlowCoordinator applies it idempotently without directly queueing its returned events, then asks the Settlement phase coordinator to project the resulting incomplete Timeline exactly once
- **AND** departure remains gated until both operations succeed

#### Scenario: Continue restores an unresolved event chain
- **WHEN** loaded Settlement data contains one or more unresolved event entries
- **THEN** CampaignFlowCoordinator resolves their configured content in persisted order and submits one chain through the active Settlement phase coordinator
- **AND** every departure entry remains rejected until the chain completes successfully

#### Scenario: Event restoration fails
- **WHEN** content resolution or Settlement ActionQueue execution cannot restore the pending chain
- **THEN** the failure remains observable and the campaign SHALL NOT enter Hunt through another departure entry

### Requirement: Settlement table assembly is scene-authorable and reentrant
SettlementPhaseCoordinator SHALL initialize the same SettlementTable3D command ports and data bindings whether the table is assigned by the scene or created as a runtime fallback. GameManager SHALL provide the scene reference, and repeated settlement entry and load SHALL reuse the table without duplicating its generated hierarchy or EventBus subscriptions.

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
