---
schemaVersion: 2
category: feature
title: "3D 狩猎地图入场运镜"
---

# Tabletop Hunt Map Intro Specification

## Purpose

在狩猎地图完成实体地形卡构建后，先用短暂俯瞰交代整张桌面地图，再把镜头稳定到可操作位置，避免玩家进入阶段时缺少空间方向感。

## Requirements

### Requirement: The intro frames the committed map
The Hunt View SHALL derive a deterministic camera plan from the world positions of the committed map tiles and SHALL aim both overview and play positions at the resulting map center.

#### Scenario: A generated map is presented
- **WHEN** the Hunt map contains one or more tile presentations
- **THEN** the intro begins above the complete tile bounds and settles at the configured play height while preserving the configured pitch

#### Scenario: Map data is missing or non-finite
- **WHEN** no tile position exists or any position contains a non-finite component
- **THEN** no invalid camera plan is published and any held intro input guard is released safely

### Requirement: The camera settles before gameplay input opens
The intro View SHALL own the shared Hunt input guard from presentation request until the camera reaches its play position.

#### Scenario: The camera is moving
- **WHEN** the overview-to-play animation has not completed
- **THEN** tile, resource, retreat, and other Hunt commands remain blocked

#### Scenario: The intro completes
- **WHEN** normalized progress reaches one
- **THEN** the camera is placed exactly at the planned play transform and the intro releases only its own input guard

### Requirement: Phase lifecycle cannot strand input
The intro SHALL settle and release its input ownership when skipped, disabled, destroyed, deprived of its camera, or unable to observe an enabled Hunt camera controller within the configured activation timeout.

#### Scenario: The Hunt root is disabled mid-intro
- **WHEN** a phase transition disables the intro View
- **THEN** the camera settles to the planned play transform and the Hunt input guard is released idempotently

#### Scenario: Camera activation never arrives
- **WHEN** a disabled Hunt camera controller remains disabled beyond the activation timeout
- **THEN** the intro degrades to its normal presentation and completion path instead of waiting forever

### Requirement: Presentation is independent of gameplay time scale
The intro SHALL advance with unscaled time and SHALL NOT mutate Hunt map state, publish gameplay facts, or issue ActionQueue commands.

#### Scenario: Gameplay time is paused during entry
- **WHEN** scaled delta time is zero while the intro is active
- **THEN** the camera still reaches its play position and restores interaction ownership

### Requirement: Intro tuning remains editor-configurable
Duration, pitch, play height, overview scale, overview height bounds, and activation timeout SHALL remain serialized View configuration.

#### Scenario: Imported configuration contains reversed or negative limits
- **WHEN** serialized values fall outside their supported ranges
- **THEN** the planner normalizes them to finite safe bounds without changing authoritative map data
