---
schemaVersion: 2
category: feature
title: 营地桌面猎人休养
---

# Tabletop Settlement Recovery Specification

## Purpose

定义玩家从 3D 猎人卡进入装备桌，再以实体部位卡处理普通伤势的流程；View 只展示与提交意图，恢复规则、资源消费、事件和存档继续归属 Settlement ActionQueue。

## Requirements

### Requirement: Recovery begins from the selected hunter card
The hunter equipment board SHALL expose a world-space recovery entry only while the selected available hunter has recoverable ordinary wounds.

#### Scenario: A wounded hunter is inspected
- **WHEN** the player opens that hunter's equipment board
- **THEN** a recovery control opens four physical body-part cards for head, torso, arms, and legs

#### Scenario: A healthy hunter is inspected
- **WHEN** every supported body part is at maximum ordinary health
- **THEN** the recovery entry is hidden

### Requirement: Body-part cards communicate current treatment state
Each recovery card SHALL show current and maximum health and SHALL only submit treatment when the base recovery rule and configured resource cost are currently satisfied.

#### Scenario: A body part is healthy or resources are missing
- **WHEN** the recovery board refreshes
- **THEN** that card explains why treatment is unavailable and does not submit a command when activated

### Requirement: Recovery remains authoritative in the Settlement runner
The recovery board SHALL submit the existing recovery command without directly changing health or resources, and SHALL suppress duplicate input while the command is pending.

#### Scenario: Treatment commits
- **WHEN** the Settlement action succeeds
- **THEN** the affected body-part cards and resource cards refresh, a recovery fact and Recovery transaction are published, and settlement persistence observes the commit

#### Scenario: A reactor prevents treatment
- **WHEN** a BeforeExecution reactor rejects recovery
- **THEN** health and resources remain unchanged and the board displays the failure reason

### Requirement: Normal play does not create the legacy recovery window
The playable bootstrap SHALL NOT instantiate the screen-space hunter recovery View after the world-space recovery flow is available.

#### Scenario: Settlement starts normally
- **WHEN** the playable bootstrap installs Settlement views
- **THEN** hunter recovery is reached through 3D hunter interaction rather than a screen-space camp button

### Requirement: Recovery tuning remains configuration-driven
Recovery resource, cost, and amount SHALL continue to come from the configured settlement content catalog.

#### Scenario: Recovery tuning changes
- **WHEN** the catalog assigns a different cost or recovery amount
- **THEN** the body-part board and Settlement action use the new values without changing View command types
