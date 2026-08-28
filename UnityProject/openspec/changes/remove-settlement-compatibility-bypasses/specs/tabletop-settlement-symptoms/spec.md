---
schemaVersion: 2
category: feature
title: "营地桌面症状成长兼容边界"
---

# 营地桌面症状成长兼容边界 Delta

## MODIFIED Requirements

### Requirement: 症状变化由 Settlement Runner 掌权

The normal playable flow SHALL use the 3D hunter equipment/symptom panel and Settlement ActionQueue. The legacy `PlayableSymptomGrowthService` and `PlayableSymptomGrowthView` screen-space sources SHALL be absent, with no replacement compatibility UI or second symptom state.

#### Scenario: The settlement table opens symptom actions

- **WHEN** a hunter has a configured unresolved symptom
- **THEN** the 3D symptom panel presents the choice and submits it to Settlement Runner
- **AND** no old screen-space Service/View is created

### Requirement: 正常流程不创建旧屏幕症状窗口

The formal playable bootstrap SHALL NOT instantiate `PlayableSymptomGrowthView` or depend on `PlayableSymptomGrowthService`; symptom state SHALL remain owned by the existing 3D panel and Settlement ActionQueue.

#### Scenario: The formal settlement table is built

- **WHEN** the playable Settlement root is assembled
- **THEN** the 3D symptom entry remains available where applicable
- **AND** neither deleted screen-space source is created
