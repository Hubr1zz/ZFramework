## ADDED Requirements

### Requirement: Settlement runtime composition is phase-owned

SettlementPhaseManager SHALL internally bind its coordinator-owned ActionSession factory when configuring Settlement runtime generations. The Settlement phase port SHALL expose only current runtime/session access, gameplay and presentation composition, event resolution, refresh operations and the departure request boundary required by GameManager.

#### Scenario: Settlement composition is configured

- **WHEN** GameManager supplies the departure port and Settlement presentation dependencies
- **THEN** SettlementPhaseManager SHALL install its own ActionSession factory
- **AND** GameManager SHALL NOT receive or provide that factory
