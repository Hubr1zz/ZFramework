---
schemaVersion: 2
category: feature
title: "事件普通伤势与营地休养闭环"
---

# Event Recoverable Wounds Specification

## Purpose

让非决战的营地与狩猎事件能够通过表配置施加明确部位的普通伤势，并复用既有 3D 营地休养流程；事件 ActionQueue 是伤势状态与提交事实的唯一玩法权威。

## Requirements

### Requirement: Event wounds are explicit and table-driven
Each configured recoverable wound SHALL identify the selected hunter, a positive damage value, and one stable body-part ID from `head`, `torso`, `arms`, or `legs`; configuring such a wound remains optional for an event option.

#### Scenario: Valid wound content is loaded
- **WHEN** a choice effect declares `AddRecoverableWound`, `targetName=selected`, positive damage, and a supported body part
- **THEN** the shared event table exposes the effect to Settlement or Hunt

#### Scenario: Wound content is malformed
- **WHEN** the actor, damage, or body part is missing or invalid, or the effect is configured as an immediate table effect without an explicit choice actor
- **THEN** table validation rejects the event rather than guessing a target

### Requirement: Phase ActionQueues own wound mutation and facts
Settlement and Hunt SHALL resolve the same wound effect inside their phase event action root. The View SHALL NOT directly change hunter health or publish gameplay wound facts.

#### Scenario: A wound effect commits
- **WHEN** the event action resolves for a hunter belonging to the current settlement
- **THEN** only the configured body part loses health and `HunterWoundedEvent` is published at the resolution checkpoint before the phase transaction fact

#### Scenario: An event action is prevented
- **WHEN** a Reactor prevents the event node before execution
- **THEN** hunter health and wound facts remain unchanged

### Requirement: Recoverable wounds do not enter death resolution
Non-showdown event wounds SHALL clamp the configured body part to at least one health and SHALL reject a part already at zero.

#### Scenario: Damage exceeds remaining ordinary health
- **WHEN** a valid hunter with positive part health receives excessive recoverable damage
- **THEN** that part ends at one health and no death-card or permanent-injury flow starts

### Requirement: Wounds persist into camp recovery
Ordinary body-part health SHALL survive campaign and active-Hunt save round trips and remain compatible with the existing world-space Settlement recovery command.

#### Scenario: A wounded expedition is restored
- **WHEN** a Hunt snapshot containing reduced body-part health is saved and restored
- **THEN** the restored hunter retains that health and the existing 3D recovery flow can treat it after returning to Settlement
