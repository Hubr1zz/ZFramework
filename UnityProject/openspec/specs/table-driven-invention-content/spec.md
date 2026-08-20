---
schemaVersion: 2
category: feature
title: "读表发明与信仰分支"
---

# Table-driven Invention Content Specification

## Purpose

让大量发明通过可替换数据源接入既有稳定身份目录、3D 发明卡和 Settlement ActionQueue，并以“信仰 → 仪式”提供首个可玩的长期成长分支。

## Requirements

### Requirement: Invention tables use stable content references
Each invention record SHALL define a stable ID, display name, category, prerequisite IDs, exclusion IDs, item costs, and effect presentation without using display text as a cross-content identity.

#### Scenario: Settlement assembles a valid invention table
- **WHEN** referenced item content and configured ScriptableObject inventions are available
- **THEN** valid records map to ordinary `InventionData` nodes and join the existing invention catalog in table order

### Requirement: Invalid invention graphs fail before gameplay
The table adapter SHALL reject missing identities, cross-namespace identity collisions, invalid categories or costs, unknown references, self references, prerequisite cycles, dependencies on rejected records, and aggregate costs outside the supported integer range.

#### Scenario: Two table nodes form a prerequisite cycle
- **WHEN** each node requires the other
- **THEN** neither node nor any node depending on that invalid cycle enters the playable catalog

#### Scenario: One cost item appears repeatedly
- **WHEN** a record repeats one stable item ID with positive counts
- **THEN** the adapter combines those counts without overflow and preserves first-occurrence order

### Requirement: Content source remains injectable
The invention table SHALL enter the Settlement content catalog through a serialized `TextAsset` source and SHALL NOT add another synchronous `Resources.Load` call.

#### Scenario: The table source is absent
- **WHEN** no invention table is assigned
- **THEN** configured ScriptableObject inventions continue to assemble without a parallel fallback loader

### Requirement: Table inventions reuse the existing three-layer flow
Table records SHALL remain Adapter input; GameCore rules SHALL decide availability, the Settlement runner SHALL commit unlocking, and existing world-space invention cards SHALL present the resulting `InventionData` without content-specific View types.

#### Scenario: Player unlocks ritual
- **WHEN** faith is mastered, the configured soft-organ cost is available, and the player confirms the ritual card
- **THEN** the Settlement ActionQueue consumes the cost, records stable mastery and annals facts, and increases every available hunter's willpower maximum by one

### Requirement: Baseline faith branch is playable
The baseline table SHALL contain `faith` as a root node and `ritual` as its direct child, using stable item costs already present in the Settlement catalog.

#### Scenario: Faith is not mastered
- **WHEN** the player inspects ritual before mastering faith
- **THEN** the existing invention availability rule reports the unmet prerequisite and prevents resource consumption
