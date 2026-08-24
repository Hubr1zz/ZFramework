---
schemaVersion: 2
category: feature
title: "狩猎噪音风险牌堆"
---

# Hunt Noise Risk Deck Specification

## Purpose

把狩猎探索中的隐藏固定概率替换为可预览、可配置并由 Hunt ActionQueue 结算的实体风险牌堆，使队伍规模和装备噪音真实影响普通地块揭示，同时为未来规则覆盖与读表内容保留稳定接口。

## Requirements

### Requirement: Hunt noise produces a bounded risk deck
Each playable route SHALL define a noise deck. The effective noise score SHALL combine living hunter count, equipped-item noise, and additive Reactor modifiers, while the danger-card count remains within the configured deck and danger limits.

#### Scenario: A noisy party prepares to reveal an ordinary tile
- **WHEN** the active Hunt runner evaluates the party before tile commit
- **THEN** it creates a bounded risk plan from the current living party and equipment
- **AND** the physical request displays the effective danger-card count and deck size

#### Scenario: Multiple effects modify noise
- **WHEN** more than one Reactor contributes a noise modifier
- **THEN** the modifiers compose additively and the final result remains bounded

### Requirement: Playable routes have valid year-eligible danger content
A destination SHALL NOT be available unless its route content has an enabled noise profile and at least one unique, stable-ID danger event eligible for the current year.

#### Scenario: Route danger content is missing or out of year range
- **WHEN** the player attempts to select that destination
- **THEN** departure is rejected before the Hunt session starts
- **AND** no silently risk-free fallback route is created

### Requirement: Ordinary reveal awaits a physical card before commit
An ordinary first reveal SHALL draw one card through the shared tabletop presenter before authoritative map mutation. Cancellation, malformed presentation data, or unavailable danger content SHALL fail without revealing the tile.

#### Scenario: The player cancels the noise draw
- **WHEN** the physical card interaction is cancelled before selection
- **THEN** the target tile remains unrevealed and the interaction can be attempted again

#### Scenario: A safe card is selected
- **WHEN** the validated card value is outside the danger range
- **THEN** the Hunt runner commits the tile reveal and records a safe noise resolution

#### Scenario: A danger card is selected
- **WHEN** the validated card value is inside the danger range
- **THEN** the runner commits the tile reveal and resolves one weighted, year-eligible Hunt event from the frozen danger pool

### Requirement: Explicit tile semantics do not double-trigger generic risk
Boss tiles and tiles carrying an explicit reveal event SHALL bypass the generic noise draw. Movement onto an already revealed tile SHALL NOT trigger an unrelated hidden random event.

#### Scenario: A forced-event tile is revealed
- **WHEN** its explicit reveal event is available
- **THEN** only that explicit event enters the interaction chain

### Requirement: Hunt ActionQueue is the only player mutation path
Player tile clicks SHALL require the active Hunt action session. The reveal commit, noise resolution, weighted event selection, and EventBus outbox fact SHALL remain in one Hunt-runner-owned causal chain.

#### Scenario: No Hunt action session is installed
- **WHEN** a player-facing tile click reaches the legacy map manager
- **THEN** the click is rejected and no map state is mutated

#### Scenario: A noise result commits
- **WHEN** the tile interaction reaches its commit checkpoint
- **THEN** the map state and noise record are committed before the corresponding outbox fact is published

### Requirement: Preview distinguishes base risk from runtime effects
Departure and Hunt status projections SHALL identify their displayed estimate as base risk when Reactor modifiers have not yet executed. The physical draw prompt SHALL show the post-Reactor effective plan.

#### Scenario: A runtime effect changes noise
- **WHEN** the final risk differs from the departure estimate
- **THEN** the player sees the authoritative effective danger count before choosing a physical card
