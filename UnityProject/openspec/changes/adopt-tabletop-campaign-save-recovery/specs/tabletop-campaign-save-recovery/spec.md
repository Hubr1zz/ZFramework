---
schemaVersion: 2
category: feature
title: "桌面战役存档失败恢复"
---

## ADDED Requirements

### Requirement: The latest campaign save state is observable without changing transaction outcomes
Campaign persistence SHALL expose an immutable Idle, Saving, or Failed status with a monotonic request revision. Only the latest request in the current campaign generation SHALL update that visible status. A newer ordinary autosave SHALL NOT change the success result of an older critical save that actually completed, and a completion from an old campaign generation SHALL NOT change the current status.

#### Scenario: An ordinary autosave starts during a critical save
- **WHEN** a critical save succeeds after a newer ordinary autosave has started
- **THEN** the critical caller receives success from its own storage operation
- **AND** only the newer request may update the visible save status

#### Scenario: An old campaign save completes after reset
- **WHEN** Reset or Adopt has moved the coordinator to a new campaign generation
- **THEN** the old completion cannot publish Idle or Failed into the new campaign

### Requirement: Retry captures the latest authoritative campaign snapshot
A retry command SHALL capture Settlement or active Hunt state again at the time of retry and SHALL NOT replay the payload that originally failed. Concurrent retry requests SHALL share one active retry owner. Cancellation SHALL NOT turn an existing Failed state into Idle, while lifecycle cancellation of an ordinary save SHALL NOT be presented as a disk failure.

#### Scenario: Gameplay changes after a failed save
- **WHEN** save A fails and the authoritative campaign advances before the player retries
- **THEN** retry serializes the latest campaign state rather than payload A

#### Scenario: The player clicks retry repeatedly
- **WHEN** a retry is already in flight
- **THEN** additional retry requests reuse that operation and do not start another storage write

#### Scenario: A failed retry is cancelled by lifecycle shutdown
- **WHEN** the player-visible state was Failed before retry and the retry token is cancelled
- **THEN** the original failure remains visible and retryable until Reset, Adopt, or a later successful save

### Requirement: Save failure recovery uses a cross-phase 3D tabletop presentation
The normal Settlement and Hunt tables SHALL display the latest Failed status as a persistent world-space primary card with one retry card. Retry-in-progress SHALL remain visible and non-interactable; Idle SHALL hide the presentation. The presenter SHALL follow the current phase root and SHALL only read campaign status and submit the retry command. It SHALL NOT write persistence directly, publish gameplay facts, or enter an ActionQueue.

#### Scenario: A background save fails in Hunt
- **WHEN** the latest active-Hunt checkpoint save reports Failed
- **THEN** a retryable 3D save card appears under the Hunt presentation root

#### Scenario: Retry succeeds
- **WHEN** the player retries and the coordinator returns to Idle
- **THEN** the save card closes automatically without changing gameplay state

#### Scenario: The phase root changes while failure remains
- **WHEN** the campaign moves between Settlement and Hunt while the latest status is still Failed
- **THEN** the same presentation moves to the current phase root and remains retryable

### Requirement: Critical persistence gates remain authoritative
Return checkpoints, encounter handoff, restart compensation, and other critical transactions SHALL continue to await their own persistence result and enforce their existing failure gates. The player-visible retry status SHALL NOT clear a checkpoint, authorize a phase transition, or report a critical transaction as committed.

#### Scenario: Hunt return persistence fails
- **WHEN** a required Hunt return save is rejected
- **THEN** the existing pending return boundary remains authoritative
- **AND** the retry card does not bypass departure or checkpoint-clear gates
