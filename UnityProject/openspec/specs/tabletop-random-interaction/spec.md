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
DrawCards, FlipCards, and OldMaid interactions SHALL be representable by the same request and result presenter contract without placing their future rules in the View layer.

#### Scenario: A future event uses cards instead of dice
- **WHEN** its action requests a supported card interaction kind
- **THEN** the phase runner can await a presenter implementation without changing the event transaction boundary

### Requirement: Headless execution remains deterministic
When no tabletop presenter is installed, the phase flow SHALL retain its injectable random source so EditMode tests and minimal headless gameplay can complete without Unity presentation objects.

#### Scenario: Event flow runs in an EditMode test
- **WHEN** the action session has no tabletop presenter
- **THEN** it resolves through the injected random source while preserving the same transaction and commit checkpoints
