---
schemaVersion: 2
category: feature
title: "狩猎地块揭示事件"
---

# Hunt Tile Reveal Events Specification

## Purpose

让带有专属规则的 3D 地块在首次翻开时通过 Hunt ActionQueue 结算语义匹配的事件，同时保留普通地块的噪音风险牌堆入口，并在事件结束后开放后续探索。

## Requirements

### Requirement: Reveal selects exactly one event source
An unrevealed ordinary tile with a configured reveal event SHALL resolve that event instead of the generic noise draw. A tile without a configured event SHALL remain eligible for the noise risk deck. The starting tile and boss encounter SHALL NOT add an ordinary reveal event.

#### Scenario: A configured landmark is revealed
- **WHEN** the player reveals Mushroom Forest
- **THEN** the matching stable-ID Hunt event is selected exactly once
- **AND** no generic noise-card request is created for that reveal

#### Scenario: An ordinary fallback tile is revealed
- **WHEN** the player reveals Statue Plains, Shallow Swamp, or Broken Ruins
- **THEN** no configured reveal event is selected
- **AND** the existing noise risk deck remains the event source

### Requirement: Reveal preserves tabletop causal order
The Hunt runner SHALL commit the reveal, await the existing 3D tile presentation, resolve the selected event through the shared tabletop interaction ports, and only then unlock neighboring tiles.

#### Scenario: A tile event awaits player input
- **WHEN** its physical dice or card interaction is still pending
- **THEN** the tile is visibly revealed
- **AND** its locked neighbors remain unavailable until the event root finishes

### Requirement: Reveal content uses stable identities
Configured events SHALL expose explicit unique IDs, be Hunt-category content eligible in year one, and reference resource rewards by registered item ID. Hunt rewards SHALL remain in expedition collectibles until the formal return boundary.

#### Scenario: A reveal event awards a resource
- **WHEN** its option effect commits
- **THEN** the effect resolves the item by stable ID
- **AND** the reward does not mutate Settlement inventory directly

### Requirement: Post-commit failures do not permanently block exploration
After the reveal checkpoint has committed, a presentation-adapter failure SHALL NOT roll back authoritative map state or leave neighboring tiles permanently locked. This capability does not claim durable resumption of a partially committed Hunt event chain; that remains a separate shared event-chain checkpoint concern.

#### Scenario: Presentation fails after reveal commit
- **WHEN** the presentation adapter reports an exception
- **THEN** the committed tile remains revealed
- **AND** the root reaches a safe finalized map state without duplicating the reveal
