---
schemaVersion: 2
category: feature
title: 营地桌面工坊建设
---

# Tabletop Settlement Workshop Construction Specification

## Purpose

定义从发明知识到实体工坊、再到可用制造配方的 3D 桌面流程；建设 View 只提交命令，蓝图校验、资源消费、建成状态、事件和存档统一归属 Settlement ActionQueue。

## Requirements

### Requirement: Unbuilt workshops appear as 3D blueprint cards
The workshop zone SHALL represent each configured unbuilt workshop as a three-dimensional blueprint card and SHALL display its current construction availability.

#### Scenario: Player selects a blueprint
- **WHEN** the player activates an unbuilt workshop card
- **THEN** a world-space board shows its description, prerequisite invention, material costs, and current rule result

### Requirement: Construction is authoritative in the Settlement runner
The construction View SHALL NOT mutate settlement data directly; the active Settlement ActionQueue SHALL revalidate blueprint registration, prior construction, invention prerequisite, and aggregate resource costs before committing.

#### Scenario: Two requests target one blueprint
- **WHEN** both requests enter the same Settlement runner
- **THEN** exactly one may spend resources and mark the workshop built

#### Scenario: A reactor prevents construction
- **WHEN** a BeforeExecution reactor prevents the construction action
- **THEN** resources, workshop flags, published facts, and available recipes remain unchanged

### Requirement: Successful construction opens configured production
A successful construction SHALL publish affected resource changes, one workshop-built fact, and one WorkshopConstruction transaction commit after state mutation.

#### Scenario: Construction commits
- **WHEN** the workshop flag is persisted
- **THEN** the blueprint is replaced by a built workshop card and recipes requiring that workshop become available on the 3D table

### Requirement: The normal playable flow has no legacy construction bypass
The playable bootstrap SHALL NOT create the legacy screen-space construction window, and the legacy development page SHALL NOT expose direct invention or construction mutation controls.

#### Scenario: Player develops the settlement
- **WHEN** the normal playable bootstrap enters Settlement
- **THEN** invention, construction, crafting, and equipment storage are operated from the world-space table

### Requirement: Workshop content is configuration-driven
Workshop identities, labels, descriptions, prerequisites, and costs SHALL come from the configured bootstrap catalog.

#### Scenario: A workshop catalog is replaced
- **WHEN** a valid catalog is assigned to the playable bootstrap settings
- **THEN** the Settlement runner and 3D table use that catalog without changing View command types
