---
schemaVersion: 2
category: architecture
title: "GameManager 管理器兼容边界"
---

# GameManager 管理器兼容边界 Delta

## MODIFIED Requirements

### Requirement: GameManager delegates domain behavior

GameManager SHALL remain a Unity composition shell and SHALL NOT expose the removed growth async facade or legacy departure facades (`SpendHunterGrowthAsync`, `RequestHuntDeparture`, `CanRequestHuntDeparture`, `TryDepartForHunt`). CampaignFlowCoordinator SHALL NOT provide or inject `ISettlementDepartureRequestPort`. Settlement growth SHALL be reached through the Settlement gameplay port, while departure SHALL remain available through the formal 3D input registration and typed `DepartForHuntAsync` command boundary.

#### Scenario: A caller inspects legacy public bypasses

- **WHEN** a caller reflects on GameManager's public API
- **THEN** the removed growth and departure facades are absent
- **AND** the formal 3D departure input registration and typed command remain available
