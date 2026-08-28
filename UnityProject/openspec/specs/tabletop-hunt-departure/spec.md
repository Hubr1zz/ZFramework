---
schemaVersion: 2
category: feature
title: "3D 狩猎远征整备"
---

# Tabletop Hunt Departure Specification

## Purpose

让玩家在营地桌面以实体猎人卡编成小队并选择狩猎地区，同时由 Settlement 与 Campaign ActionQueue 分别掌握名册提交和阶段切换的权威状态。

营地出猎世界空间端口由正式组合根独立安装，不受旧屏幕 HUD 可见性开关影响。

## Requirements

### Requirement: Departure preparation is a world-space card flow
The normal settlement departure flow SHALL use a 3D launcher card, draggable hunter cards, a four-slot squad grid, and physical destination cards instead of a screen-space confirmation window.

The departure world-space input port SHALL be installed whenever the playable settlement bootstrap is active, regardless of legacy HUD visibility settings.

#### Scenario: Player prepares a hunt
- **WHEN** the settlement runner is idle and the player activates the departure launcher
- **THEN** available hunters appear as draggable cards and one to four of them can be placed in the expedition slots

#### Scenario: Legacy HUD visibility is disabled
- **WHEN** the playable bootstrap starts with legacy settlement HUD visibility disabled
- **THEN** the world-space departure port and destination selection remain available
- **AND** no screen-space HUD is required for the departure command

### Requirement: Hunter cards expose read-only departure decisions
The squad preparation table SHALL let the player click a hunter card without dragging to inspect that hunter's current hit locations, willpower, key attributes, bounded traits, bounded equipment summary, and signed equipment-noise contribution. Inspection SHALL reuse the existing 3D primary card and SHALL NOT open an equipment editor or mutate the staged squad.

Dragging a hunter card SHALL only express placement intent and SHALL NOT open or replace the current inspection.

#### Scenario: Player inspects a staged hunter
- **WHEN** the player clicks a hunter card without starting a drag
- **THEN** the 3D primary card SHALL show the selected hunter's departure-relevant details
- **AND** the hunter's current slot and staged squad order SHALL remain unchanged

#### Scenario: Player rearranges a hunter after inspection
- **WHEN** the player drags a different hunter card after inspecting one hunter
- **THEN** the drag SHALL complete or return to its origin according to the existing slot rules
- **AND** no inspection intent, GameAction, or authoritative equipment change SHALL be produced

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

### Requirement: Every departure entry uses the authoritative command path
Every current or compatibility departure entry SHALL submit through the same Settlement departure request boundary. Compatibility APIs and notification events SHALL NOT directly replace the departing roster or change the campaign phase.

#### Scenario: A legacy departure entry is invoked
- **WHEN** an old HUD, UnityEvent, or compatibility caller requests departure
- **THEN** the request is either accepted by the Settlement and Campaign runners or rejected without changing the roster, publishing a departure fact, or entering Hunt

#### Scenario: A departure request is already in flight
- **WHEN** another entry submits the same or a different roster before the current handoff completes
- **THEN** no parallel preparation or campaign transition is started

### Requirement: Concurrent settlement flows do not overlap
The departure table SHALL NOT open while another Settlement action chain is awaiting input, and duplicate departure confirmations SHALL NOT start parallel cross-runner handoffs.

#### Scenario: A settlement event is unresolved
- **WHEN** the player activates the departure launcher
- **THEN** the event cards retain input ownership and no departure panel is opened

#### Scenario: Departure preparation owns the tabletop
- **WHEN** the squad or destination cards are open
- **THEN** the underlying settlement table is hidden so its recruitment, equipment, workshop, and ledger cards cannot open overlapping flows
- **AND** cancelling preparation restores the settlement table to its previous active state

### Requirement: Destination content remains configuration-driven
Available routes SHALL come from `PlayableHuntDestinationCatalog`. When at least one route is available for the current year, departure SHALL require an explicit valid selection. When no route is available, the existing configured hunt content SHALL remain a valid fallback without changing the View contract.

The production catalog SHALL provide two distinct routes from year 1 and SHALL unlock a third high-noise mixed ruins-and-swamp route from year 2. Each route SHALL own distinct configured Hunt content while reusing the existing destination-card View and campaign departure boundary. The production bootstrap SHALL keep the legacy settlement HUD disabled; this setting SHALL NOT disable the world-space departure ports.

#### Scenario: A route is available but no route was selected
- **WHEN** any departure entry submits a valid squad without an explicit destination
- **THEN** the request is rejected in Settlement and the current destination state is preserved

#### Scenario: The catalog has no route available for the current year
- **WHEN** a valid squad continues from the staging table
- **THEN** the departure can use fallback hunt content and does not retain a previous route accidentally

#### Scenario: The campaign reaches year 2
- **WHEN** the player opens the destination-card page after the first completed hunt
- **THEN** the two year-1 routes and the `echoing-broken-road` route are available
- **AND** the new route uses its own tile, event, and noise configuration, including the route-local `hunt_broken_road_echo` event, without introducing a route-specific View or runtime branch
