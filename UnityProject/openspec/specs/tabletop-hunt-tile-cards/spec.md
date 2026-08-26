---
schemaVersion: 2
category: feature
title: "3D 狩猎地形卡翻面"
---

# Tabletop Hunt Tile Cards Specification

## Purpose

让狩猎地图以实体六边形地形卡表达探索边界：未知地块背面朝上，已提交的揭示状态通过短暂翻面展示正面信息，而不建立第二套地图事实。

## Requirements

### Requirement: Hidden terrain uses physical card backs
The Hunt map SHALL present locked and interactable terrain as face-down 3D hex cards and SHALL not expose locked terrain identity.

#### Scenario: A tile is locked
- **WHEN** the map first presents a non-starting locked tile
- **THEN** the card is face down and neither face displays its terrain name

#### Scenario: A tile becomes interactable
- **WHEN** the authoritative tile state changes from locked to interactable
- **THEN** the card remains face down and its back shows only the configured terrain name
- **AND** it does not reveal the event, resource pool, encounter marker or random outcome

### Requirement: Hidden terrain requires a physical reveal commitment
Clicking an interactable terrain card SHALL open a world-space scout confirmation before issuing a Hunt gameplay command. The confirmation SHALL remain presentation-only and SHALL create a fresh exploration snapshot only after the player commits.

#### Scenario: The player inspects and cancels a terrain card
- **WHEN** the player clicks an interactable terrain card and chooses cancel
- **THEN** the scout cards close without changing tile state, event occurrences or Hunt checkpoints

#### Scenario: The player confirms a terrain reveal
- **WHEN** the player chooses reveal on the scout cards
- **THEN** the View closes its scout input lease and requests a fresh snapshot from the current Hunt session
- **AND** only a valid snapshot enters the existing Hunt ActionQueue reveal flow

#### Scenario: The player clicks an already revealed terrain card
- **WHEN** a revealed adjacent card is selected for movement
- **THEN** the View submits the existing movement command without opening scout confirmation

### Requirement: Scout input ownership follows the Hunt presentation lifetime
The scout confirmation SHALL block conflicting Hunt input while open and SHALL release only its own input ownership on cancel, confirm, disable, destruction or presentation-generation replacement.

#### Scenario: The Hunt presentation is replaced while scouting
- **WHEN** the active map View is cleared or replaced before a choice is made
- **THEN** the scout cards close and their input lease is released without issuing a gameplay command

### Requirement: Committed reveals flip to the configured front
The View SHALL animate a card from its current back orientation to its front after the authoritative tile state becomes revealed.

#### Scenario: The starting tile is already revealed
- **WHEN** the map is initially constructed with a revealed starting tile
- **THEN** that tile appears face up immediately without replaying a reveal animation

#### Scenario: Exploration reveals a tile
- **WHEN** a previously hidden tile is committed as revealed
- **THEN** the card flips over the configured duration and its front shows the configured terrain name

### Requirement: Flip presentation does not own gameplay state
The terrain card View SHALL only consume `HexTileInstance` and `TileState` presentation data and SHALL NOT mutate map state or issue exploration commands.

#### Scenario: A flip is in progress
- **WHEN** the reveal animation has not finished
- **THEN** the tile collider is disabled and becomes available again after the card settles, while the committed tile state remains unchanged

### Requirement: Missing display data degrades safely
The terrain card SHALL remain presentable when a revealed tile has no configured display record.

#### Scenario: A revealed tile has no config
- **WHEN** the View receives a revealed tile with missing configuration
- **THEN** the front uses a neutral unknown-terrain label and the flip completes without an exception

### Requirement: Encounter markers are idempotent presentation state
The Hunt map View SHALL maintain at most one Boss encounter marker for each coordinate and SHALL remove its marker when that tile no longer qualifies for presentation.

#### Scenario: A revealed Boss tile is refreshed repeatedly
- **WHEN** the same authoritative tile state is presented more than once
- **THEN** the tile contains exactly one Boss marker

#### Scenario: A coordinate is rebuilt without a visible Boss encounter
- **WHEN** the View presents that coordinate as hidden or without a Boss encounter
- **THEN** its previous Boss marker is removed without changing gameplay state
