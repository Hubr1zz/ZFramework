---
schemaVersion: 2
category: feature
title: "狩猎桌面资源采集"
---

# Hunt Tabletop Harvest Specification

## Purpose

让狩猎资源点采集保持桌游实体感，同时维持 Hunt ActionQueue 对随机牌序、逐卡揭示和最终资源写入的唯一控制权。

## Requirements

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

### Requirement: Compatibility fallback remains available
The legacy screen-space popup MAY remain as a fallback when no live world presentation anchor exists, but it SHALL NOT be selected for a normal 3D hunt map resource marker.

#### Scenario: A non-world test host requests harvest presentation
- **WHEN** the hunt view cannot resolve a live 3D marker for the selected resource point
- **THEN** the compatibility popup may present the same ActionQueue-backed harvest transaction
