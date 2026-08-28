---
schemaVersion: 2
category: feature
title: "战役持久化与恢复"
---

## MODIFIED Requirements

### Requirement: Legacy hunt quota progress migrates once
Campaign loading SHALL treat `HuntsCompletedThisYear` and `HuntsPerYear` only as schema-versioned migration input for the frozen campaign calendar. Schema 1 SHALL preserve CurrentYear and begin at the first configured season. Schema 0 MAY map a valid completed count to CurrentSeasonIndex only when the legacy quota exactly equals the selected calendar's season count. Invalid or mismatched progress SHALL preserve CurrentYear, normalize to the first season, and retain a diagnostic. Migration SHALL NOT create Timeline occurrences or infer additional elapsed years.

#### Scenario: A compatible legacy save contains one completed hunt
- **WHEN** schema 0 has completed 1 of 2 and the selected frozen calendar contains exactly 2 seasons
- **THEN** migration keeps CurrentYear and maps CurrentSeasonIndex to 1
- **AND** it does not create annual events or advance the year

#### Scenario: Legacy progress is incompatible with the frozen calendar
- **WHEN** the legacy quota differs from the selected calendar's season count, or the completed count is outside its valid range
- **THEN** migration keeps CurrentYear, normalizes CurrentSeasonIndex to 0, and preserves an actionable diagnostic

#### Scenario: A schema 1 save is migrated
- **WHEN** the save predates explicit CalendarId and CurrentSeasonIndex but no longer has authoritative quota semantics
- **THEN** migration binds the default frozen calendar, preserves CurrentYear, and starts from season index 0
