---
schemaVersion: 2
category: architecture
title: "跨阶段事件 occurrence 检查点核心"
---

# Event Occurrence Checkpoint Core Specification

## Purpose

为营地与狩猎事件链提供同一套有限、幂等的 occurrence 身份与提交语义，同时让各阶段继续拥有自己的 Runner、资源端口、恢复投影和持久化边界。

## Requirements

### Requirement: Occurrence identity is independent from content identity
Each scheduled event occurrence SHALL have a sequence identity independent from its stable EventId. Repeated sibling references to the same EventId SHALL remain distinct ordered occurrences, while committing the same sequence more than once SHALL NOT append its children again.

#### Scenario: Two sibling branches reference the same event
- **WHEN** a parent commits two ordered child references with the same stable EventId
- **THEN** the checkpoint creates two different positive sequence values
- **AND** consuming either occurrence leaves the other pending

### Requirement: Checkpoint growth is bounded and diagnosable
The shared occurrence queue SHALL reject blank child IDs, cap pending occurrences at the owning phase limit, preserve accepted order, and expose an actionable overflow diagnostic.

#### Scenario: A parent exceeds the pending limit
- **WHEN** more valid children are submitted than the configured limit permits
- **THEN** only the bounded prefix enters the checkpoint
- **AND** the owner can fail closed using the retained diagnostic

### Requirement: Stable IDs define ancestry cycles
Phase adapters SHALL compare explicit stable ContentId values when rejecting a direct ancestry cycle. Unity asset names and object references SHALL NOT override stable event identity.

#### Scenario: A different asset aliases an ancestor ContentId
- **WHEN** a child points to another EventData asset carrying an ancestor's explicit ContentId
- **THEN** the back edge is rejected before execution
- **AND** no pending occurrence remains for that cycle

### Requirement: Settlement persistence preserves ancestry across reloads
The Settlement adapter SHALL use SchemaVersion 2 checkpoint DTOs while preserving the existing JSON field names. Every pending occurrence SHALL persist the ordered stable ContentId path of its ancestors and map that path to the shared runtime contract without moving Timeline ownership or introducing an active-Hunt payload. A pending checkpoint from any other schema version SHALL fail closed and remain available for explicit migration or recovery.

#### Scenario: A schema-two checkpoint is serialized and loaded
- **WHEN** SettlementInstance completes a JsonUtility round trip
- **THEN** ChainId, Sequence, EventId, Year, ActorId, ancestor ContentId path, and pending order remain unchanged
- **AND** a restored child cannot replay an ancestor through a back edge

#### Scenario: A pending checkpoint uses an unsupported schema
- **WHEN** recovery encounters a schema older or newer than the current checkpoint contract
- **THEN** the runner SHALL NOT execute its pending occurrences
- **AND** the checkpoint remains intact for explicit migration or recovery

### Requirement: Shared core does not claim phase recovery
The shared core SHALL NOT own maps, Timeline, inventories, encounter transitions, Views, or save I/O. Hunt cross-process recovery SHALL require a complete active-Hunt snapshot before its occurrence ledger can become durable.

#### Scenario: A Hunt session is destroyed
- **WHEN** no active-Hunt snapshot exists
- **THEN** the occurrence core does not write Hunt work into Settlement pending chains
- **AND** the system does not claim that the Hunt can resume after process restart
