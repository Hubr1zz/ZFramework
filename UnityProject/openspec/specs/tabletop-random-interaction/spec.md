---
schemaVersion: 2
category: feature
title: 桌游式随机交互
---

# Tabletop Random Interaction Specification

## Purpose

定义营地、狩猎等阶段通过 ActionQueue 等待桌面随机表现并提交权威规则结果的统一边界，使物理骰子、抽卡、翻牌与抽鬼牌能够复用同一请求协议，同时保持 GameCore 与 View 解耦。

## Requirements

### Requirement: Random presentation is awaited inside the owning action runner
Each phase SHALL request tabletop randomness from its own ActionQueue execution environment and SHALL await presentation before committing the related rule transition.

#### Scenario: An event requires a random check
- **WHEN** a settlement or hunt event reaches a checked choice
- **THEN** the phase action waits for the tabletop presenter result before the event transaction commits

### Requirement: Physical dice use validated authoritative results
The physical dice presenter SHALL spawn dice at the configured actor or target anchor, wait until their rigid bodies are stable, and return a result matching the request interaction id, count, and side range.

In Hunt, an actor-bound request SHALL resolve the corresponding hunter card on the 3D squad status board before falling back to the aggregate squad pawn. The dice tray SHALL apply a serialized world-space offset so it lands beside, rather than on top of, the referenced physical card.

#### Scenario: A Hunt event checks the selected hunter
- **WHEN** the event ActionQueue requests a physical roll with that hunter's actor id
- **THEN** the dice tray appears beside the matching Hunt status card
- **AND** the squad pawn is used only when no live actor card can be resolved

#### Scenario: A d10 event check completes
- **WHEN** one physical d10 becomes stable at the event anchor
- **THEN** the validated face value is supplied to the event transaction as its prepared roll

#### Scenario: Presentation returns malformed data
- **WHEN** the interaction id, dice count, value range, or total is invalid
- **THEN** the action fails without committing the event result

### Requirement: Rerolls preserve cost and cancellation boundaries
The event flow SHALL present a reroll only when the actor can pay its rule cost, and SHALL abort the action without committing when the presentation is cancelled.

#### Scenario: Actor cannot afford a reroll
- **WHEN** the player requests a reroll but the actor cannot pay its cost
- **THEN** no second dice presentation is started and the existing roll remains authoritative

### Requirement: Card interactions share an extensible presentation port
DrawCards, FlipCards, and OldMaid interactions SHALL use the same request and result presenter contract without placing event rules in the View layer. Card requests SHALL carry a stable deck ID, selected-card count, bounded value range, and player-facing instruction; results SHALL return the same number of unique stable card IDs and bounded values.

#### Scenario: An event uses cards instead of dice
- **WHEN** its action requests a supported card interaction kind
- **THEN** the shared router dispatches to the 3D card presenter, the player completes the requested physical interaction, and the owning phase runner validates the result before opening the check result card

#### Scenario: Player flips a bone omen
- **WHEN** the configured event requests one `FlipCards` result from the `bone-omens` deck
- **THEN** ten face-down 3D cards are presented, exactly one distinct card can be selected, and its bounded value becomes the event check roll

#### Scenario: Player faces a simplified Old Maid draw
- **WHEN** an `OldMaid` request is presented
- **THEN** one indistinguishable face-down card is selected from a hand containing one stable `old-maid` identity, and the result reveals whether that card was drawn without deciding consequences in the View

### Requirement: Random presentation kinds are routed without phase coupling
The runtime SHALL route physical dice and physical card requests through dedicated presenters while serializing their shared tabletop ownership. Settlement and Hunt SHALL receive the router through their existing phase execution environments.

#### Scenario: Two random interactions overlap
- **WHEN** another phase-owned action requests tabletop randomness while one presenter is active
- **THEN** the second request waits and no dice tray and card mat can accept input simultaneously

### Requirement: Headless execution remains deterministic
When no tabletop presenter is installed, the phase flow SHALL retain its injectable random source so EditMode tests and minimal headless gameplay can complete without Unity presentation objects.

#### Scenario: Event flow runs in an EditMode test
- **WHEN** the action session has no tabletop presenter
- **THEN** it resolves through the injected random source while preserving the same transaction and commit checkpoints
