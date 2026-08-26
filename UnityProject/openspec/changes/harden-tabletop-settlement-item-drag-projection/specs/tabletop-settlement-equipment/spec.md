## ADDED Requirements

### Requirement: Equipment projections preserve slot ownership

Every visible stored or equipped item card SHALL be placed through its `CardSlot`, so the slot occupant and card current slot reference each other. Refresh or page replacement SHALL clear this relationship before destroying the old projection.

#### Scenario: The equipment board rebuilds

- **WHEN** authoritative storage or equipment state changes and the board rebuilds
- **THEN** every visible card SHALL have exactly one matching slot and no cleared slot SHALL retain a destroyed occupant

### Requirement: Equipment command pending survives presentation changes

An equipment or unequipment gameplay command SHALL remain the panel's active command until its matching ActionQueue completion, independently of panel visibility, selected-hunter presentation generation, or callback rebinding.

#### Scenario: The panel is hidden and reopened while a command is pending

- **WHEN** an equipment command is awaiting completion and the equipment board is hidden, reopened, or rebound
- **THEN** the board SHALL NOT submit a second equipment command until the first command completes
- **AND** the current presentation SHALL rebuild from authoritative state after completion
