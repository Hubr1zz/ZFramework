---
schemaVersion: 2
category: feature
title: "营地桌面成长训练兼容边界"
---

# 营地桌面成长训练兼容边界 Delta

## MODIFIED Requirements

### Requirement: 成长分配由 Settlement Runner 掌权

Growth SHALL be submitted by the 3D hunter advancement View to the Settlement ActionQueue gameplay port. `PlayableHunterAdvancementAdapter` SHALL retain only the after-hunt application overloads and `HunterGrowthSpentEvent`; it SHALL NOT expose a direct `TrySpendGrowth` mutation entry, and GameManager SHALL NOT expose a duplicate growth facade.

#### Scenario: A player spends a growth point

- **WHEN** the 3D growth card submits a hunter and choice
- **THEN** Settlement Runner revalidates and commits the growth
- **AND** no direct adapter or GameManager bypass is available
