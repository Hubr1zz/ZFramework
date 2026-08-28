---
schemaVersion: 2
category: architecture
title: "ActionQueue 全生命周期环境"
---

## ADDED Requirements

### Requirement: Shared event resolution remains an Action-owned adapter
The shared event resolver SHALL accept explicit event, actor, and narrow effect-command context from the owning Settlement or Hunt Action root. It SHALL NOT own a shared gameplay queue, an implicit selected actor, View callbacks, or a continuation API that advances an event chain outside the owning ActionEnvironment. Combat survival effects remain an existing compatibility boundary until Showdown gameplay is redesigned.

#### Scenario: An event produces child nodes
- **WHEN** the resolver returns chained events or encounter requests
- **THEN** the owning phase ActionQueue records and resumes those nodes through its own occurrence store and committed facts
- **AND** the resolver does not enqueue or present the next node itself

#### Scenario: A View awaits an event choice
- **WHEN** the world-space presenter returns an option and actor
- **THEN** the owning Action root explicitly prepares and commits the event transaction
- **AND** no resolver callback can mutate gameplay from the View
