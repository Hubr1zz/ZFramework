## MODIFIED Requirements

### Requirement: Settlement phase enters through GameManager

The project SHALL enter Settlement through GameManager and activate the Settlement world and UI roots. SettlementPhaseManager SHALL own Settlement runtime generations and one plain Settlement coordinator. The coordinator SHALL own the active Settlement ActionSession plus the idempotent binding of scene-authored or fallback Settlement presentation. GameManager SHALL retain only the phase transition, cross-phase return transaction, persistence boundary and scene-reference injection.

#### Scenario: Entering Settlement

- **WHEN** the global phase changes to Settlement
- **THEN** GameManager activates Settlement roots and asks SettlementPhaseManager to activate the current generation
- **AND** the coordinator SHALL bind the current generation's ActionSession and Settlement presentation exactly once

### Requirement: Settlement presentation callbacks are generation scoped

Every Settlement table or UI gameplay callback SHALL resolve the currently published Settlement generation and its active ActionSession at invocation time. A callback captured before deactivation, reset, load replacement or campaign shutdown SHALL fail closed and SHALL NOT submit work to a replacement generation.

#### Scenario: A stale tabletop callback arrives after load replacement

- **WHEN** a Settlement table callback captured for the previous SettlementManager executes after another generation becomes current
- **THEN** the coordinator SHALL reject the callback before it enters the Settlement ActionQueue
- **AND** the replacement Settlement state SHALL remain unchanged

#### Scenario: Presentation rebinds to a restored generation

- **WHEN** a restored Settlement generation becomes current and its ActionSession activates
- **THEN** the existing scene-authored or fallback table and Settlement UI SHALL rebind to the restored SettlementManager
- **AND** the coordinator SHALL NOT create a duplicate table hierarchy
