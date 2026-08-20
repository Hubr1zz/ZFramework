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

### Requirement: Content remains configuration-driven
Recipes and their required workshop identifiers SHALL continue to come from configured content data so future table-driven items, equipment, and workshops can replace the current ScriptableObject adapter without changing the View command contract.

#### Scenario: A new configured recipe becomes available
- **WHEN** its workshop and unlock requirements are satisfied
- **THEN** it appears under the corresponding workshop card without adding a new View type
