## MODIFIED Requirements

### Requirement: GameManager delegates Settlement event-chain execution

GameManager SHALL retain cross-phase return submission, two-phase persistence, pending hunt records, restore projection creation/publication, FSM transitions and startup/load boundaries, while delegating `SettlementEventWork` execution and restore-projection continuation to `PlayableSettlementPhaseCoordinator`.

#### Scenario: A return has been reliably checkpointed

- **WHEN** the second return save succeeds and annual event work is pending
- **THEN** GameManager SHALL invoke the active Settlement coordinator to execute the work
- **AND** the coordinator SHALL own completion and continuation checks for that event runner

#### Scenario: The campaign resets during event restoration

- **WHEN** the active campaign runtime is reset or disposed while a Settlement event runner is awaiting
- **THEN** the coordinator SHALL cancel the runner before the old continuation can advance projection state
- **AND** GameManager SHALL retain its existing persistence and phase transition ownership
