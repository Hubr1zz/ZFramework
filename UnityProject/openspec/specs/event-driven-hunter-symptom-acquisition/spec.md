---
schemaVersion: 2
category: feature
title: "事件驱动猎人症状获得"
---

# Event-driven Hunter Symptom Acquisition Specification

## Purpose

让营地与狩猎事件通过所属阶段的 ActionQueue，把稳定症状内容 ID 写入猎人权威状态，并由现有世界空间猎人卡与症状板呈现；旧存档中的显示名或别名只作为迁移输入。

## Requirements

### Requirement: New event content references symptoms by stable ID
`AddAilment` event effects SHALL store an exact symptom content ID. Event table validation SHALL reject a display name, legacy alias, blank reference, or unknown ID before the event enters a playable pool.

#### Scenario: A table row uses a symptom display name
- **WHEN** an `AddAilment` row targets the configured display name instead of its stable ID
- **THEN** table validation rejects the event record

### Requirement: Legacy symptom references migrate into authoritative state
Runtime restoration SHALL resolve an existing ailment token by stable ID, current display name, or configured legacy alias, then register the matching stable symptom state. Unknown legacy tokens SHALL remain intact so a future catalog can migrate them without data loss.

#### Scenario: A saved display name is restored
- **WHEN** a hunter loads with a configured symptom display name in the legacy ailment list
- **THEN** the matching stable symptom state is registered once
- **AND** the compatibility projection retains only the current display name for that symptom

### Requirement: Event acquisition is settlement-scoped and idempotent
An `AddAilment` effect SHALL require a living authoritative hunter belonging to the current settlement and a resolvable symptom. The first acquisition SHALL apply the symptom modifiers and compatibility projection once; a repeated acquisition SHALL succeed without duplicating state or modifiers.

#### Scenario: One option contains the same symptom twice
- **WHEN** both effects resolve against the same living settlement hunter
- **THEN** both effects are reported as applied
- **AND** only the first reports a state change and applies the symptom modifier

#### Scenario: An event targets an unknown symptom
- **WHEN** the configured stable ID is absent from the runtime catalog
- **THEN** the effect is reported as rejected with no hunter-state or fact mutation

### Requirement: The owning ActionQueue publishes committed acquisition facts
Settlement and Hunt event nodes SHALL stage one acquisition fact for each successful state change and publish it through their event outbox at the resolution checkpoint. Compatibility calls outside an ActionQueue SHALL NOT directly publish that fact.

#### Scenario: A symptom is acquired during event resolution
- **WHEN** the event node commits its resolution checkpoint
- **THEN** the acquisition fact identifies the source event, effect index, hunter, stable symptom ID and display name
- **AND** it is published before the event-resolution transaction fact

### Requirement: Authoritative symptom state drives the 3D tabletop
The existing world-space hunter card and symptom panel SHALL derive availability and cards from stable symptom state. Event acquisition SHALL NOT create a separate screen-space symptom UI or a second symptom-state owner.

#### Scenario: The event result returns to settlement
- **WHEN** the affected hunter card refreshes after the committed event
- **THEN** its symptom entry and card represent the newly acquired authoritative symptom state
