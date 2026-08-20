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
A successful unlock SHALL publish affected resource changes, one invention-unlocked fact carrying stable ID and display name, and one stable-ID Invention transaction commit after authoritative state has changed. The same commit SHALL add one idempotent, completed stable-ID invention entry to the persistent campaign timeline.

#### Scenario: Invention succeeds
- **WHEN** all current rules pass and the action commits
- **THEN** resource cards, invention visuals, and the 3D annals refresh and the settlement save boundary observes the transaction commit

#### Scenario: An event grants an invention

- **WHEN** a committed event effect unlocks an invention without using the invention board
- **THEN** the same persistent annals projection SHALL record that invention exactly once

### Requirement: Invention content remains configuration-driven
Invention identity, prerequisites, exclusions, costs, presentation, and future structured effects SHALL remain supplied by content data without changing the View command contract. Persistent state and cross-content references SHALL use the explicit stable ContentId; display names SHALL remain presentation and legacy-import aliases only.

#### Scenario: A configured invention is added
- **WHEN** the settlement content catalog contains the new node
- **THEN** the 3D invention zone can present and unlock it without adding a new View type

#### Scenario: Display text changes after a release

- **WHEN** an invention is renamed or localized while retaining its ContentId
- **THEN** saved mastery, prerequisites, recipes, workshops, training, events, ActionQueue entities, transaction IDs, and annals continue to address the same invention

### Requirement: Legacy invention identity migrates before gameplay
After the invention catalog is registered, old unlocked flags and invention annals using a display name or prior asset name SHALL migrate idempotently to stable ContentId. Unknown external identifiers SHALL be preserved, and a save with a future identity schema SHALL NOT be downgraded or rewritten.

#### Scenario: Old and new keys coexist

- **WHEN** one legacy save contains both `武器训练` and `weapon_training`
- **THEN** migration merges them into one unlocked `weapon_training` flag and one stable annals entry without losing a true value

#### Scenario: A newer save is opened by an older build

- **WHEN** its invention identity schema exceeds the supported version
- **THEN** the build leaves its invention flags and schema untouched

### Requirement: Ambiguous and unknown invention references fail closed
The settlement catalog SHALL reject invention identities whose stable IDs, display aliases, or legacy asset aliases collide. An event SHALL NOT unlock or persist an invention that cannot be resolved through the registered catalog.

#### Scenario: An event references a missing invention

- **WHEN** a committed event effect requests an unknown invention ContentId
- **THEN** no unlocked flag, annals entry, or successful invention fact is produced
