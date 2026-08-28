---
schemaVersion: 2
category: feature
title: "狩猎桌面资源采集"
---

# Hunt Tabletop Harvest Specification

## Purpose

让狩猎资源点采集保持桌游实体感，同时维持 Hunt ActionQueue 对随机牌序、逐卡揭示和最终资源写入的唯一控制权。

## Requirements

### Requirement: 采集要求小队到达资源地块

系统 MUST 仅允许小队采集当前所在、且已经翻开的地块上的资源点。远处已翻开的资源棋子 MAY 保持可见，但在小队移动到对应地块之前，桌面选择入口与 Hunt ActionQueue 的准备动作 MUST 拒绝该资源点。

采集事务创建后，Hunt ActionQueue MUST 在事务完成或玩家离开采集前拒绝翻牌与移动命令，避免小队位置与采集目标在流程中途分离。

#### Scenario: 玩家尝试远程采集已翻开的资源点

- **GIVEN** 相邻地块已经翻开并展示资源棋子
- **AND** 小队仍停留在其他地块
- **WHEN** 玩家选择该资源棋子或请求准备采集
- **THEN** 系统不打开采集桌面且不创建采集事务
- **WHEN** 小队移动到该资源地块后再次请求准备采集
- **THEN** 系统允许 ActionQueue 创建采集事务

### Requirement: Harvest uses world-space physical cards
When a resource marker has a valid world presentation anchor, the hunt view SHALL present its material pool as face-down 3D cards beside that resource point instead of opening the screen-space harvest popup.

#### Scenario: A player selects a resource marker
- **WHEN** the selected point belongs to a revealed tile with a live 3D marker
- **THEN** the material cards appear near that marker and the next revealable card is visibly interactable

### Requirement: Cards reveal committed action results
The 3D harvest view SHALL submit prepare and advance commands through `HuntManager` and SHALL only turn a card face-up from the `PlayableHarvestStepResult` returned by the Hunt ActionQueue.

#### Scenario: The player clicks the next material card
- **WHEN** the Hunt ActionQueue commits one reveal step
- **THEN** the matching physical card flips to its hit or miss face and the following card becomes interactable

### Requirement: Effective harvest terms remain player-visible
The Hunt runner SHALL finalize draw count and hit chance through the `BeginHarvestAction` Reactor window before creating the immutable card plan. The world-space harvest view SHALL read and display the resulting chance from that transaction rather than recomputing rules.

#### Scenario: A campaign invention modifies harvest chance
- **WHEN** a mastered invention Reactor changes the hit chance before execution
- **THEN** every card result uses the modified chance and the physical harvest panel displays that effective percentage

### Requirement: Harvest interaction owns map input
The tabletop harvest view SHALL block tile, resource-marker, and retreat commands while its physical cards remain open, and SHALL release the guard only when the player dismisses the presentation or the hunt session changes.

#### Scenario: A partially revealed pool is open
- **WHEN** at least one card has been revealed but the transaction is not committed
- **THEN** the player must resolve the remaining cards and cannot abandon the authoritative transaction through the view

#### Scenario: Committed results remain visible
- **WHEN** the final card result has been committed but the result cards remain on the tabletop
- **THEN** map movement, another resource point, and retreat input remain blocked until the player selects the physical close card

### Requirement: Configuration remains bounded
The view SHALL derive its card count from resource configuration while applying `HarvestDrawPlan.MaximumCardCount`, including a valid confirmation path for an empty material pool.

#### Scenario: Imported content contains an unsafe draw count
- **WHEN** the configured count exceeds the domain limit
- **THEN** the view creates no more than the domain maximum and remains operable

### Requirement: Resource pools bind table content by stable identity
Resource material entries MAY reference a resource item by stable content ID. The hunt content bundle SHALL resolve that ID through the active campaign registry and freeze the canonical `ItemData` before gameplay. Unknown IDs, non-resource items, invalid copy counts, or unresolved pools SHALL reject the candidate bundle; direct asset references MAY remain as a compatibility fallback when no stable ID is configured.

#### Scenario: A production tile references a table-defined material
- **WHEN** the active item registry contains the configured stable resource ID
- **THEN** the resource point uses that registry item in its immutable harvest plan

#### Scenario: A resource ID is invalid
- **WHEN** a material entry names an unknown or non-resource item
- **THEN** content assembly fails before the route becomes playable

### Requirement: Compatibility fallback remains available
The legacy screen-space popup MAY remain as a fallback when no live world presentation anchor exists, but it SHALL NOT be selected for a normal 3D hunt map resource marker.

#### Scenario: A non-world test host requests harvest presentation
- **WHEN** the hunt view cannot resolve a live 3D marker for the selected resource point
- **THEN** the compatibility popup may present the same ActionQueue-backed harvest transaction
