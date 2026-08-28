---
schemaVersion: 2
category: feature
title: "3D 狩猎远征整备"
---

## MODIFIED Requirements

### Requirement: Destination content remains configuration-driven
Available routes SHALL come from `PlayableHuntDestinationCatalog`. The destination-card page SHALL present every valid configured route, including routes that are unavailable for the current year. A locked route SHALL expose its current unavailability reason and SHALL NOT be selectable or confirmable.

When at least one route is available for the current year, departure SHALL require an explicit valid selection. The View SHALL prefer the active available route and otherwise select the first available route. If a departure attempt fails, the View SHALL rebuild availability from the current campaign year and preserve the attempted route by stable destination ID when it remains available. When no route is available, the existing configured hunt content SHALL remain a valid fallback without changing the View contract.

The production catalog SHALL provide two distinct routes from year 1 and SHALL unlock a third high-noise mixed ruins-and-swamp route from year 2. Each route SHALL own distinct configured Hunt content while reusing the existing destination-card View and campaign departure boundary. The production bootstrap SHALL keep the legacy settlement HUD disabled; this setting SHALL NOT disable the world-space departure ports.

#### Scenario: A future route is visible but locked
- **WHEN** the player opens the destination-card page in year 1
- **THEN** the two year-1 routes are selectable and `echoing-broken-road` is shown as a locked 3D card
- **AND** the locked card displays its availability reason and cannot replace the current selection or submit a departure

#### Scenario: A route is available but no route was selected
- **WHEN** any departure entry submits a valid squad without an explicit destination
- **THEN** the request is rejected in Settlement and the current destination state is preserved

#### Scenario: The catalog has no route available for the current year
- **WHEN** a valid squad continues from the staging table
- **THEN** the departure can use fallback hunt content and does not retain a previous route accidentally

#### Scenario: A departure attempt is rejected after route selection
- **WHEN** the authoritative departure transaction rejects a selected route while the game remains in Settlement
- **THEN** the View rebuilds route availability for the current year
- **AND** the attempted stable route remains selected only if it is still available

#### Scenario: The campaign reaches year 2
- **WHEN** the configured seasons of year 1 have all been completed and the player opens the destination-card page in year 2
- **THEN** the two year-1 routes and the `echoing-broken-road` route are available
- **AND** the new route uses its own tile, event, and noise configuration, including the route-local `hunt_broken_road_echo` event, without introducing a route-specific View or runtime branch
