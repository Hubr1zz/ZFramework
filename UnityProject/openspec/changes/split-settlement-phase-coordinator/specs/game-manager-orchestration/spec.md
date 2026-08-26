## ADDED Requirements

### Requirement: Settlement internal composition is delegated

GameManager SHALL inject Settlement scene references, content catalogs and the cross-phase departure request once during campaign composition. The ZFramework-owned SettlementPhaseManager coordinator SHALL own Settlement ActionSession instances, fallback table creation, presentation rebinding and gameplay callback routing. GameManager SHALL NOT store a parallel Settlement ActionSession or bind individual crafting, equipment, recovery, recruitment, growth, training, symptom or invention callbacks.

#### Scenario: The Settlement generation is replaced

- **WHEN** a load or restart publishes another Settlement generation
- **THEN** GameManager SHALL continue its persistence and phase transaction responsibilities
- **AND** the Settlement coordinator SHALL invalidate the previous session and rebind presentation to the replacement generation

#### Scenario: The campaign runtime shuts down

- **WHEN** the ZFramework campaign lease is disposed
- **THEN** the Settlement coordinator SHALL invalidate its session and presentation callbacks before Settlement generations are released
- **AND** no parallel MonoBehaviour or global Settlement authority SHALL remain
