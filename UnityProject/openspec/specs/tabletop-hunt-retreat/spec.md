---
schemaVersion: 2
category: feature
title: "3D 狩猎回营结算"
---

# Tabletop Hunt Retreat Specification

## Purpose

让玩家通过地图边缘的实体卡结束探索，并由 Hunt 与 Campaign ActionQueue 依次准备狩猎记录、接受阶段切换和提交回营结算。

## Requirements

### Requirement: Retreat is a world-space card flow
The normal Hunt retreat flow SHALL use a persistent 3D return card and a separate physical confirmation layout instead of a screen-space action button.

#### Scenario: Player inspects the return option
- **WHEN** no other Hunt interaction owns input and the player activates the return card
- **THEN** physical cards summarize deployed hunters, losses, and carried materials without ending the hunt

#### Scenario: Player continues exploring
- **WHEN** the player selects the physical continue card
- **THEN** the confirmation layout closes and the Hunt map remains unchanged and interactive

### Requirement: Retreat confirmation owns Hunt input
While the return confirmation is open, the View SHALL hold the Hunt input guard and SHALL release it after cancellation, successful phase exit, disable, or destruction.

#### Scenario: The confirmation layout is open
- **WHEN** the player clicks a tile or resource marker
- **THEN** the map command is ignored until the return confirmation is resolved

### Requirement: Hunt runner prepares the completion snapshot
The active Hunt ActionQueue SHALL prepare the year, deployed count, loss count, and collected-resource snapshot before any Campaign transition or resource transfer occurs, and reactors SHALL be able to prevent that preparation.

#### Scenario: A Hunt reactor prevents retreat
- **WHEN** a registered reactor rejects the retreat action
- **THEN** no prepared fact is published, no resource is transferred, and the player remains in Hunt

### Requirement: Unresolved harvests cannot be abandoned by retreat
Retreat preparation SHALL reject a harvest transaction that can still resolve, but SHALL discard cancelled or already committed transactions from the session guard.

#### Scenario: Material cards remain unresolved
- **WHEN** the player attempts to return before completing or safely cancelling the harvest
- **THEN** the return request fails and the harvest keeps input ownership

#### Scenario: A harvest was cancelled before its first reveal
- **WHEN** the player closes that resource interaction and then confirms return
- **THEN** the stale transaction does not prevent retreat

### Requirement: Campaign acceptance gates authoritative exit settlement
The orchestration boundary SHALL request Hunt-to-Settlement through the Campaign ActionQueue and SHALL transfer collectibles and apply hunter advancement only after the phase machine accepts that transition.

#### Scenario: Campaign transition is rejected
- **WHEN** Hunt preparation succeeds but the Campaign transition does not commit
- **THEN** the game remains in Hunt, collected items remain on the hunters, and the prepared completion record remains available for an idempotent transition retry

#### Scenario: Campaign transition succeeds
- **WHEN** both Runner operations commit
- **THEN** collectibles transfer exactly once, hunter advancement is applied, Hunt is disposed, and the completion record becomes a persisted pending Settlement handoff

### Requirement: Every accepted return advances exactly one year
The Settlement ActionQueue SHALL consume each stable completion record at most once, advance the campaign from year N to N+1, and materialize the annual Timeline entries for N+1 before publishing committed facts. Annual pacing SHALL NOT depend on a configurable hunts-per-year quota.

#### Scenario: A new completion record reaches Settlement
- **WHEN** the pending record has a stable ID that is absent from HuntHistory
- **THEN** one Settlement root appends the record, advances exactly one year, and creates at most one random annual Timeline slot for the new year

#### Scenario: The same completion record is replayed
- **WHEN** transition retry or load recovery submits an already-applied stable record ID
- **THEN** Settlement reports an idempotent success without advancing the year, duplicating HuntHistory, or drawing another annual event

#### Scenario: Annual event materialization is interrupted
- **WHEN** an annual Timeline slot already exists for the target year but the return record is still pending
- **THEN** retry reuses that slot and completes the year transition without drawing a replacement

### Requirement: Duplicate return requests are serialized
The orchestration boundary SHALL reject duplicate return requests while a return handoff is already in progress or another Hunt action chain is running.

#### Scenario: Player confirms repeatedly
- **WHEN** the first return request is still awaiting completion
- **THEN** no second Hunt snapshot or Campaign transition is started
