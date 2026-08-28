---
schemaVersion: 2
category: feature
title: "跨阶段桌面事件交互"
---

## MODIFIED Requirements

### Requirement: Settlement and hunt share one event input port
Settlement and Hunt SHALL present narrative, choice, check, and result prompts through the shared `IPlayableEventInput` contract while retaining independent phase runners and execution environments.

The playable bootstrap SHALL install only the world-space event input path for normal play. Settlement composition SHALL NOT require, configure, or refresh a legacy screen-space HUD, and the event presenter SHALL remain the only View-side input path.

#### Scenario: Either phase reaches an event node
- **WHEN** its ActionQueue awaits player input
- **THEN** the same world-space event presenter supplies the decision without directly mutating event state

#### Scenario: Normal settlement presentation starts
- **WHEN** the playable bootstrap assembles Settlement presentation
- **THEN** the world-space event input is available independently of any screen-space HUD
- **AND** no legacy Settlement event or departure panel is installed
