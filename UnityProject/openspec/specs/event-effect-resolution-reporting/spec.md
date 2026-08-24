---
schemaVersion: 2
category: feature
title: "事件效果结构化结算结果"
---

# Event Effect Resolution Reporting Specification

## Purpose

让营地与狩猎事件的每项效果都产生有序、可读取的结算结果，避免配置错误或业务拒绝只写入 Warning、而阶段 Root 仍无法说明哪些效果实际生效。

## Requirements

### Requirement: Every attempted event effect produces a result
The event resolver SHALL produce one ordered result for every attempted effect. Each result SHALL identify its source event, list index, configured effect type and target when present, whether it was applied, and a player-readable rejection reason when it was not applied.

#### Scenario: One effect in a sequence is rejected
- **WHEN** an event applies a valid effect, an invalid effect, and another valid effect in that order
- **THEN** the batch contains three results in the configured order
- **AND** the rejected result explains the failure without hiding the later applied effect

### Requirement: Phase command boundaries expose the aggregate
Settlement and Hunt ActionQueue roots SHALL expose the aggregated effect batch in their command result. A business rejection MAY coexist with a successful root action while rollback is unsupported, but the result SHALL expose the failed-effect count and SHALL NOT represent the batch as fully successful.

#### Scenario: A Hunt event cannot remove a staged resource
- **WHEN** the selected hunter does not carry the requested resource
- **THEN** the Hunt tile command preserves the existing stage state
- **AND** its result exposes one rejected effect instead of relying on a warning log

### Requirement: Recognized effects reject impossible inputs
Resource removal SHALL be rejected when the authoritative inventory cannot pay the amount. Hunter-targeted effects SHALL be rejected when their configured target resolves to no hunter. Neither case SHALL be reported as applied.

#### Scenario: A selected-hunter effect has no selected hunter
- **WHEN** a hunter-targeted effect resolves without a matching actor
- **THEN** no hunter state changes
- **AND** the result explains that the target was not found

### Requirement: Published batches are stable snapshots
An effect batch SHALL copy the ordered results supplied at construction. Later mutations to an execution accumulator SHALL NOT change the batch contents or make its applied and failed counts disagree with its effect list.

#### Scenario: A parent action continues collecting child results
- **WHEN** a child batch has already been assigned and another child resolves later
- **THEN** the earlier batch retains its original contents and counts
