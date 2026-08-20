---
schemaVersion: 2
category: feature
title: "3D 桌面开场序列"
---

# Tabletop Opening Sequence Specification

## Purpose

让存档入口与开场叙事成为玩家进入营地前首先操作的世界空间实体卡，并阻止后台营地事件与开场内容同时争夺桌面。

## Requirements

### Requirement: Campaign entry is presented with physical cards
The normal playable bootstrap SHALL present continue, new campaign, overwrite confirmation, and opening narrative choices through world-space 3D cards instead of screen-space IMGUI.

#### Scenario: A save exists
- **WHEN** save discovery completes successfully
- **THEN** the table presents separate physical continue and new-campaign cards

#### Scenario: A saved campaign may be overwritten
- **WHEN** the player selects new campaign while a save exists
- **THEN** a separate physical confirmation layout offers return and irreversible confirmation without deleting the save early

### Requirement: Opening presentation owns the visible tabletop
The opening sequence SHALL hide the active phase presentation root until the player loads a campaign or completes the opening narrative.

#### Scenario: A settlement event is already waiting for input
- **WHEN** the opening sequence is visible
- **THEN** the settlement ActionQueue may remain suspended, but its cards and the settlement table are not simultaneously visible or interactable

#### Scenario: The opening sequence completes
- **WHEN** a save loads successfully or the player accepts the opening narrative
- **THEN** the opening cards close and only the current phase presentation root becomes active

### Requirement: Save operations remain asynchronous and recoverable
Save discovery, loading, and deletion SHALL use the existing persistence Adapter with lifecycle cancellation, and a failed operation SHALL leave a usable physical choice flow.

#### Scenario: Save loading fails
- **WHEN** the persistence Adapter reports a failed load
- **THEN** the menu remains open, continue is disabled, and starting a new campaign remains available

#### Scenario: New-campaign deletion succeeds
- **WHEN** the confirmed save deletion completes
- **THEN** the opening narrative is presented when configured, otherwise the gameplay tabletop is released immediately
