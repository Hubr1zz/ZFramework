## ADDED Requirements

### Requirement: Hunter suppression has one bounded classification rule

The game SHALL treat hunter suppression as an integer in the inclusive range 0 through 8, with 4 as the default for newly authored hunter data. Values 0–2 SHALL classify as “疯狂”, values 3–5 as “正常”, and values 6–8 as “消极”. The classification SHALL be a read-only projection and SHALL NOT by itself apply attributes, event weights, AI behavior or departure restrictions.

#### Scenario: A hunter suppression value is classified

- **WHEN** a system or 3D View requests the classification of a hunter suppression value
- **THEN** the shared GameCore rule SHALL return the configured band using the bounded value
- **AND** the request SHALL NOT enqueue or mutate gameplay state

### Requirement: Positive suppression effects saturate at the maximum

An authored `AddInsanity` effect SHALL require a positive amount. When the existing event gameplay transaction accepts the effect, the hunter suppression value SHALL increase without integer overflow and SHALL saturate at 8. A rejected amount or missing authoritative target SHALL NOT change a hunter.

#### Scenario: An event exceeds the remaining suppression space

- **WHEN** an accepted gameplay event adds suppression that would exceed 8
- **THEN** the owning event ActionQueue transaction SHALL commit the hunter suppression value as 8
- **AND** no UI or View lifecycle event SHALL be added to the gameplay queue

### Requirement: Authored and persisted suppression data fail safely

Hunter template values outside 0 through 8 and non-positive authored `AddInsanity` amounts SHALL fail content validation. A legacy file save containing a hunter value outside the supported range SHALL be normalized to the nearest boundary while constructing the canonical restored snapshot, without producing gameplay effects.

#### Scenario: A legacy save contains an out-of-range value

- **WHEN** the production save recovery path restores a hunter whose persisted suppression is below 0 or above 8
- **THEN** the restored canonical snapshot SHALL contain 0 or 8 respectively
- **AND** the hunter's unrelated persisted fields SHALL remain unchanged

### Requirement: Existing 3D hunter information exposes the classification

The existing world-space hunter card and equipment dossier SHALL display both the authoritative suppression number and its “疯狂／正常／消极” classification. This projection SHALL remain read-only and SHALL NOT add a new interaction or gameplay authority.

#### Scenario: The player inspects a hunter

- **WHEN** a 3D hunter card or equipment dossier is populated from an authoritative hunter instance
- **THEN** its information text SHALL include the hunter suppression number and matching classification
