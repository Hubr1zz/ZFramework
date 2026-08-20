---
schemaVersion: 2
category: feature
title: "跨阶段桌面事件交互"
---

# Tabletop Event Interaction Specification

## Purpose

让营地与狩猎事件复用同一套世界空间实体卡交互，同时保持阶段 ActionQueue 对事件选择、物理随机表现、规则提交和事件链推进的唯一控制权。

## Requirements

### Requirement: Settlement and hunt share one event input port
Settlement and Hunt SHALL present narrative, choice, check, and result prompts through the shared `IPlayableEventInput` contract while retaining independent phase runners and execution environments.

#### Scenario: Either phase reaches an event node
- **WHEN** its ActionQueue awaits player input
- **THEN** the same world-space event presenter supplies the decision without directly mutating event state

### Requirement: Event decisions use world-space physical cards
The event presenter SHALL represent the primary narrative and every player decision as 3D cards anchored near the associated hunter or the active phase table.

#### Scenario: A choice event has several options
- **WHEN** the prompt opens
- **THEN** the narrative appears on a primary card and every option appears as a separate physical choice card titled with the action the player will take
- **AND** check type, target, and requirements remain readable without replacing the action label with a generic option number

### Requirement: Availability remains visible and authoritative
Unavailable options and hunters SHALL remain visible as disabled cards with a reason, and the presenter SHALL revalidate availability before returning a selection.

#### Scenario: No hunter satisfies an option
- **WHEN** the player reviews the event
- **THEN** that option cannot be clicked and explains why it is unavailable

### Requirement: Physical randomness remains between selection and check confirmation
The event presenter SHALL only return the selected option, actor, reroll decision, and confirmation; physical dice or future card randomness SHALL remain awaited by the owning ActionQueue before rule commit.

#### Scenario: A checked option is selected
- **WHEN** an actor has been chosen
- **THEN** the phase action requests the tabletop random presenter before opening the check-result cards

### Requirement: Input ownership is released on every exit path
The presenter SHALL block conflicting hunt-map input while a prompt is active and SHALL release that ownership after completion, cancellation, or destruction.

#### Scenario: A phase session is cancelled while cards are open
- **WHEN** the prompt cancellation token is triggered
- **THEN** the cards close, the input guard is released, and no event result is committed by the View

### Requirement: Malformed option content fails visibly
Null or unavailable option entries SHALL NOT crash presentation and SHALL NOT become selectable.

#### Scenario: Imported event data contains a missing option entry
- **WHEN** the choice prompt is built
- **THEN** a disabled invalid-data card is shown and the remaining valid choices stay operable
