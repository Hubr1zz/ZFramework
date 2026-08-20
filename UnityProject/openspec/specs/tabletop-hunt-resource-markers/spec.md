---
schemaVersion: 2
category: feature
title: "3D 狩猎资源点棋子"
---

# Tabletop Hunt Resource Markers Specification

## Purpose

把已揭示地块上的每个资源点分别表现为可辨认、可选择的实体棋子，使玩家能够决定先采集哪一种资源，并让后续素材翻牌准确出现在所选资源附近。

## Requirements

### Requirement: Every active resource point has its own pawn
The Hunt map SHALL create one independent 3D marker for every non-exhausted resource point on a revealed tile and SHALL preserve each point's original list index.

#### Scenario: A tile contains two different resources
- **WHEN** the tile reveals two non-exhausted resource point instances
- **THEN** two spatially distinct pawns appear and each pawn displays its own resource name and draw count

#### Scenario: Imported point data contains a null entry
- **WHEN** a tile resource list contains a missing point
- **THEN** no phantom pawn is created for that entry and the remaining original indices stay unchanged

### Requirement: Pawn selection targets the exact resource point
Each marker SHALL pass its committed tile coordinate and original resource-point index through `ResourceMarkerClickHandler` to the existing Hunt command port.

#### Scenario: The player selects the second pawn
- **WHEN** the player clicks the marker for resource point index one
- **THEN** `HuntManager.OnResourcePointSelected` receives index one and the harvest presentation resolves that exact instance

#### Scenario: The point became exhausted before the click commits
- **WHEN** the marker index resolves to an exhausted or missing point
- **THEN** the existing Hunt command validation rejects the interaction without opening a harvest transaction

### Requirement: Markers respect terrain-card presentation state
Resource markers SHALL remain children of their owning physical terrain card and SHALL reject pointer commands while that card is flipping.

#### Scenario: A terrain reveal is still animating
- **WHEN** the player points at or clicks a resource marker before the terrain card settles
- **THEN** the marker does not hover or submit a Hunt command

### Requirement: Harvest presentation follows the selected marker
The map SHALL resolve a world presentation position by `ResourcePointInstance` identity so the physical material pool appears beside the selected pawn rather than beside an aggregate tile marker.

#### Scenario: Multiple points share one tile
- **WHEN** either point starts a harvest interaction
- **THEN** the material-card panel uses the corresponding pawn's world anchor

### Requirement: Exhausted markers are removed safely
After a harvest commit, the map SHALL deactivate obsolete markers before deferred destruction and SHALL rebuild only the remaining active points.

#### Scenario: One of two points is exhausted
- **WHEN** the harvest commit marks the first point exhausted
- **THEN** its pawn stops receiving input immediately while the second point is rebuilt with its original command index

### Requirement: Resource pawns are presentation-only
The layout Adapter and marker View SHALL NOT mutate resource state, reveal materials, or commit rewards; authoritative harvesting remains in the Hunt ActionQueue.

#### Scenario: A marker is created or hovered
- **WHEN** its transform, label, or highlight changes
- **THEN** the associated `ResourcePointInstance` and Hunt transaction state remain unchanged
