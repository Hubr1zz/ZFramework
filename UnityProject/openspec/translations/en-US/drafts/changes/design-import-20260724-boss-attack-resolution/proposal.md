# Boss Attack Resolution Rules

## Module Boundary

- Authoritative owners: `BossAttackResolver` and the temporary Precision/Agility decks
- Responsibility: Independently handle each target's per-attack hit/evasion deck, retained results, and damage requests.
- Independence: This module has its own state/lifecycle and acceptance scenarios, allowing it to be implemented and tested independently.

## Goal

Deliver the “Boss Attack Resolution Rules” and the corresponding implementation Feature, collaborating with other combat modules through explicit dependencies.

## Out of Scope

Do not duplicate the internal rules of other combat modules; express cross-module interactions only through dependency contracts.

## Acceptance

- Rule Scenarios can be independently verified through this module's interfaces.
- Every implementation difference is mapped to a task.
- Unity presentation does not become the authoritative rule state.
