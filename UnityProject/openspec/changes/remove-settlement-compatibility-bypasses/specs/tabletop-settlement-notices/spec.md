---
schemaVersion: 2
category: feature
title: "3D 营地消息记录兼容边界"
---

# 3D 营地消息记录兼容边界 Delta

## MODIFIED Requirements

### Requirement: Settlement notices are world-space cards

The three old IMGUI Toast sources (`PlayableGrowthMilestoneToast`, `PlayableHunterLossToast`, `PlayableWeaponMasteryToast`) SHALL be absent from the normal project surface. `SettlementNoticePresenter3D` SHALL remain the sole presenter for the corresponding after-commit facts, without a parallel screen-space feedback path.

#### Scenario: A growth, loss or mastery fact is committed

- **WHEN** the Settlement runner publishes the after-commit fact
- **THEN** SettlementNoticePresenter3D presents it in world space
- **AND** no deleted IMGUI Toast type is instantiated or required

### Requirement: Notices remain non-authoritative

Removing the old Toast components SHALL NOT make the presenter authoritative: it SHALL continue to consume committed facts only and SHALL NOT mutate Settlement, ActionQueue or phase state.

#### Scenario: A player dismisses a notice

- **WHEN** the player dismisses the world-space notice
- **THEN** only the presentation queue advances
- **AND** no gameplay compatibility entry is invoked
