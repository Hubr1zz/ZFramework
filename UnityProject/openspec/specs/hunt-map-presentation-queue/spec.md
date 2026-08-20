---
schemaVersion: 2
category: system
title: "狩猎地图表现队列"
---

# Hunt Map Presentation Queue Specification

## Purpose

让狩猎地图的权威状态、3D 桌游表现与后续事件保持单一 ActionQueue 因果顺序，并允许不同表现实现通过端口接入。

## Requirements

### Requirement: Committed map interactions await their 3D presentation
The Hunt Action environment SHALL wait for the matching tile-flip or squad-movement presentation after authoritative state commit and before continuing the interaction chain.

#### Scenario: A hidden tile is revealed
- **WHEN** a reveal command commits its authoritative tile state
- **THEN** the tile event does not begin until the configured 3D flip presentation has settled

#### Scenario: The squad moves to a revealed tile
- **WHEN** a move command commits its authoritative squad coordinate
- **THEN** the root command remains active until the 3D squad pawn has settled at that coordinate

### Requirement: Presentation remains replaceable and non-authoritative
The Action layer SHALL depend on a Hunt presentation port and SHALL continue immediately when no presenter is configured.

#### Scenario: A non-visual Hunt environment executes a command
- **WHEN** no tile interaction presenter is available
- **THEN** the committed gameplay chain completes without a presentation dependency

#### Scenario: A presentation implementation fails after commit
- **WHEN** the presenter throws without lifecycle cancellation
- **THEN** the failure is reported and the already committed gameplay chain continues without rollback

### Requirement: Environment lifetime cancels presentation waits
The presentation wait SHALL observe the Hunt Action environment cancellation token.

#### Scenario: The Hunt session is disposed during presentation
- **WHEN** the tile flip or squad move is still pending
- **THEN** the wait is cancelled and the Action chain does not continue into later event resolution
