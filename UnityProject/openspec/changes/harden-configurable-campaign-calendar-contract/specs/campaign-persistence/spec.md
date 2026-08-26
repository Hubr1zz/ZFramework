---
schemaVersion: 2
category: feature
title: 战役持久化与恢复
---

## MODIFIED Requirements

### Requirement: Hunt return handoff survives process interruption

Campaign orchestration SHALL persist the complete stable PendingHuntReturn before leaving Hunt or applying resources and hunter advancement. Settlement SHALL save the applied authoritative state while the checkpoint remains present, then clear and save it; failed preparation, application, or persistence SHALL retain the last durable recovery boundary and gate departure.

#### Scenario: The process exits before Settlement applies the return

- **WHEN** the latest valid snapshot contains PendingHuntReturn but not its HuntHistory record
- **THEN** continue SHALL submit that record to the Settlement runner, apply recorded resources and surviving-participant advancement exactly once, and advance exactly one configured season
- **AND** only a real year boundary SHALL restore the resulting annual Timeline through the ordinary event projection

#### Scenario: The clear-checkpoint save is retried

- **WHEN** a stable pending record is already present in HuntHistory
- **THEN** recovery SHALL treat the complete return outcome as already applied, clear PendingHuntReturn, and persist that cleared state without adding resources, advancing hunters, advancing the calendar or drawing again

### Requirement: Legacy hunt quota progress migrates once

Campaign loading SHALL treat HuntsCompletedThisYear and HuntsPerYear only as schema-versioned migration input. Schema 0 MAY map valid completion progress to CurrentSeasonIndex only when the old quota equals the frozen calendar's season count; mismatched or invalid progress SHALL conservatively reset to the first season with a diagnostic. Schema 1 SHALL preserve CurrentYear and resume from the first season. Migration SHALL NOT advance a year or create Timeline occurrences.

#### Scenario: A valid schema-zero save matches the frozen calendar

- **WHEN** schema 0 has a valid completion count and its quota equals the selected calendar season count
- **THEN** migration MAY map that count to CurrentSeasonIndex, normalize legacy counters and mark the current pacing schema
- **AND** repeated catalog application SHALL NOT change the calendar again

#### Scenario: Legacy progress is ambiguous or invalid

- **WHEN** the quota differs from the frozen calendar, is non-positive, or completion count is outside its valid range
- **THEN** migration SHALL keep CurrentYear unchanged, select the first season, normalize obsolete counters and preserve an actionable diagnostic
