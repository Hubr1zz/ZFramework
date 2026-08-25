---
schemaVersion: 2
category: feature
title: "读表狩猎事件内容"
---

# Table-driven Hunt Events Specification

## Purpose

让跨路线共用的狩猎事件通过稳定 ID 读表扩展，同时保留路线 `ScriptableObject` 作为地形专属默认内容，并继续由 Hunt ActionQueue 与 3D 桌面随机交互完成结算。

## Requirements

### Requirement: Shared hunt events load from the merged event catalog
The event table runtime SHALL load `Hunt` records from a dedicated table source and SHALL expose them as the same runtime `EventData` model consumed by existing hunt sessions.

#### Scenario: A hunt session starts for any configured destination
- **WHEN** destination content is applied to `HuntManager`
- **THEN** route-specific events and shared table-driven hunt events are both available
- **AND** no View-specific content branch is required

### Requirement: Stable IDs define deterministic overrides
A table-driven Hunt event SHALL replace route content with the same explicit stable ContentId. Null entries, non-Hunt categories, blank IDs, and every route entry sharing a duplicate ID SHALL NOT enter the resulting Hunt pool.

#### Scenario: Route content and table content share an ID
- **WHEN** the Hunt event pool is assembled
- **THEN** exactly one event with that ID remains
- **AND** the validated table event is authoritative

### Requirement: Hunt event execution remains phase-owned
Loaded Hunt events SHALL be selected by `HuntEventSystem` and resolved by the active Hunt runner. Effects, event chains, encounter requests, and committed facts SHALL continue through the existing Hunt ActionQueue and shared event ports.

#### Scenario: A tile interaction selects a table event
- **WHEN** a player resolves its option
- **THEN** the Hunt runner waits for the event interaction and commits its effects in sequence
- **AND** the View does not mutate Hunt or Settlement state directly

### Requirement: Tabletop presentation is content-configurable
Hunt options SHALL reuse the existing physical-dice and card-interaction request contract. New art, audio, and cinematic presentation SHALL remain replaceable behind that presenter interface.

#### Scenario: A route-card check is configured
- **WHEN** the option requires `DrawCards`
- **THEN** the shared tabletop presenter receives the configured deck ID and instruction
- **AND** the validated result is returned to the same event transaction used by dice checks
