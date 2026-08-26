## ADDED Requirements

### Requirement: Hunt runtime composition is phase-owned

HuntPhaseManager SHALL internally bind its manager factory, ActionSession factory and current-generation provider. The Hunt phase port SHALL expose only initialized candidate preparation, current presentation/session lifecycle, restore cleanup and the cross-phase callbacks required by GameManager.

#### Scenario: Hunt composition is configured

- **WHEN** GameManager supplies Hunt scene dependencies, interaction ports and cross-phase callbacks
- **THEN** HuntPhaseManager SHALL bind its coordinator to its own current generation
- **AND** GameManager SHALL NOT inject a current provider or receive manager/session factories
