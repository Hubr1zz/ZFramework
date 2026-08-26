## ADDED Requirements

### Requirement: GameManager consumes bounded phase operation ports

CampaignRuntime SHALL expose Settlement、Hunt 与 Showdown only through internal phase operation ports. GameManager SHALL NOT hold or request concrete phase manager/coordinator types, manager factories, ActionSession factories, or a caller-supplied current-generation provider.

#### Scenario: GameManager assembles the campaign runtime

- **WHEN** GameManager acquires the ZFramework CampaignRuntime lease
- **THEN** it SHALL receive one bounded operation port for each phase
- **AND** concrete phase managers and coordinators SHALL remain owned only by CampaignRuntime
- **AND** GameManager SHALL provide only scene dependencies, shared interaction ports and cross-phase callbacks

#### Scenario: The campaign runtime is inspected for factory escape paths

- **WHEN** a caller inspects the public campaign runtime and internal phase port contracts
- **THEN** neither contract SHALL accept arbitrary manager or ActionSession factories
- **AND** no phase port SHALL return a concrete phase manager or coordinator
