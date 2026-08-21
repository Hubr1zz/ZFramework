---
schemaVersion: 2
category: feature
title: "狩猎小队行动资格与失能交接"
---

# Hunt Squad Action Availability Specification

## Purpose

保证狩猎事件造成伤亡后，地图、采集与 3D 状态桌立即使用同一存活猎人资格；远征小队失去全部行动者时停止探索，但仍保留正式回营结算路径。

## Requirements

### Requirement: Hunt actions require a living squad actor
Tile reveal, squad movement, resource-point selection, and harvest preparation SHALL require at least one living deployed hunter. These actions SHALL fail without mutating map, resource-point, or collectible state when no living actor remains.

#### Scenario: The expedition has lost every deployed hunter
- **WHEN** another tile interaction or harvest preparation is requested
- **THEN** the request is rejected without revealing, moving, reserving, or exhausting content
- **AND** the existing tabletop retreat command remains available

### Requirement: Event commits normalize the selected hunter
After each Hunt event resolution checkpoint, the Hunt authority SHALL retain the selected hunter only when that hunter is still living and deployed; otherwise it SHALL select the first living deployed hunter before publishing the committed event fact.

#### Scenario: An event kills the current action owner
- **WHEN** another deployed hunter remains alive
- **THEN** that survivor becomes the selected hunter before committed observers refresh
- **AND** chained events and subsequent Hunt commands do not reuse the dead hunter

### Requirement: Harvest commit revalidates hunter life
A harvest transaction SHALL revalidate its hunter at final commit. If the hunter is no longer alive, the transaction SHALL release its resource-point reservation, SHALL NOT exhaust the point or grant collectibles, and SHALL become closable instead of retrying indefinitely.

#### Scenario: The hunter dies after revealing a harvest card
- **WHEN** the final harvest commit is attempted
- **THEN** the transaction ends with a player-readable failure
- **AND** the resource point remains available for a future living hunter

### Requirement: Squad loss is explained in world space
The normal 3D Hunt status board SHALL replace exploration guidance with a loss explanation when no living deployed hunter remains and SHALL direct the player to the physical retreat card.

#### Scenario: Event resolution removes the final active hunter
- **WHEN** the committed event fact refreshes the status board
- **THEN** the board explains that exploration and harvesting have ended
- **AND** it identifies the tabletop retreat card as the remaining flow command
