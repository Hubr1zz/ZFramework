---
schemaVersion: 2
category: feature
title: "读表配方与事件奖励制造链"
---

# Table-driven Crafting Content Specification

## Purpose

让配方内容通过可替换数据源接入既有营地工坊、3D 配方卡和 Settlement ActionQueue，并把事件奖励继续转化为可装备物品，形成无需新增 View 类型的内容闭环。

## Requirements

### Requirement: Recipe tables bind content by stable asset identity
Recipe records SHALL identify themselves, ingredients, outputs, and optional invention prerequisites by stable content IDs; display names SHALL remain presentation data rather than table references.

#### Scenario: A valid recipe table is assembled
- **WHEN** Settlement content loads after item and invention catalogs are available
- **THEN** every valid record maps to the existing `CraftRecipe` contract and appears beside configured ScriptableObject recipes

### Requirement: Invalid recipe data fails before gameplay
The table adapter SHALL reject ambiguous recipe IDs or names, unknown item or invention references, non-positive counts, empty ingredient sets, and aggregate ingredient counts that exceed the supported integer range.

#### Scenario: Multiple records share one recipe ID
- **WHEN** the table contains an ambiguous identity
- **THEN** every record with that identity is rejected instead of allowing source order to choose an authority

#### Scenario: Repeated ingredients are valid
- **WHEN** one recipe lists the same stable item ID more than once
- **THEN** the adapter combines the counts without overflow and preserves the first-occurrence display order

### Requirement: Table recipes reuse the existing three-layer flow
The adapter SHALL provide ordinary `CraftRecipe` values to `WorkshopSystem`; world-space workshop and recipe cards SHALL continue to submit the existing command without reading table records or mutating Settlement state.

#### Scenario: A table recipe becomes available
- **WHEN** its workshop and invention requirements are satisfied
- **THEN** it appears under the existing 3D workshop card without adding a recipe-specific View

### Requirement: Crafting remains atomic in the Settlement environment
Table-defined recipes SHALL be registered and revalidated by the active Settlement ActionQueue exactly like configured ScriptableObject recipes.

#### Scenario: The player crafts a salt ward
- **WHEN** the settlement owns one black salt and confirms `刻制盐纹护符`
- **THEN** one black salt is consumed, one salt ward enters equipment storage, and the normal Crafting transaction facts are published once

### Requirement: Event rewards can feed later settlement choices
The baseline content SHALL include a table-defined equipment recipe whose ingredient is obtainable from the existing keyword-gated event flow.

#### Scenario: Black salt was earned during stone vigil
- **WHEN** the player returns to the shared workshop and crafts the configured recipe
- **THEN** the reward becomes an equippable armor card carrying the configured ritual and ward keywords
