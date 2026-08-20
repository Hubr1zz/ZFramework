---
schemaVersion: 2
category: feature
title: "读表发明与信仰分支"
---

# Table-driven Invention Content Specification

## Purpose

让大量发明通过可替换数据源接入既有稳定身份目录、3D 发明卡和各阶段 ActionQueue，并以“信仰 → 仪式”和“纸和笔 → 植物知识”提供可玩的长期成长分支。

## Requirements

### Requirement: Invention tables use stable content references
Each invention record SHALL define a stable ID, display name, category, prerequisite IDs, exclusion IDs, item costs, structured unlock effects, and effect presentation without using display text as a cross-content identity or rule selector.

#### Scenario: Settlement assembles a valid invention table
- **WHEN** referenced item content and configured ScriptableObject inventions are available
- **THEN** valid records map to ordinary `InventionData` nodes and join the existing invention catalog in table order

### Requirement: Invalid invention graphs fail before gameplay
The table adapter SHALL reject missing identities, cross-namespace identity collisions, invalid categories, costs, effect kinds or effect targets, zero-value effects, unknown references, self references, prerequisite cycles, dependencies on rejected records, and aggregate costs outside the supported integer range.

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

### Requirement: Unlock effects are structured Action children
Effect descriptions SHALL remain player-facing text only. After the unlock commit, each eligible hunter effect SHALL execute as a child `GameAction` in the current Settlement environment with its own source, target, and Reactor window.

#### Scenario: A rule changes one hunter's ritual benefit
- **WHEN** a Settlement Reactor changes the ritual effect amount for one hunter
- **THEN** that hunter receives the overridden amount while other eligible hunters retain their own independently resolved effects

#### Scenario: A rule prevents one hunter effect
- **WHEN** a Reactor prevents one eligible hunter's effect child after the invention commit
- **THEN** the invention remains mastered, its cost remains consumed, and other eligible effect children continue in stable roster order

### Requirement: Baseline faith branch is playable
The baseline table SHALL contain `faith` as a root node and `ritual` as its direct child, using stable item costs already present in the Settlement catalog.

#### Scenario: Faith is not mastered
- **WHEN** the player inspects ritual before mastering faith
- **THEN** the existing invention availability rule reports the unmet prerequisite and prevents resource consumption

### Requirement: Action effects inject rules into matching phase runners
The table SHALL allow an invention to declare stable, uniquely identified Action effects. A campaign-level installer SHALL project those effects into matching phase environments as Reactors, and each Reactor SHALL re-read current mastery state when an Action executes so loading or unlocking does not require rebuilding the phase implementation.

#### Scenario: Plant knowledge improves herb harvesting
- **WHEN** `plant-knowledge` is mastered and a hunter prepares harvesting an item carrying the `Herb` keyword
- **THEN** the Hunt Reactor increases the configured harvest hit chance by 10 percentage points before the immutable card plan is generated
- **AND** the 3D harvest panel displays the effective chance

#### Scenario: Plant knowledge is absent or the resource is not an herb
- **WHEN** the invention is not mastered or the harvested item does not carry the target keyword
- **THEN** the baseline harvest chance remains unchanged

### Requirement: Action-effect content fails closed
Action-effect records SHALL define a non-empty globally unique effect ID, a supported kind, a normalized target keyword, and a finite non-zero value inside the supported range.

#### Scenario: Two inventions reuse an Action-effect ID
- **WHEN** two table records declare the same stable Action-effect ID
- **THEN** both conflicting inventions SHALL be rejected before entering the playable catalog
