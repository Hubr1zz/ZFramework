---
schemaVersion: 2
category: feature
title: "3D 狩猎小队棋子移动"
---

# Tabletop Hunt Squad Pawn Specification

## Purpose

让已提交的狩猎小队位置以实体桌游棋子表达，并用短暂移动过程建立清晰的空间反馈，同时保持 Hunt 地图状态的单一权威来源。

## Requirements

### Requirement: The squad is represented by physical hunter pawns
The Hunt map SHALL present the deployed squad as a 3D pawn group whose visible hunter count reflects the committed active hunter roster within the supported party capacity.

#### Scenario: A valid hunt session is presented
- **WHEN** the Hunt map is built with active hunters
- **THEN** the squad group appears at the committed starting tile with one visible pawn per active hunter

#### Scenario: An invalid empty roster reaches the View
- **WHEN** the active hunter count is zero
- **THEN** the pawn base remains stable and no phantom hunter pawn is displayed

### Requirement: Committed movement receives tabletop motion feedback
The View SHALL snap the initial squad placement and SHALL animate subsequent committed positions over the configured duration with a bounded hop.

#### Scenario: The map is initialized
- **WHEN** the authoritative starting coordinate is first presented
- **THEN** the pawn group appears at that coordinate without replaying a movement animation

#### Scenario: A squad move is committed
- **WHEN** `OnSquadMoved` presents a new authoritative coordinate
- **THEN** the pawn group turns toward and animates to the corresponding world position before settling exactly on it

### Requirement: Movement presentation owns temporary Hunt input
The pawn SHALL hold the shared Hunt input guard while moving and SHALL release it after settling, disabling, destruction, or an immediate placement.

#### Scenario: A move animation is in progress
- **WHEN** the pawn has not reached its committed destination
- **THEN** Hunt map commands remain blocked and no duplicate movement command can be submitted through the View

#### Scenario: The map is rebuilt during movement
- **WHEN** the moving pawn is disabled for visual cleanup
- **THEN** it settles to its committed destination and releases its input guard immediately

### Requirement: The pawn is presentation-only
The squad pawn SHALL consume committed position and roster data and SHALL NOT mutate Hunt map state, publish movement events, or issue gameplay commands.

#### Scenario: The pawn finishes moving
- **WHEN** its animation reaches the destination
- **THEN** only its transform and input-guard ownership change while the authoritative Hunt state remains untouched

### Requirement: World interactions follow the squad presentation
The Hunt tabletop interaction anchor SHALL resolve to the live squad pawn so physical dice and cards can be presented near the current group position.

#### Scenario: A world presentation requests the squad anchor
- **WHEN** the Hunt map owns a live pawn group
- **THEN** the pawn transform is returned as the interaction anchor
