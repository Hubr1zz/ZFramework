---
schemaVersion: 2
category: feature
title: "读表事件分支与事件链"
---

# Table-driven Event Chains Specification

## Purpose

让大量营地与狩猎事件通过稳定事件 ID 配置直接后续、成功分支和失败分支，并复用已有跨阶段事件 ActionQueue，而不在代码中硬连 `ScriptableObject`。

## Requirements

### Requirement: Tables express every direct event branch by stable ID
An event table record SHALL support ordered event-level, option-success, and option-failure chain ID lists. Direct-chain targets SHALL use the `Triggered` category, and runtime `EventData` references SHALL be resolved only after all configured event sources have been merged.

#### Scenario: A branch targets an event in the merged catalog
- **WHEN** table loading completes
- **THEN** each stable ID resolves to the shared runtime event instance
- **AND** configured order is preserved

### Requirement: Invalid references fail closed before play
An event record whose scheduled or direct-chain target is blank, duplicated, missing, or otherwise rejected SHALL NOT enter a playable pool. Records depending on that rejected record SHALL also be rejected.

#### Scenario: A valid-looking event points at a malformed child
- **WHEN** the merged catalog is validated
- **THEN** both the malformed child and every transitively dependent source are excluded
- **AND** an actionable content-table error identifies the broken reference

### Requirement: Existing phase runners remain authoritative
Resolved table chains SHALL run through the existing Settlement or Hunt event root. Each runtime event instance SHALL be scheduled at most once in one causal chain, including cyclic or converging table references.

#### Scenario: Two branches converge on the same child event
- **WHEN** the owning ActionQueue collects subsequent nodes
- **THEN** the child resolves no more than once in that root
- **AND** the View continues to provide input without mutating event state

### Requirement: Direct chains and delayed events remain distinct
Direct chain IDs SHALL enqueue the next node in the current event root. `ScheduleEvent` SHALL continue to write a future timeline entry and SHALL require a `Scheduled` target.

#### Scenario: One result contains both a direct child and a delayed consequence
- **WHEN** the result commits
- **THEN** the direct child continues in the current phase ActionQueue
- **AND** the delayed consequence appears only when its due campaign year is reached
