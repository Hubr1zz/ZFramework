---
schemaVersion: 2
category: feature
title: 营地桌面发明解锁
---

# Tabletop Settlement Invention Specification

## Purpose

定义玩家通过营地 3D 发明卡查看条件并掌握发明的完整流程，确保卡牌只表达意图，规则校验、资源消费、效果应用、事件与存档边界统一归属 Settlement ActionQueue。

## Requirements

### Requirement: Inventions are interactive 3D cards
The settlement table SHALL represent configured inventions as three-dimensional cards and SHALL visibly distinguish mastered, currently available, and unavailable states.

#### Scenario: Player selects an unmastered invention
- **WHEN** the player activates its card
- **THEN** a world-space confirmation board shows description, effect, configured costs, and the current availability result

### Requirement: The View does not own invention state
The invention card and confirmation board SHALL NOT spend resources, unlock content, or apply effects directly; they SHALL submit at most one command while a request is pending.

#### Scenario: Player confirms twice rapidly
- **WHEN** a first invention request is still pending
- **THEN** the board ignores the duplicate activation and leaves authority with the Settlement runner

### Requirement: Unlocking is serialized by the Settlement runner
Authoritative unlocking SHALL execute as a root action in the active Settlement ActionQueue and SHALL revalidate content registration, prerequisites, exclusions, aggregate resource costs, and prior unlock state at execution time.

#### Scenario: Two requests compete for the same invention
- **WHEN** both requests enter one Settlement runner
- **THEN** exactly one request may consume resources and unlock the invention

#### Scenario: A reactor prevents invention
- **WHEN** a BeforeExecution reactor prevents the unlock action
- **THEN** resources, invention state, effects, and committed events remain unchanged

### Requirement: Successful unlocking publishes committed facts
A successful unlock SHALL publish affected resource changes, one invention-unlocked fact, and one Invention transaction commit after authoritative state has changed.

#### Scenario: Invention succeeds
- **WHEN** all current rules pass and the action commits
- **THEN** resource cards and invention visuals refresh and the settlement save boundary observes the transaction commit

### Requirement: Invention content remains configuration-driven
Invention identity, prerequisites, exclusions, costs, presentation, and future structured effects SHALL remain supplied by content data without changing the View command contract.

#### Scenario: A configured invention is added
- **WHEN** the settlement content catalog contains the new node
- **THEN** the 3D invention zone can present and unlock it without adding a new View type
