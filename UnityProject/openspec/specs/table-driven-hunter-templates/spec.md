---
schemaVersion: 2
category: feature
title: "读表猎人模板与稳定身份"
---

# Table-driven Hunter Templates Specification

## Purpose

让初始猎人和招募候选通过可替换数据表扩展，同时沿用现有猎人数据、营地 ActionQueue 与世界空间 3D 卡牌表现，并为存档和跨内容规则提供稳定模板身份。

## Requirements

### Requirement: Hunter templates use stable content identity
Every configured or table-driven hunter template SHALL expose a non-empty stable content ID independent from its Unity asset name and player-facing display name.

#### Scenario: A hunter is recruited from a template
- **WHEN** the Settlement recruitment Action commits a valid candidate
- **THEN** the created hunter and the committed recruitment fact retain the template's stable content ID

### Requirement: Template content enters through an injectable table source
The Settlement content catalog SHALL accept a serialized hunter table and map valid records to ordinary `HunterData` instances before gameplay begins.

#### Scenario: A valid recruitable record is assembled
- **WHEN** its identity, attributes, roles, and equipment references are valid
- **THEN** it joins the existing recruitment catalog in table order without a content-specific View or Action branch

### Requirement: Invalid records fail closed
The Adapter SHALL reject missing or colliding identities, templates without a starting or recruitment role, negative attributes, non-positive movement, oversized loadouts, weapon-limit or armor-coverage violations, and unknown or non-equipment item references.

#### Scenario: A table record references a resource as equipment
- **WHEN** the hunter table is assembled
- **THEN** that record does not enter either playable template pool and an actionable content error is reported

### Requirement: Initial loadouts become authoritative runtime state
Creating a hunter from a template SHALL copy all configured combat attributes and up to the supported equipment capacity into both runtime equipment objects and stable persisted equipment IDs.

#### Scenario: A recruit starts with a configured armor card
- **WHEN** the hunter instance is created
- **THEN** the armor appears in the existing 3D equipment slots and remains restorable from the saved stable item ID

### Requirement: Existing three-layer recruitment flow is reused
Table records SHALL remain Adapter input, the Settlement runner SHALL remain the only recruitment commit authority, and the existing world-space recruitment cards SHALL present the resulting `HunterData` without reading JSON directly.

#### Scenario: The player recruits a table candidate
- **WHEN** the 3D candidate card submits the existing recruit command
- **THEN** the normal cost, capacity, yearly limit, Reactor, annal, roster event, and persistence rules apply unchanged

### Requirement: Production recruitment content provides replacement variety
The production hunter table SHALL provide at least eight valid recruitable templates with differentiated attributes and initial loadouts, so the normal world-space recruitment board exposes more than one page of candidates without content-specific View branches.

#### Scenario: Production campaign content is assembled
- **WHEN** the serialized recruitment assets and production hunter table are merged into one campaign content plan
- **THEN** every table candidate SHALL retain a unique stable ID, registered trait references, valid equipment, and a reachable position in the existing five-card pagination flow

### Requirement: Template origin persists across sessions
The authoritative hunter state SHALL serialize its origin template ID while preserving compatibility with saves where that field is absent.

#### Scenario: A legacy hunter is loaded
- **WHEN** its saved payload has no origin template ID
- **THEN** the hunter remains playable with an empty origin identity and no fabricated display-name binding
