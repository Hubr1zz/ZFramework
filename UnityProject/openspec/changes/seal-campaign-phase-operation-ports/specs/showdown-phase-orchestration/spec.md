## ADDED Requirements

### Requirement: Showdown access remains a lifecycle compatibility port

CampaignRuntime SHALL expose ShowdownPhaseManager only through a bounded lifecycle and compatibility port. The port MAY support the existing GameManager combat façades, but SHALL NOT transfer Showdown manager ownership or introduce new combat rules.

#### Scenario: Existing combat façades access Showdown

- **WHEN** GameManager services an existing combat read or command façade
- **THEN** it SHALL delegate through the current Showdown phase port
- **AND** CampaignRuntime SHALL remain the unique owner of the Showdown manager and combat session lifecycle
