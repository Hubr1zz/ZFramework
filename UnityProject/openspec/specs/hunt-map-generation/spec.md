---
schemaVersion: 2
category: feature
title: "狩猎地图生成"
---

# Hunt Map Generation Specification

## Purpose

按地区地形配置生成完整六边形狩猎地图，并保证起始地块与成组地形规则成为稳定、可重放的 GameCore 权威状态。

## Requirements

### Requirement: Weighted terrain generation
The GameCore map generator SHALL fill every coordinate within the configured radius from the supplied weighted terrain definitions.

#### Scenario: A hunt map is generated
- **WHEN** the Adapter maps the destination tile pool into GameCore definitions
- **THEN** every radial coordinate is represented exactly once and the resulting map is deterministic for the same random sequence

### Requirement: Starting tile reservation
The configured starting tile SHALL own the origin before any terrain group is placed.

#### Scenario: A large terrain group reaches the origin
- **WHEN** grouped placement has enough members to cover the origin
- **THEN** the origin remains the revealed starting tile and the group uses only remaining coordinates

### Requirement: Configurable grouped terrain
Terrain definitions marked for grouped spawning SHALL retain their placed members and SHALL honor whether members must be adjacent.

#### Scenario: Adjacent group placement is configured
- **WHEN** a terrain definition requests four adjacent members
- **THEN** up to four available coordinates form one connected group and later generation does not overwrite them

#### Scenario: Dispersed group placement is configured
- **WHEN** a terrain definition disables adjacency
- **THEN** its additional members may be selected from any remaining map coordinates

### Requirement: Layered configuration mapping
The Unity Adapter SHALL map `spawnInGroup`, `groupSize`, and `mustBeAdjacent` from `HexTileData` into the GameCore definition without making the View authoritative.

#### Scenario: ScriptableObject terrain data enters generation
- **WHEN** `HexMapGenerator` converts a configured terrain asset
- **THEN** the GameCore generator receives all group-placement fields and the generated `HexTileInstance` values only present the resulting state
