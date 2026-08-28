---
schemaVersion: 2
category: feature
title: "事件驱动猎人永久死亡"
---

# Event-driven Hunter Death Specification

## Purpose

让营地与狩猎事件能够通过所属阶段的 ActionQueue 触发猎人永久死亡，同时复用唯一的死亡事务、3D 桌面反馈、年鉴和战役终局流程，不让事件系统建立第二套名册修改逻辑。

## Requirements

### Requirement: Event death targets the selected hunter
A choice effect that permanently kills a hunter SHALL require a valid selected hunter and a non-empty stable cause ID. Immediate narrative effects SHALL NOT contain hunter death because they do not guarantee an actor selection.

#### Scenario: A death option is prepared without an actor
- **WHEN** either the shared transaction API or the legacy choice API receives that selection
- **THEN** preparation fails without applying any effect or completing the event

#### Scenario: Table content contains an invalid death cause
- **WHEN** the cause ID is blank, oversized, or contains control characters
- **THEN** the entire event record is rejected before it enters a playable pool

#### Scenario: The death command is unavailable
- **WHEN** a lethal option also contains rewards but the event system has no hunter-death command
- **THEN** choice preparation fails before any reward or other effect is committed

### Requirement: The owning phase runner commits event death
Settlement and Hunt SHALL keep event selection, configured physical interaction, result confirmation, and effect commit inside their current event node and phase execution environment.

#### Scenario: The player accepts a lethal event option
- **WHEN** the event node commits its prepared choice
- **THEN** the View supplies only the selected option and actor
- **AND** the event effect invokes the injected hunter-death command inside that same ActionQueue node

### Requirement: Every permanent death reuses one aftermath transaction
Event death, hunt survival death, and compatibility callers SHALL route through the existing Hunter Management death transaction. That transaction SHALL mark the authoritative hunter dead, return equipment, write one annal entry, grant configured survivor inspiration, publish the death fact with its stable cause, and publish the roster change.

#### Scenario: An event kills an equipped hunter
- **WHEN** the death command accepts a hunter belonging to the current settlement
- **THEN** the hunter's equipment returns to settlement storage and all aftermath facts commit once

### Requirement: Death submission is idempotent and settlement-scoped
Event choice preparation and the hunter-death command SHALL reject null or foreign hunter instances and SHALL NOT duplicate aftermath when the same hunter is submitted more than once.

#### Scenario: A foreign hunter is submitted to a rewarding death option
- **WHEN** the supplied hunter instance is not the authoritative instance in the current settlement
- **THEN** the choice is rejected before its reward or death effect is committed

#### Scenario: The same death is submitted twice
- **WHEN** a retry or duplicate caller invokes the command again
- **THEN** the original annal, cause, equipment return, inspiration, and death fact remain singular

### Requirement: Permanent loss remains visible on the tabletop
The committed death fact SHALL include a player-facing cause and SHALL be projected through the existing world-space settlement notice flow. If no living hunters remain, the existing tabletop campaign-defeat flow SHALL remain authoritative.

#### Scenario: A hunter fulfills the dark bargain
- **WHEN** the event death commits
- **THEN** the 3D loss notice names the hunter and explains the bargain
- **AND** a last-hunter loss skips the ordinary event-result prompt so the normal campaign-defeat presentation owns the tabletop without competing modal cards
- **AND** no chained event or encounter starts after campaign defeat
