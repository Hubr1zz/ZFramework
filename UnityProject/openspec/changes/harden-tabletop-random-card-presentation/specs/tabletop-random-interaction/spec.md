---
schemaVersion: 2
category: feature
title: 桌游式随机交互
---

## ADDED Requirements

### Requirement: Physical random cards use Cards3D pointer semantics

The production DrawCards, FlipCards, and OldMaid presenter SHALL represent every selectable card with a `CardView3D`-derived physical view. Selection SHALL require a pointer press and release that remains within the configured movement threshold; a gesture exceeding that threshold SHALL NOT select a non-draggable random card.

#### Scenario: Player moves the pointer while inspecting a face-down card

- **WHEN** the pointer moves beyond the card drag threshold before release
- **THEN** the card SHALL remain face down and the interaction SHALL continue waiting for a deliberate short click

#### Scenario: Player draws from a stacked deck

- **WHEN** a `DrawCards` request needs another card
- **THEN** only the current unselected deck-top card SHALL accept input
- **AND** a revealed card SHALL NOT become selectable again

### Requirement: Random card presentation releases transient ownership on every exit

The card presenter SHALL release its temporary card views, owned runtime materials, selection source and background input lease after completion, cancellation, disable, or destruction. A stale card callback SHALL NOT complete a later request, and the same presenter SHALL be reusable after a cancelled interaction.

#### Scenario: The owning phase disables the presenter while waiting

- **WHEN** the presenter is disabled before a card is selected
- **THEN** the current wait SHALL cancel, all temporary card objects SHALL close, and pre-existing colliders SHALL return to their prior enabled state
- **AND** a later request after re-enable SHALL create and complete a new independent card interaction
