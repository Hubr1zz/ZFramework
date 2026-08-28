---
schemaVersion: 2
category: system
title: 跨阶段事件 occurrence 检查点核心
---

## ADDED Requirements

### Requirement: Pending occurrences carry validated reroll continuation state

The shared occurrence contract SHALL support an optional versioned reroll continuation containing only stable EventId, OptionId, ActorId, the validated rerolled value and frozen check bonus. Phase adapters SHALL reject a continuation whose identity does not match its occurrence, whose option or actor cannot be resolved, whose roll is outside the configured check range, or whose version is unsupported.

#### Scenario: A reroll continuation survives an occurrence round trip

- **WHEN** a pending occurrence with a valid reroll continuation is captured and restored
- **THEN** all continuation fields SHALL remain unchanged and refer to the same occurrence, option and actor

#### Scenario: A continuation does not match current content

- **WHEN** a saved reroll continuation references a different event, missing option, missing actor or invalid roll
- **THEN** the owning phase SHALL fail closed, retain the pending occurrence, and expose a diagnostic instead of reselecting or rerolling
