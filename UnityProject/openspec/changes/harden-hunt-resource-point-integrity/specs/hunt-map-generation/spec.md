---
schemaVersion: 2
category: feature
title: "狩猎地图生成"
---

## ADDED Requirements

### Requirement: Revealed tiles generate constrained weighted resource points
When a tile is revealed for the first time, GameCore SHALL select resource-point definitions only from valid stable IDs that have not reached their configured per-tile limit. Each selection SHALL preserve the configured spawn weights. Generation SHALL stop when the tile resource capacity is reached or no eligible definition remains; reaching one definition's limit SHALL NOT consume retries that can hide another eligible definition.

The current contract SHALL NOT infer a minimum count or a probability distribution between zero and the configured capacity.

#### Scenario: A high-weight point reaches its per-tile limit
- **WHEN** a high-weight resource point reaches its per-tile limit while another valid point remains eligible
- **THEN** generation continues from the remaining eligible pool until capacity is reached or that pool is exhausted

#### Scenario: A resource point type allows repeated instances
- **WHEN** one definition permits two instances and the tile has capacity for three
- **THEN** that definition may appear twice
- **AND** generation stops after its limit if no other definition remains

#### Scenario: No valid resource point remains
- **WHEN** every configured definition has reached its limit or has no stable ID
- **THEN** generation terminates without an unbounded retry loop
