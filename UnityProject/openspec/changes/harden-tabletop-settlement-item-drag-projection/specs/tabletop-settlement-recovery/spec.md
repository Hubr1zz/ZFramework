## ADDED Requirements

### Requirement: Consumable cards use a transient treatment target

The hunter equipment board SHALL allow a configured consumable card to target the world-space treatment entry, but the treatment target SHALL NOT retain the card or mutate inventory. It SHALL restore the card to its storage slot before opening body-part selection, and the existing Settlement ActionQueue recovery command SHALL remain authoritative for final consumption and healing.

#### Scenario: A consumable card enters treatment selection

- **WHEN** the player drops a valid consumable card on the treatment target
- **THEN** the treatment target SHALL be empty and the storage projection SHALL remain intact while body-part selection is open

#### Scenario: Treatment commits

- **WHEN** the player selects a recoverable body part and the Settlement recovery Action succeeds
- **THEN** the consumable inventory and hunter wound state SHALL update from the authoritative commit and the 3D presentation SHALL refresh
