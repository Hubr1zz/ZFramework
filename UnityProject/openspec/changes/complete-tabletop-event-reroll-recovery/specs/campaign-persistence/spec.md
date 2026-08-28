---
schemaVersion: 2
category: feature
title: 战役持久化与恢复
---

## ADDED Requirements

### Requirement: Continue does not duplicate a committed Settlement reroll

Settlement persistence SHALL write the actor cost and reroll continuation before publishing the reroll save boundary. Continue SHALL restore both as one authoritative checkpoint and SHALL keep the Timeline or child occurrence pending until the final event resolution commits.

#### Scenario: The process exits after Settlement reroll payment

- **WHEN** the player has paid for a reroll but has not accepted the rerolled result before the saved campaign is loaded
- **THEN** Continue SHALL preserve the paid Willpower and gained Fate, resume the stored rerolled result, and keep the event incomplete
- **AND** completing the event SHALL record one result memory with `WasRerolled=true` and SHALL NOT repeat the payment
