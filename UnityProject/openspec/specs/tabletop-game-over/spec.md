---
schemaVersion: 2
category: feature
title: "3D 桌面战役终局"
---

# Tabletop Game Over Specification

## Purpose

让战役失败以跨阶段的世界空间实体卡收束当前流程，统一冻结仍在桌面上的后台交互，并在不推进决战玩法细节的前提下提供清晰、可恢复的重新开始入口。

## Requirements

### Requirement: Campaign defeat is presented on the tabletop
The normal playable flow SHALL present campaign defeat and restart intent through world-space 3D cards instead of creating the legacy screen-space game-over UI.

#### Scenario: The campaign publishes a game-over fact
- **WHEN** any phase publishes `GameOverEvent`
- **THEN** a camera-centered physical defeat card and a physical restart choice become visible

### Requirement: The defeat presentation owns interaction
The defeat presentation SHALL suspend background collider interaction while keeping its own choice card interactive.

#### Scenario: A background collider already exists
- **WHEN** the defeat presentation opens
- **THEN** the collider is disabled until the presentation closes

#### Scenario: A background collider appears after opening
- **WHEN** another flow creates an enabled collider while defeat remains visible
- **THEN** the new collider is captured and disabled without disabling the defeat cards

### Requirement: Restart releases the tabletop before resetting the campaign
The restart choice SHALL close the defeat presentation and release captured input before invoking campaign reset and returning to Settlement.

#### Scenario: The player starts another campaign
- **WHEN** the player selects the physical restart card
- **THEN** captured colliders are restored, persisted progress is deleted through the existing persistence Adapter, and the campaign resets to Settlement
