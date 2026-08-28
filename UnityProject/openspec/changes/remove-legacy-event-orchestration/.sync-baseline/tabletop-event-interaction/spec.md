---
schemaVersion: 2
category: feature
title: "跨阶段桌面事件交互"
---

# Tabletop Event Interaction Specification

## Purpose

让营地与狩猎事件复用同一套世界空间实体卡交互，同时保持阶段 ActionQueue 对事件选择、物理随机表现、规则提交和事件链推进的唯一控制权。

正式组合根独立安装世界空间事件输入端口；旧屏幕 HUD 的可见性不决定事件端口是否存在。

## Requirements

### Requirement: Settlement and hunt share one event input port
Settlement and Hunt SHALL present narrative, choice, check, and result prompts through the shared `IPlayableEventInput` contract while retaining independent phase runners and execution environments.

The playable bootstrap SHALL install the world-space event input port independently of legacy settlement HUD visibility.

#### Scenario: Either phase reaches an event node
- **WHEN** its ActionQueue awaits player input
- **THEN** the same world-space event presenter supplies the decision without directly mutating event state

#### Scenario: Legacy HUD visibility is disabled
- **WHEN** the playable bootstrap starts with legacy settlement HUD visibility disabled
- **THEN** settlement and hunt event prompts still have the world-space event input port
- **AND** the event presenter remains the only View-side input path

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

### Requirement: Accumulated Fate changes future event choices
Event option conditions SHALL support inclusive minimum and maximum Fate thresholds against the selected hunter's authoritative persisted state. Thresholds SHALL be table-configurable, player-readable as Fate requirements, and revalidated by the event transaction rather than the View.

#### Scenario: A low-Fate hunter approaches a Fate-sensitive event
- **WHEN** the hunter's Fate is at or below the configured safe threshold
- **THEN** the safe option is available and the high-risk Fate option remains disabled with its minimum Fate requirement

#### Scenario: Repeated rerolls have raised a hunter's Fate
- **WHEN** the hunter reaches the configured minimum Fate threshold
- **THEN** the safe option is disabled and the high-risk option becomes available through the existing ActionQueue event flow

### Requirement: Table-driven resource rewards use stable item identity
Every table-driven `AddResource` effect shared by Settlement or Hunt SHALL reference a registered Resource item's stable ContentId. Display names SHALL remain presentation text and MAY be accepted only by legacy migration boundaries.

#### Scenario: A localized resource name changes
- **WHEN** an event grants that resource after its player-facing name has changed
- **THEN** the same stable ContentId is committed to Settlement inventory or Hunt collectibles without changing the table reference

#### Scenario: Event tables are validated as one content generation
- **WHEN** Settlement, Hunt, and card-interaction event sources have been merged
- **THEN** every configured resource reward resolves to a registered item and its stored target equals that item's ContentId

### Requirement: Physical randomness remains between selection and check confirmation
The event presenter SHALL only return the selected option, actor, reroll decision, and confirmation. Event table content SHALL select physical dice, draw cards, flip cards, or Old Maid through configuration; the owning ActionQueue SHALL map that configuration to a tabletop request, await a validated result, and only then prepare or reroll the rule transaction.

#### Scenario: A checked option is selected
- **WHEN** an actor has been chosen
- **THEN** the phase action requests the tabletop random presenter before opening the check-result cards

#### Scenario: Card-based check is rerolled
- **WHEN** the player spends Willpower to retry a card-based check
- **THEN** the owning ActionQueue requests the same configured deck interaction again, retains the higher rule result, and the 3D event cards describe the action as drawing rather than throwing dice

### Requirement: Input ownership is released on every exit path
The presenter SHALL block conflicting Hunt commands and the pre-existing physical tabletop colliders in either phase while a prompt is active. Event cards created after that background lease SHALL remain interactive. The presenter SHALL release both forms of ownership after completion, cancellation, or destruction.

#### Scenario: A settlement event is awaiting a choice
- **WHEN** the event presenter opens its physical cards over the settlement table
- **THEN** existing hunter, resource, workshop, invention, and departure colliders stop receiving pointer input
- **AND** the event choice cards remain interactive

#### Scenario: A phase session is cancelled while cards are open
- **WHEN** the prompt cancellation token is triggered
- **THEN** the cards close, background colliders return to their prior enabled state, the Hunt command guard is released, and no event result is committed by the View

### Requirement: Malformed option content fails visibly
Null or unavailable option entries SHALL NOT crash presentation and SHALL NOT become selectable.

#### Scenario: Imported event data contains a missing option entry
- **WHEN** the choice prompt is built
- **THEN** a disabled invalid-data card is shown and the remaining valid choices stay operable
