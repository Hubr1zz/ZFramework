---
schemaVersion: 2
category: feature
title: "读表猎人特性与稳定身份"
---

# Table-driven Hunter Traits Specification

## Purpose

让猎人特性以稳定 ID 参与模板、事件、成长、症状、武器熟练和存档，同时由数据表提供玩家可见名称与关键词。

## Requirements

### Requirement: Gameplay references use canonical trait IDs
Every production hunter template, event condition, event effect, growth milestone, symptom projection, and weapon mastery milestone SHALL reference a registered canonical trait ID instead of a localized display name.

#### Scenario: A localized trait name changes
- **WHEN** the trait table changes only its display name
- **THEN** existing gameplay references and saved hunter state continue to resolve through the unchanged trait ID

### Requirement: Trait content is assembled before gameplay
The Settlement content plan SHALL load one versioned trait table, reject missing or colliding identities and aliases, and fail installation when production content references an unregistered trait ID.

#### Scenario: An event grants an unknown trait
- **WHEN** campaign content preflight validates the event graph
- **THEN** the candidate is rejected before a playable runtime is published

### Requirement: Legacy saves migrate inside the content projection transaction
Settlement restore SHALL idempotently convert registered legacy display names to canonical trait IDs, deduplicate equivalent references, preserve unknown external identifiers, and reject a save schema newer than the runtime.

#### Scenario: A save contains both a legacy name and its canonical ID
- **WHEN** the Settlement candidate is projected
- **THEN** the candidate contains one canonical trait ID and the authoritative runtime is unchanged until projection succeeds

### Requirement: Presentation resolves player-facing trait names
World-space cards and hunter detail presentation SHALL resolve stored trait IDs through the active trait catalog and SHALL NOT display canonical IDs when registered display names exist.

#### Scenario: A recruitment card contains a stable trait ID
- **WHEN** the card is rendered
- **THEN** the player sees the configured localized trait name

#### Scenario: A hunter dossier contains several traits
- **WHEN** the world-space equipment dossier is rendered or refreshed
- **THEN** it shows a bounded localized trait summary with a remaining count instead of overflowing the tabletop panel

### Requirement: Trait keywords remain content-driven
Keyword aggregation SHALL include the trait's canonical ID, display name, legacy aliases, and configured keywords without routing presentation events through ActionQueue.

#### Scenario: A trait grants a stone keyword
- **WHEN** event option availability collects the hunter's gameplay keywords
- **THEN** the option can match `stone` while UI refresh remains a direct read-only projection
