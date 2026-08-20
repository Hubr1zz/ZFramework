---
schemaVersion: 2
category: feature
title: "3D 狩猎小队状态桌"
---

# Tabletop Hunt Squad Status Specification

## Purpose

让狩猎阶段以地图边缘的实体卡展示小队状态与行动者选择，避免常驻屏幕 HUD 破坏桌游感或形成玩法命令旁路。

## Requirements

### Requirement: Normal Hunt status is world-space
The normal 3D Hunt map SHALL present destination progress and up to four deployed hunters on a world-space status board, and SHALL NOT create the legacy screen-space top bar, hunter overlay, harvest popup, or event popup.

#### Scenario: Hunt presentation initializes
- **WHEN** a live `HuntMapVisualizer` is available
- **THEN** the status board appears beside the map with one summary card and one card per deployed hunter up to squad capacity

### Requirement: Hunter cards select the action owner
Each living hunter card SHALL submit its hunter identity through `HuntManager.SelectHunter`, and SHALL ignore selection while another tabletop Hunt interaction owns input.

#### Scenario: Player selects another hunter
- **WHEN** Hunt input is available and the player clicks a different living hunter card
- **THEN** `SelectedHunter` changes and the cards refresh their selected state

### Requirement: Cards reflect committed Hunt state
The status board SHALL refresh from Hunt ActionQueue committed tile and harvest events and SHALL show current body-part health, willpower, and carried-resource count.

#### Scenario: A Hunt action commits
- **WHEN** tile interaction or harvest state is committed
- **THEN** summary progress and hunter card values are refreshed from authoritative Hunt state

### Requirement: Harvest remains physical on the normal map
The normal 3D Hunt path SHALL open physical harvest cards even when a resource marker presentation position is temporarily unavailable, using the map interaction anchor as a bounded fallback.

#### Scenario: Resource marker position cannot be resolved
- **WHEN** the active Hunt still has a live 3D map visualizer
- **THEN** harvest cards open near the map interaction anchor instead of falling back to screen-space UI

### Requirement: Flow guidance does not duplicate gameplay commands
After the optional opening narrative closes, the flow guide SHALL stop rendering and SHALL NOT expose direct phase-transition or turn-ending controls.

#### Scenario: Opening narrative is dismissed
- **WHEN** the player continues from the opening or the start flow skips it
- **THEN** the guide component disables itself and all subsequent gameplay commands remain on their owning tabletop views
