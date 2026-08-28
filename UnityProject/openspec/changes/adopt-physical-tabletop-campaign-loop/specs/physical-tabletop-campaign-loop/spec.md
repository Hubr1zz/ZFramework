## ADDED Requirements

### Requirement: A physical tabletop input loop completes one non-combat expedition

The player SHALL be able to use world-space cards and pieces to resolve any blocking Settlement event, assemble a valid squad, choose an available destination, confirm a safe Hunt tile interaction, collect an available resource, retreat, and resolve any queued return event. Each accepted physical input SHALL request at most one command through the existing owning phase port, and the authoritative return transaction SHALL finish in Settlement with no pending return and exactly one configured season committed.

#### Scenario: Player completes an expedition through physical objects

- **WHEN** the player completes the available 3D prompts from Settlement departure through Hunt exploration and return-event resolution
- **THEN** the existing Settlement and Hunt ActionQueue environments SHALL serialize the accepted gameplay commands
- **AND** the campaign SHALL return to Settlement with the expedition recorded, its return checkpoint cleared, and one season advanced

### Requirement: Continue preserves the completed physical loop

The campaign SHALL persist the authoritative result of the physical expedition before it becomes available for another departure. Continuing from that save SHALL NOT repeat the return, season, or completed event commits, and the player SHALL be able to open the physical squad assembly board again.

#### Scenario: Player continues after a completed return

- **WHEN** the game is recreated and Continue loads the save written after the physical return loop
- **THEN** the year, season, expedition history, completed random-event set, inventory and roster SHALL match the saved authoritative state
- **AND** no pending return SHALL remain and the next physical departure entry SHALL be available

### Requirement: Resolved world input remains presentation-only

Mouse callbacks SHALL prevent screen UI click-through before forwarding a world-object intent. A touch, controller, automation, or test adapter MAY forward an already resolved world-object click without consulting unrelated mouse UI state. Pointer readiness, drag thresholds, highlighting, animation and input routing SHALL remain presentation state and SHALL NOT themselves enter a gameplay ActionQueue.

#### Scenario: A non-mouse adapter forwards a resolved world click

- **WHEN** an adapter has already resolved a valid 3D tile or resource piece independently of the mouse pointer
- **THEN** the View adapter SHALL forward that one intent to the existing phase port without applying the mouse-only UI-overlap filter
- **AND** all gameplay validation and mutation SHALL remain in the owning Hunt or Settlement command path
