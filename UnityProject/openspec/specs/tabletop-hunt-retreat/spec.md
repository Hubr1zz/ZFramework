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

### Requirement: Retreat location creates a visible cargo decision
Returning from the configured camp position SHALL preserve every carried material. Confirming an emergency retreat away from camp SHALL require the player to select one currently carried stable resource ID and SHALL omit exactly one unit of that resource from the prepared return snapshot. An empty-handed squad MAY retreat without a selection.

#### Scenario: The squad returns to camp before ending the hunt
- **WHEN** the return layout opens at the camp position
- **THEN** the physical confirmation card states that every carried material will be settled
- **AND** the prepared return record retains the complete cargo snapshot

#### Scenario: The squad retreats away from camp with cargo
- **WHEN** the return layout opens away from camp
- **THEN** each aggregated carried resource is represented by a physical selection card
- **AND** the confirmation card remains unavailable until one current resource is selected for abandonment
- **AND** a successful preparation removes exactly one matching unit from the return record without mutating the live hunter collectibles

#### Scenario: The squad retreats away from camp empty-handed
- **WHEN** no active hunter carries a positive resource count
- **THEN** the emergency retreat can be confirmed without an abandonment selection

### Requirement: Retreat confirmation owns Hunt input
While the return confirmation is open, the View SHALL hold the Hunt input guard and SHALL release it after cancellation, successful phase exit, disable, or destruction.

#### Scenario: The confirmation layout is open
- **WHEN** the player clicks a tile or resource marker
- **THEN** the map command is ignored until the return confirmation is resolved

### Requirement: Hunt runner prepares the completion snapshot
The active Hunt ActionQueue SHALL prepare the year, deployed count, loss count, and collected-resource snapshot before any Campaign transition or resource transfer occurs, and reactors SHALL be able to prevent that preparation.

The Hunt runner SHALL re-read the current squad position and live cargo when validating an abandonment decision. A missing, forged, stale, or camp-only selection SHALL fail without changing live cargo or publishing a prepared fact.

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
The orchestration boundary SHALL request Hunt-to-Settlement through the Campaign ActionQueue and SHALL durably checkpoint the prepared stable return before releasing Hunt. Resource transfer and hunter advancement SHALL occur only in the Settlement return root.

#### Scenario: Campaign transition is rejected
- **WHEN** Hunt preparation succeeds but the Campaign transition does not commit
- **THEN** the game remains in Hunt and SHALL either durably remove the unused checkpoint before exploration resumes or retain a locked checkpoint that only permits an idempotent transition retry
- **AND** retry SHALL reuse or recreate the same one-unit filtered snapshot without accumulating another abandonment loss

#### Scenario: Campaign transition succeeds
- **WHEN** both Runner operations commit
- **THEN** Hunt is disposed only after its complete return record is durable, and the Settlement root transfers recorded collectibles and advances surviving participants exactly once

### Requirement: Return cards preview configured calendar consequences
The Campaign orchestration boundary SHALL combine the current Hunt cargo preview with a read-only calendar preview produced from the frozen campaign calendar, current year, and current season. The 3D confirmation card SHALL name the exact target year and season before accepting the return.

The preview SHALL distinguish a same-year season advance from a year boundary. It SHALL state that annual-event settlement opens only at a year boundary and SHALL describe that settlement as optional rather than claiming an event exists. Preview generation SHALL NOT materialize events, mutate campaign state, or enter an ActionQueue.

If the configured calendar cannot produce a valid advance plan, the confirmation card SHALL display the reason and SHALL disable return confirmation. The authoritative Settlement root SHALL still recompute and validate the calendar advance when it consumes the stable completion record.

#### Scenario: Return advances to another configured season in the same year
- **WHEN** the current season is not the final season in the frozen campaign calendar
- **THEN** the physical return card names the next configured season in the current year
- **AND** states that no new annual event is created

#### Scenario: Return crosses the configured year boundary
- **WHEN** the current season is the final season in the frozen campaign calendar
- **THEN** the physical return card names the first configured season in year N+1
- **AND** states that annual-event settlement will run if applicable

#### Scenario: Calendar preview is unavailable
- **WHEN** the calendar is missing or invalid for the current saved position
- **THEN** the return card fails closed with a visible reason and cannot submit a return

### Requirement: Every accepted return advances exactly one configured season
The Settlement ActionQueue SHALL consume each stable completion record at most once, advance the campaign to the next configured season, and materialize annual Timeline entries only when that advance enters year N+1. The number of complete Hunt returns per year SHALL derive from the frozen calendar rather than a hardcoded quota.

#### Scenario: A new completion record reaches Settlement
- **WHEN** the pending record has a stable ID that is absent from HuntHistory
- **THEN** one Settlement root appends the record and advances exactly one configured season
- **AND** creates annual Timeline work only if the configured advance crosses into a new year

#### Scenario: The same completion record is replayed
- **WHEN** transition retry or load recovery submits an already-applied stable record ID
- **THEN** Settlement reports an idempotent success without advancing the year, duplicating HuntHistory, or drawing another annual event

#### Scenario: Annual event materialization is interrupted
- **WHEN** a year-boundary return finds that an annual Timeline slot already exists for the target year but the return record is still pending
- **THEN** retry reuses that slot and completes the year transition without drawing a replacement

### Requirement: Duplicate return requests are serialized
The orchestration boundary SHALL reject duplicate return requests while a return handoff is already in progress or another Hunt action chain is running.

#### Scenario: Player confirms repeatedly
- **WHEN** the first return request is still awaiting completion
- **THEN** no second Hunt snapshot or Campaign transition is started
