---
schemaVersion: 2
category: feature
title: 营地桌面工坊制作
---

# Settlement Workshop Crafting Specification

## Purpose

定义玩家通过营地 3D 工坊卡查看配方并制作物品的完整流程，确保视觉卡牌只提交命令，资源消费、产出、事件与存档边界统一归属 Settlement ActionQueue。

## Requirements

### Requirement: Available workshops are represented by 3D cards
The settlement table SHALL group currently unlocked recipes by required workshop identity and SHALL present each non-empty group as a three-dimensional workshop card.

#### Scenario: A built workshop has unlocked recipes
- **WHEN** the settlement table refreshes
- **THEN** the workshop zone contains a workshop card that opens three-dimensional recipe cards for that group

### Requirement: Recipe cards submit commands without owning state
A recipe card SHALL display its configured inputs and output, and SHALL translate player activation into a crafting command without consuming visual cards or directly mutating settlement state.

#### Scenario: Player activates a recipe card
- **WHEN** no previous request from that card is pending
- **THEN** the card submits exactly one request and displays pending, success, or failure feedback

### Requirement: Crafting is atomic inside the settlement runner
Authoritative crafting SHALL execute as a root action in the active Settlement ActionQueue environment and SHALL validate recipe registration, workshop availability, unlock conditions, and resource amounts at execution time.

#### Scenario: Two requests compete for one material batch
- **WHEN** both crafting requests enter the same settlement runner
- **THEN** only the first valid request consumes resources and the second fails without creating duplicate output

#### Scenario: A reactor prevents crafting
- **WHEN** a BeforeExecution reactor prevents the crafting action
- **THEN** resources and equipment storage remain unchanged

### Requirement: Successful crafting publishes committed facts
A successful crafting action SHALL publish resource changes, a crafted output fact, and one Crafting transaction commit after authoritative state has changed.

#### Scenario: Crafting equipment succeeds
- **WHEN** the recipe consumes its inputs and produces non-resource equipment
- **THEN** equipment storage increases, affected resource cards refresh, the visible equipment panel refreshes, and the settlement save boundary observes the Crafting commit

#### Scenario: Crafting consumes the final copy of a resource
- **WHEN** a committed resource change reduces that material to zero
- **THEN** its physical resource card is removed and the remaining positive resources are reflowed without showing zero-count cards

#### Scenario: An exhausted resource is obtained again
- **WHEN** a later committed resource change raises that material above zero
- **THEN** the physical resource card appears again with its authoritative count

### Requirement: Content remains configuration-driven
Recipes and their required workshop identifiers SHALL continue to come from configured content data so future table-driven items, equipment, and workshops can replace the current ScriptableObject adapter without changing the View command contract.

#### Scenario: A new configured recipe becomes available
- **WHEN** its workshop and unlock requirements are satisfied
- **THEN** it appears under the corresponding workshop card without adding a new View type

### Requirement: Starter crafting closes the next-hunt preparation loop
The configured starter content SHALL offer multiple equipment outcomes that consume materials obtainable during Hunt and produce different next-Hunt noise profiles. These recipes SHALL resolve mixed ScriptableObject and table item identities through stable ContentIds and SHALL use the existing Tools invention gate.

#### Scenario: The player returns with fungal materials
- **GIVEN** the Tools invention is mastered
- **AND** the armor workshop is built
- **AND** the settlement owns mushroom flesh, viscous sap, and a soft organ
- **WHEN** the player crafts the configured fungal wrap from its physical recipe card
- **THEN** all Hunt materials are consumed atomically
- **AND** a quiet armor instance enters equipment storage for the next departure

#### Scenario: The player chooses a louder weapon
- **GIVEN** the Tools invention is mastered
- **AND** the settlement owns black salt and broken stone
- **WHEN** the player crafts the configured salt crystal edge
- **THEN** the produced weapon exposes its positive Hunt noise modifier through the existing item contract

### Requirement: Starter crafting sustains the recovery loop
The configured starter content SHALL provide a medical production branch that converts a Hunt-obtainable resource into the existing hunter recovery resource. The recipe SHALL require the Tools invention and a built `medical_workshop`, and SHALL reuse the ordinary 3D recipe card and Settlement ActionQueue transaction.

#### Scenario: The player cultivates recovery material
- **GIVEN** the Tools invention is mastered and `medical_workshop` is built
- **AND** the settlement owns one `soft_organ`
- **WHEN** the player crafts `培制药用菌肉`
- **THEN** the soft organ is consumed once and one `mushroom_flesh` is added
- **AND** the resulting resource can be consumed by the existing world-space hunter recovery flow

#### Scenario: The medical workshop is not built
- **GIVEN** the settlement owns the recipe input and has mastered Tools
- **WHEN** the player submits the medical recipe before constructing `medical_workshop`
- **THEN** the Settlement ActionQueue rejects the command without consuming the soft organ or producing mushroom flesh
