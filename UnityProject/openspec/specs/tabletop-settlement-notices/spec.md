---
schemaVersion: 2
category: feature
title: "3D 营地消息记录"
---

# Tabletop Settlement Notices Specification

## Purpose

让永久损失和长期成长以不会互相覆盖的实体记录卡返回营地桌面，同时保持提交事件为唯一事实来源，并确保玩家离开营地期间不会错过关键战役反馈。

## Requirements

### Requirement: Settlement notices are world-space cards
The normal playable flow SHALL present hunt completion records, hunter growth milestones, permanent hunter losses, and weapon mastery changes as world-space cards and SHALL NOT create their legacy screen-space toast components.

#### Scenario: A committed progression fact is published
- **WHEN** the playable game is in Settlement
- **THEN** one physical notice card and one dismiss card appear on the settlement table

#### Scenario: A Hunt return is committed

- **WHEN** the Timeline stores a Hunt record, including a return that does not advance the year
- **THEN** a physical notice summarizes deployed hunters, losses, carried materials, and current annual Hunt progress

### Requirement: Burst feedback preserves commit order
The notice presenter SHALL queue every supported fact in publication order instead of allowing a later message to overwrite an earlier one.

#### Scenario: Several outcomes commit together
- **WHEN** growth, loss, and mastery facts are published before the first notice closes
- **THEN** the player can read all three notices in the same order

### Requirement: Notices remain non-authoritative
The notice presenter SHALL only project committed facts and SHALL NOT mutate hunter, settlement, ActionQueue, or phase state.

#### Scenario: Player dismisses a notice
- **WHEN** the player activates its physical dismiss card
- **THEN** the next queued notice may appear without causing a gameplay transaction

### Requirement: Notice time only advances in Settlement
An active notice SHALL pause outside Settlement and resume when the player returns, so important campaign feedback is not consumed behind an inactive phase root.

#### Scenario: Phase leaves Settlement while a notice is visible
- **WHEN** another phase owns presentation
- **THEN** the notice remains pending at its current display duration and can still be read after returning

### Requirement: Notices have a bounded automatic exit
Each notice SHALL dismiss after a configurable unscaled duration while Settlement remains active, and the player SHALL also be able to dismiss it directly.

#### Scenario: Player ignores a notice
- **WHEN** its configured display duration expires in Settlement
- **THEN** it closes and the next queued notice becomes eligible for presentation
