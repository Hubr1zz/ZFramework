---
schemaVersion: 2
category: feature
title: "读表物品与跨内容关键词"
---

# Table-driven Item Keywords Specification

## Purpose

让装备、特性、症状和事件共享稳定的规则关键词，并允许物品内容从可替换的数据表来源进入现有营地仓库、3D 卡牌和 ActionQueue 事件流程。

## Requirements

### Requirement: Item tables extend existing content contracts
The item table SHALL map stable IDs, display data, compatibility tags, free-form keywords, and basic equipment statistics into the existing `ItemData` contract without changing View or equipment Action command signatures.

#### Scenario: A valid table resource is loaded
- **WHEN** Settlement content is assembled
- **THEN** the table item is registered beside configured ScriptableObject items and can be consumed by existing resource, crafting, equipment, and presentation adapters

#### Scenario: Table identity is ambiguous
- **WHEN** two records share an ID or display item name
- **THEN** every ambiguous record is rejected instead of allowing load order to choose an authority

### Requirement: Keywords use one normalized rule language
Keyword comparison SHALL trim surrounding whitespace, compare case-insensitively through a canonical lowercase representation, and ignore empty values.

#### Scenario: Legacy and table content meet
- **WHEN** an equipped item has legacy `ItemTag.Stone` or the string keyword `stone`
- **THEN** both sources satisfy the same `HasKeyword: stone` rule

### Requirement: Event choices query authoritative equipped content
Conditional event options SHALL derive keywords from the selected hunter's traits, ailments, and equipped item ContentIds resolved through the active item registry; legacy display-name aliases MAY be accepted only at migration and compatibility boundaries. The View SHALL only display availability and submit a selection.

#### Scenario: A hunter equips a stone-tagged item
- **WHEN** a physical event card requires the `stone` keyword
- **THEN** that hunter can select the option, while hunters without a matching authoritative source see the same card disabled with a requirement reason

### Requirement: Keyword rewards remain in the existing event transaction
Effects chosen through a keyword-gated option SHALL commit inside the owning phase ActionQueue event node and SHALL publish the normal settlement transaction checkpoint after mutation.

#### Scenario: The stone vigil option resolves
- **WHEN** an eligible hunter confirms the option
- **THEN** its configured resource and growth effects commit once, the settlement refreshes through the existing transaction fact, and no View mutates inventory directly
