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

### Requirement: Physical item cards share one pointer drag lifecycle

Every physical equipment or storage card SHALL route Unity mouse input and additional world-space pointer adapters through one configurable drag-threshold lifecycle. Screen-space input SHALL be projected through the active main camera, while an adapter MAY supply an already resolved world position. Pointer state and motion SHALL remain presentation-only; only an accepted slot drop MAY request an authoritative gameplay command.

#### Scenario: A short press remains a click

- **WHEN** a pointer is pressed and released without exceeding the configured drag threshold
- **THEN** the card SHALL invoke its click behavior exactly once
- **AND** it SHALL NOT begin a slot drop or gameplay command

#### Scenario: A physical card is dragged into a slot

- **WHEN** the card collider is reached by the main-camera pointer ray and the pointer moves beyond the threshold into a compatible slot
- **THEN** the shared drag lifecycle SHALL resolve that slot through the card's production drop search
- **AND** the slot adapter MAY submit exactly one gameplay command through the existing Settlement command gate
