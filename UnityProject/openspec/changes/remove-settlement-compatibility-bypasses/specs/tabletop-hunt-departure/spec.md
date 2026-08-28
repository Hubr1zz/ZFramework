---
schemaVersion: 2
category: feature
title: "3D 狩猎远征整备兼容边界"
---

# 3D 狩猎远征整备 Delta

## MODIFIED Requirements

### Requirement: Every departure entry uses the authoritative command path

The current player departure entry SHALL be SettlementTable3D's world-space squad callback followed by the registered `IPlayableHuntDepartureInput`/destination View and typed Campaign departure transaction. The removed `ISettlementDepartureRequestPort`, SettlementManager.TryDepart and GameManager legacy departure methods SHALL NOT be part of the active contract.

#### Scenario: Player confirms a staged squad

- **WHEN** the player activates the 3D departure launcher
- **THEN** the staged squad reaches the destination View and typed Campaign transaction
- **AND** SettlementManager does not own a compatibility departure port or mutate the roster directly

### Requirement: Concurrent settlement flows do not overlap

The formal 3D callback SHALL preserve the existing pending-return notice, deduplication and in-flight gate. Removing compatibility APIs SHALL NOT remove the blocked notice or allow a second departure handoff.

#### Scenario: A previous return save is pending

- **WHEN** the 3D departure callback is submitted twice while the previous return is pending
- **THEN** one "暂不能出猎" notice is presented
- **AND** after the pending save completes, a typed destination departure can succeed
