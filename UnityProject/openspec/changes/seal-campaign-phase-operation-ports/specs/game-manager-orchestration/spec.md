## ADDED Requirements

### Requirement: GameManager consumes bounded phase operation ports

CampaignRuntime SHALL expose Settlement、Hunt 与 Showdown only through internal phase operation ports. GameManager SHALL NOT hold or request concrete phase manager/coordinator types, manager factories, ActionSession factories, or a caller-supplied current-generation provider. Phase lifecycle ports and phase gameplay ports SHALL remain separate contracts composed by the owning phase manager.

#### Scenario: GameManager assembles the campaign runtime

- **WHEN** GameManager acquires the ZFramework CampaignRuntime lease
- **THEN** it SHALL receive one bounded lifecycle operation port for each phase
- **AND** concrete phase managers and coordinators SHALL remain owned only by CampaignRuntime
- **AND** GameManager SHALL consume phase gameplay through explicit gameplay ports rather than concrete sessions

#### Scenario: The campaign runtime is inspected for escape paths

- **WHEN** a caller inspects the public campaign runtime and internal phase port contracts
- **THEN** neither contract SHALL accept arbitrary manager or ActionSession factories
- **AND** no phase port SHALL return a concrete phase manager or coordinator
- **AND** Showdown lifecycle substitution SHALL NOT require implementing its gameplay facade
