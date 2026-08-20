---
schemaVersion: 2
category: feature
title: "3D 狩猎远征整备"
---

# Tabletop Hunt Departure Specification

## Purpose

让玩家在营地桌面以实体猎人卡编成小队并选择狩猎地区，同时由 Settlement 与 Campaign ActionQueue 分别掌握名册提交和阶段切换的权威状态。

## Requirements

### Requirement: Departure preparation is a world-space card flow
The normal settlement departure flow SHALL use a 3D launcher card, draggable hunter cards, a four-slot squad grid, and physical destination cards instead of a screen-space confirmation window.

#### Scenario: Player prepares a hunt
- **WHEN** the settlement runner is idle and the player activates the departure launcher
- **THEN** available hunters appear as draggable cards and one to four of them can be placed in the expedition slots

### Requirement: The View only submits player intent
The departure View SHALL preserve squad and route choices while navigating its card pages, but SHALL NOT directly mutate the settlement roster, active hunt content, or game phase.

#### Scenario: Player returns from route selection
- **WHEN** the player selects “重新编队”
- **THEN** the previously staged hunter order is restored and no authoritative departure state has changed

#### Scenario: Player cancels preparation
- **WHEN** the player closes the departure table before confirmation
- **THEN** the game remains in Settlement and no departure command is committed

### Requirement: Settlement runner commits a valid roster
The active Settlement ActionQueue environment SHALL revalidate that the submitted roster contains one to four unique, currently available hunters belonging to the settlement before replacing `DepartingHunterIds`.

#### Scenario: Hunter availability changes while the table is open
- **WHEN** confirmation reaches the runner after a selected hunter becomes unavailable
- **THEN** the command fails without changing the previous departure roster or publishing a prepared fact

### Requirement: Campaign runner performs the phase handoff
After roster preparation succeeds, the orchestration boundary SHALL configure the selected destination and request the Settlement-to-Hunt transition through the Campaign ActionQueue.

#### Scenario: A valid departure is confirmed
- **WHEN** the Settlement action commits and the Campaign transition succeeds
- **THEN** Hunt initializes with the staged hunters, selected destination content, and an active Hunt runner

#### Scenario: Campaign transition is rejected or cancelled
- **WHEN** destination state was staged but the phase transition does not commit
- **THEN** the previous destination selection is restored and the game remains operable in Settlement

### Requirement: Concurrent settlement flows do not overlap
The departure table SHALL NOT open while another Settlement action chain is awaiting input, and duplicate departure confirmations SHALL NOT start parallel cross-runner handoffs.

#### Scenario: A settlement event is unresolved
- **WHEN** the player activates the departure launcher
- **THEN** the event cards retain input ownership and no departure panel is opened

### Requirement: Destination content remains configuration-driven
Available routes SHALL come from `PlayableHuntDestinationCatalog`; when no route is available, the existing configured hunt content SHALL remain a valid fallback without changing the View contract.

#### Scenario: The catalog has no route available for the current year
- **WHEN** a valid squad continues from the staging table
- **THEN** the departure can use fallback hunt content and does not retain a previous route accidentally
