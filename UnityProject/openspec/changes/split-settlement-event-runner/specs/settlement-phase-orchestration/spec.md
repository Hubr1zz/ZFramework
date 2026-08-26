## MODIFIED Requirements

### Requirement: Settlement event execution follows the active generation

The Settlement phase coordinator SHALL execute `SettlementEventWork` chains for the active Settlement runtime and session, and SHALL reject or cancel stale or parallel runners before they can advance restore projection state. GameManager SHALL retain pending-return persistence and projection ownership.

#### Scenario: A runner becomes stale during event execution

- **WHEN** the Settlement runtime is replaced, reset, or its ActionSession is deactivated while an event await is pending
- **THEN** the coordinator SHALL stop the continuation
- **AND** it SHALL NOT call projection `Complete`/`Prepare` or queue the next event chain

#### Scenario: A second event chain is requested while one is running

- **WHEN** another `SettlementEventWork` batch is submitted before the current runner completes
- **THEN** the request SHALL fail explicitly and a supplied restore projection SHALL remain fail-closed
- **AND** the coordinator SHALL NOT run two event chains concurrently
